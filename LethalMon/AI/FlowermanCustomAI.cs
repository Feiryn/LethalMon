using System;
using System.Collections.Generic;
using System.Linq;
using LethalMon.Items;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LethalMon.AI;

public class FlowermanCustomAI : CustomAI
{
    private Vector3 agentLocalVelocity;
    
    public Transform animationContainer;
    
    private float velX;
    
    private float velZ;
    
    public AudioSource creatureAngerVoice;

    private EnemyAI? grabbedEnemyAi;

    private EnemyAI? targetEnemy;
    
    // Left arm
    private Transform arm1L;
    
    private Transform arm2L;
    
    private Transform arm3L;
    
    private Transform hand1L;
    
    // Right arm
    private Transform arm1R;
    
    private Transform arm2R;
    
    private Transform arm3R;
    
    private Transform hand1R;
    
    // Grabbed monsters positions (height, distance, rotation)
    private static Dictionary<string, Tuple<float, float, Quaternion>> grabbedMonstersPositions = new()
    {
        { "SandSpiderAI", new Tuple<float, float, Quaternion>(2, 1, Quaternion.Euler(-75, 0, 0)) },
        { "SpringManAI", new Tuple<float, float, Quaternion>(0.5f, 0, Quaternion.Euler(15, 0, 0)) },
        { "FlowermanAI", new Tuple<float, float, Quaternion>(0.5f, 0.2f, Quaternion.Euler(15, 0, 0)) },
        { "CrawlerAI", new Tuple<float, float, Quaternion>(2, 1.2f, Quaternion.Euler(-60, 0, 0)) },
        { "HoarderBugAI", new Tuple<float, float, Quaternion>(1.5f, 0.3f, Quaternion.Euler(15, 0, 0)) },
        { "CentipedeAI", new Tuple<float, float, Quaternion>(2.3f, 0.8f, Quaternion.Euler(-75, 0, 0)) },
        { "PufferAI", new Tuple<float, float, Quaternion>(2.3f, 0.1f, Quaternion.Euler(-75, 0, 180)) },
        { "JesterAI", new Tuple<float, float, Quaternion>(0.5f, 0.1f, Quaternion.Euler(15, 0, 0)) },
        { "NutcrackerEnemyAI", new Tuple<float, float, Quaternion>(0.5f, 0.1f, Quaternion.Euler(15, 0, 0)) },
        { "MaskedPlayerEnemy", new Tuple<float, float, Quaternion>(0.5f, 0.1f, Quaternion.Euler(15, 0, 0)) },
        { "ButlerEnemyAI", new Tuple<float, float, Quaternion>(0.5f, 0.5f, Quaternion.Euler(15, 0, 0)) }
    };
    
    public override void Start()
    {
        base.Start();
        
        creatureAnimator.SetBool("sneak", value: true);
        this.creatureAnimator.Play("Base Layer.CreepForward");
        
        Transform torso3 = this.gameObject.transform
            .Find("FlowermanModel")
            .Find("AnimContainer")
            .Find("metarig")
            .Find("Torso1")
            .Find("Torso2")
            .Find("Torso3");
        
        arm1L = torso3.Find("Arm1.L");
        arm2L = arm1L.Find("Arm2.L");
        arm3L = arm2L.Find("Arm3.L");
        hand1L = arm3L.Find("Hand1.L");
        
        arm1R = torso3.Find("Arm1.R");
        arm2R = arm1R.Find("Arm2.R");
        arm3R = arm2R.Find("Arm3.R");
        hand1R = arm3R.Find("Hand1.R");
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();
        
        // todo check inside only
        // todo cooldown

        if (this.grabbedEnemyAi != null)
        {
            if (Vector3.Distance(this.transform.position, this.destination) < 2f)
            {
                Debug.Log("Enemy brought to destination, release it");
                
                ReleaseEnemy();
                
                this.CalmDown();
            }
            
            Debug.Log("Enemy already grabbed and moving, skip AI interval");
            
            return;
        }
        else if (targetEnemy != null)
        {
            if (!targetEnemy.isEnemyDead)
            {
                if (this.targetEnemy.meshRenderers.Any(meshRendererTarget => this.meshRenderers.Any(meshRendererSelf => meshRendererSelf.bounds.Intersects(meshRendererTarget.bounds))))
                {
                    Debug.Log("Collided with target, grab it");
                    
                    GrabEnemy(targetEnemy);
                }
                else
                {
                    Debug.Log("Moving to target");
                    
                    SetDestinationToPosition(targetEnemy.transform.position);
                }

                return;
            }
            
            Debug.Log("Target is dead, stop targeting it");
            
            targetEnemy = null;
            
            this.CalmDown();
        }
        else
        {
            foreach (EnemyAI spawnedEnemy in RoundManager.Instance.SpawnedEnemies)
            {
                if (spawnedEnemy != null && !spawnedEnemy.isEnemyDead && grabbedMonstersPositions.ContainsKey(spawnedEnemy.GetType().Name) && spawnedEnemy.transform != null && this.CheckLineOfSightForPosition(spawnedEnemy.transform.position))
                {
                    targetEnemy = spawnedEnemy;
                    this.StandUp();
                    Debug.Log("Targeting " + targetEnemy.enemyType.name);
                    return;
                }
            } 

            Debug.Log("No grabbable enemy in sight");
        }
        
        Debug.Log("Follow owner");
        
        this.FollowOwner();
    }

