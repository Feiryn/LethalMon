using System;
using System.Collections.Generic;
using System.Linq;
using LethalMon.Items;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LethalMon.Behaviours;

public class FlowermanTamedBehaviour : TamedEnemyBehaviour
{
    #region Properties
    internal FlowermanAI bracken { get; private set; }

    private EnemyAI? grabbedEnemyAi;

    // Left arm
    private Transform? arm1L;
    
    private Transform? arm2L;
    
    private Transform? arm3L;
    
    private Transform? hand1L;
    
    // Right arm
    private Transform? arm1R;
    
    private Transform? arm2R;
    
    private Transform? arm3R;
    
    private Transform? hand1R;

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

    private readonly float MaximumDistanceTowardsOwner = 50f; // Distance, after which the grabbed enemy will be dropped in order to return to the owner
    private readonly float MinimumDistanceTowardsOwner = 15f; // Distance, in which the bracken shouldn't drop the enemy again
    internal float DistanceTowardsOwner => ownerPlayer != null ? Vector3.Distance(ownerPlayer.transform.position, bracken.transform.position) : 0f;
    #endregion

    #region Base Methods
    internal override void Start()
    {
        base.Start();

        bracken = (Enemy as FlowermanAI)!;
        if (bracken == null)
            bracken = gameObject.AddComponent<FlowermanAI>();

        bracken.creatureAnimator.SetBool("sneak", value: true);
        bracken.creatureAnimator.Play("Base Layer.CreepForward");
        
        Transform? torso3 = bracken.gameObject.transform
            .Find("FlowermanModel")?
            .Find("AnimContainer")?
            .Find("metarig")?
            .Find("Torso1")?
            .Find("Torso2")?
            .Find("Torso3");

        if (torso3 != null)
        {
            arm1L = torso3.Find("Arm1.L");
            arm2L = arm1L.Find("Arm2.L");
            arm3L = arm2L.Find("Arm3.L");
            hand1L = arm3L.Find("Hand1.L");

            arm1R = torso3.Find("Arm1.R");
            arm2R = arm1R.Find("Arm2.R");
            arm3R = arm2R.Find("Arm3.R");
            hand1R = arm3R.Find("Hand1.R");
        }
    }

    internal override void LateUpdate()
    {
        base.LateUpdate();

        if (grabbedEnemyAi != null)
            SetArmsInHoldPosition();
    }

    internal override void OnTamedFollowing()
    {
        base.OnTamedFollowing();

        DefendOwnerFromClosestEnemy();
    }

    internal override void OnTamedDefending()
    {
        if (grabbedEnemyAi != null)
        {
            var distanceTowardsOwner = DistanceTowardsOwner;
            if (distanceTowardsOwner > MinimumDistanceTowardsOwner && (Vector3.Distance(bracken.transform.position, bracken.destination) < 2f || DistanceTowardsOwner > MaximumDistanceTowardsOwner))
            {
                LethalMon.Log("Enemy brought to destination or far enough away from owner, release it. Distance to owner: " + DistanceTowardsOwner);

                ReleaseEnemy();

                this.CalmDownAndFollow();
            }

            //LethalMon.Log("Enemy already grabbed and moving, skip AI interval");
        }
        else if (targetEnemy != null)
        {
            if (targetEnemy.isEnemyDead)
            {
                LethalMon.Log("Target is dead, stop targeting it");
                targetEnemy = null;
                this.CalmDownAndFollow();

                return;
            }

            if (targetEnemy.meshRenderers.Any(meshRendererTarget => bracken.meshRenderers.Any(meshRendererSelf => meshRendererSelf.bounds.Intersects(meshRendererTarget.bounds))))
            {
                LethalMon.Log("Collided with target, grab it");

                GrabEnemy(targetEnemy);
            }
            else
            {
                LethalMon.Log("Moving to target");

                bracken.SetDestinationToPosition(targetEnemy.transform.position);
            }
        }
        else
            this.CalmDownAndFollow();
    }

    internal override void DoAIInterval()
    {
        base.DoAIInterval();
    }

    internal override void OnUpdate(bool update = false, bool doAIInterval = true)
    {
        base.OnUpdate(update, doAIInterval);

        bracken.CalculateAnimationDirection();
        bracken.creatureAnimator.SetFloat("speedMultiplier", Vector3.ClampMagnitude(base.transform.position - previousPosition, 1f).sqrMagnitude / (Time.deltaTime / 4f));
    }

    public override PokeballItem RetrieveInBall(Vector3 position)
    {
        this.ReleaseEnemy();

        return base.RetrieveInBall(position);
    }
    #endregion