    public override void Update()
    {
        base.Update();
        
        CalculateAnimationDirection();
    }

    private void CalculateAnimationDirection(float maxSpeed = 1f)
    {
        agentLocalVelocity = animationContainer.InverseTransformDirection(Vector3.ClampMagnitude(base.transform.position - previousPosition, 1f) / (Time.deltaTime * 2f));
        velX = Mathf.Lerp(velX, agentLocalVelocity.x, 10f * Time.deltaTime);
        creatureAnimator.SetFloat("VelocityX", Mathf.Clamp(velX, 0f - maxSpeed, maxSpeed));
        velZ = Mathf.Lerp(velZ, 0f - agentLocalVelocity.y, 10f * Time.deltaTime);
        creatureAnimator.SetFloat("VelocityZ", Mathf.Clamp(velZ, 0f - maxSpeed, maxSpeed));
        creatureAnimator.SetFloat("speedMultiplier", Vector3.ClampMagnitude(base.transform.position - previousPosition, 1f).sqrMagnitude / (Time.deltaTime / 4f));
        previousPosition = base.transform.position;
    }

    public void StandUp()
    {
        creatureAngerVoice.Play();
        creatureAngerVoice.pitch = Random.Range(0.9f, 1.3f);
        creatureAnimator.SetBool("anger", true);
        creatureAnimator.SetBool("sneak", false);
    }

    public void CalmDown()
    {
        creatureAngerVoice.Stop();
        creatureAnimator.SetBool("sneak", true);
        creatureAnimator.SetBool("anger", false);
    }

    public void GrabEnemy(EnemyAI enemyAI)
    {
        if (grabbedEnemyAi != null)
        {
            this.ReleaseEnemy();
        }

        creatureAngerVoice.Stop();
        enemyAI.enabled = false;
        enemyAI.agent.enabled = false;
        var enemyAiTransform = enemyAI.transform;
        var flowermanTransform = this.transform;
        enemyAiTransform.transform.SetParent(flowermanTransform);

        Tuple<float, float, Quaternion> monsterPositions = grabbedMonstersPositions[enemyAI.GetType().Name];
        enemyAiTransform.localPosition = Vector3.up * monsterPositions.Item1 + Vector3.forward * monsterPositions.Item2;
        enemyAiTransform.localRotation = monsterPositions.Item3;
        
        grabbedEnemyAi = enemyAI;
        targetEnemy = null;

        Vector3 farthestPosition = ChooseFarthestNodeFromPosition(enemyAiTransform.position).position;
        this.SetDestinationToPosition(farthestPosition);
        Debug.Log("Moving to " + farthestPosition);
    }

    public void ReleaseEnemy()
    {
        if (grabbedEnemyAi != null)
        {
            Transform enemyAiTransform = grabbedEnemyAi.transform;
            enemyAiTransform.SetParent(null);
            var selfTransform = this.transform;
            enemyAiTransform.localPosition = selfTransform.localPosition;
            enemyAiTransform.position = selfTransform.position;
            enemyAiTransform.rotation = selfTransform.rotation;
            enemyAiTransform.localRotation = selfTransform.localRotation;
            grabbedEnemyAi.enabled = true;
            grabbedEnemyAi.agent.enabled = true;
            grabbedEnemyAi = null;
            
            Debug.Log("Enemy release");
        }
    }

    public override void CopyProperties(EnemyAI enemyAI)
    {
        base.CopyProperties(enemyAI);

        this.animationContainer = ((FlowermanAI) enemyAI).animationContainer;
        this.creatureAngerVoice = ((FlowermanAI) enemyAI).creatureAngerVoice;
    }

    public override PokeballItem RetrieveInBall(Vector3 position)
    {
        this.ReleaseEnemy();
        
        return base.RetrieveInBall(position);
    }

    public void LateUpdate()
    {
        if (this.grabbedEnemyAi != null)
        {
            arm1L.localRotation = Quaternion.Euler(-115.4f, -103.6f, -162.8f);
            arm2L.localRotation = Quaternion.Euler(-15.3f, 0.4f, 37.87f);
            arm3L.localRotation = Quaternion.Euler(-88.09f, 93.4f, 8.3f);
            hand1L.localRotation = Quaternion.Euler(-22.3f, 0f, 0f);
            
            arm1R.localRotation = Quaternion.Euler(-81.5f, 88.9f, -553.6f);
            arm2R.localRotation = Quaternion.Euler(-50.7f, -92.46f, 6f);
            arm3R.localRotation = Quaternion.Euler(-50.6f, 5.84f, 0f);
            hand1R.localRotation = Quaternion.Euler(-69.2f, 0f, 0f);
        }
    }
}