    #region Methods
    internal void DefendOwnerFromClosestEnemy()
    {
        // Check if enemy in sight
        foreach (EnemyAI spawnedEnemy in RoundManager.Instance.SpawnedEnemies) // todo: maybe SphereCast with fixed radius instead of checking LoS for any enemy for performance?
        {
            if (spawnedEnemy != null && spawnedEnemy != bracken && !spawnedEnemy.isEnemyDead && grabbedMonstersPositions.ContainsKey(spawnedEnemy.GetType().Name) && spawnedEnemy.transform != null && bracken.CheckLineOfSightForPosition(spawnedEnemy.transform.position))
            {
                targetEnemy = spawnedEnemy;
                this.DefendOwner();
                LethalMon.Log("Targeting " + spawnedEnemy.enemyType.name);
                return;
            }
        }
    }

    public void DefendOwner()
    {
        StandUp();
        SwitchToTamingBehaviour(TamingBehaviour.TamedDefending);
    }

    public void StandUp()
    {
        bracken.creatureAngerVoice.Play();
        bracken.creatureAngerVoice.pitch = Random.Range(0.9f, 1.3f);
        bracken.creatureAnimator.SetBool("anger", true);
        bracken.creatureAnimator.SetBool("sneak", false);
    }

    public void CalmDownAndFollow()
    {
        CalmDown();
        SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
    }

    public void CalmDown()
    {
        bracken.creatureAngerVoice.Stop();
        bracken.creatureAnimator.SetBool("sneak", true);
        bracken.creatureAnimator.SetBool("anger", false);
    }

    public void GrabEnemy(EnemyAI enemyAI)
    {
        if (grabbedEnemyAi != null)
        {
            this.ReleaseEnemy();
        }

        bracken.creatureAngerVoice.Stop();
        enemyAI.enabled = false;
        enemyAI.agent.enabled = false;
        var enemyAiTransform = enemyAI.transform;
        var flowermanTransform = bracken.transform;
        enemyAiTransform.transform.SetParent(flowermanTransform);

        if (grabbedMonstersPositions.ContainsKey(enemyAI.GetType().Name))
        {
            Tuple<float, float, Quaternion> monsterPositions = grabbedMonstersPositions[enemyAI.GetType().Name];
            enemyAiTransform.localPosition = Vector3.up * monsterPositions.Item1 + Vector3.forward * monsterPositions.Item2;
            enemyAiTransform.localRotation = monsterPositions.Item3;
        }
        
        grabbedEnemyAi = enemyAI;
        targetEnemy = null;

        var farthestPosition = bracken.ChooseFarthestNodeFromPosition(enemyAiTransform.position).position;
        bracken.SetDestinationToPosition(farthestPosition);
        LethalMon.Log("Moving to " + farthestPosition);
    }

    public void ReleaseEnemy()
    {
        if (grabbedEnemyAi == null) return;

        Transform enemyAiTransform = grabbedEnemyAi.transform;
        enemyAiTransform.SetParent(null);
        var selfTransform = bracken.transform;
        enemyAiTransform.localPosition = selfTransform.localPosition;
        enemyAiTransform.position = selfTransform.position;
        enemyAiTransform.rotation = selfTransform.rotation;
        enemyAiTransform.localRotation = selfTransform.localRotation;
        grabbedEnemyAi.enabled = true;
        grabbedEnemyAi.agent.enabled = true;
        grabbedEnemyAi = null;
            
        LethalMon.Log("Enemy release");
    }

    private void SetArmsInHoldPosition()
    {
        if (arm1L != null) arm1L.localRotation = Quaternion.Euler(-115.4f, -103.6f, -162.8f);
        if (arm2L != null) arm2L.localRotation = Quaternion.Euler(-15.3f, 0.4f, 37.87f);
        if (arm3L != null) arm3L.localRotation = Quaternion.Euler(-88.09f, 93.4f, 8.3f);
        if (hand1L != null) hand1L.localRotation = Quaternion.Euler(-22.3f, 0f, 0f);

        if (arm1R != null) arm1R.localRotation = Quaternion.Euler(-81.5f, 88.9f, -553.6f);
        if (arm2R != null) arm2R.localRotation = Quaternion.Euler(-50.7f, -92.46f, 6f);
        if (arm3R != null) arm3R.localRotation = Quaternion.Euler(-50.6f, 5.84f, 0f);
        if (hand1R != null) hand1R.localRotation = Quaternion.Euler(-69.2f, 0f, 0f);
    }
    #endregion
}