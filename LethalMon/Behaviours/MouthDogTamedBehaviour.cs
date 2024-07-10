using System;
using System.Collections;
using GameNetcodeStuff;
using UnityEngine;

namespace LethalMon.Behaviours;

public class MouthDogTamedBehaviour : TamedEnemyBehaviour
{
    internal MouthDogAI mouthDog  { get; private set; }

    private float CumulatedCheckDogsSeconds = 0f;
    
    private static readonly float CheckDogsSecondsInterval = 1f;

    private float HowlTimer = 0f;
    
    private bool howled = false;

    private float HowlCooldown = 0f;
    
    private static readonly float HowlCooldownSeconds = 5f;
    
    
    internal override void Start()
    {
        base.Start();

        mouthDog = (Enemy as MouthDogAI)!;
        if (mouthDog == null)
            mouthDog = gameObject.AddComponent<MouthDogAI>();

        if (ownerPlayer != null)
        {
            mouthDog.agent.speed = 5f;
            mouthDog.gameObject.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f);
            mouthDog.creatureAnimator.Play("Base Layer.Idle1");
            mouthDog.creatureSFX.volume = 0f;
            mouthDog.creatureVoice.pitch = 1.4f;
            mouthDog.breathingSFX = null;
        }
    }

    internal override void OnUpdate(bool update = false, bool doAIInterval = true)
    {
        base.OnUpdate(update, false);

        var position = mouthDog.transform.position;
        mouthDog.creatureAnimator.SetFloat("speedMultiplier", Vector3.ClampMagnitude(position - mouthDog.previousPosition, 1f).sqrMagnitude / (Time.deltaTime / 4f));
        mouthDog.previousPosition = position;

        if (HowlCooldown > 0f)
        {
            HowlCooldown -= Time.deltaTime;
        }
    }

    private IEnumerator EscapedFromBallCoroutine(PlayerControllerB playerWhoThrewBall)
    {
        int chaseTime = 5;
        while (chaseTime > 0 && !playerWhoThrewBall.isPlayerDead)
        {
            chaseTime--;
            mouthDog.DetectNoise(playerWhoThrewBall.transform.position, float.MaxValue);
            yield return new WaitForSeconds(5f);
        }
    }
    
    internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
    {
        StartCoroutine(EscapedFromBallCoroutine(playerWhoThrewBall));
    }

    internal override void OnTamedFollowing()
    {
        base.OnTamedFollowing();

        if (HowlCooldown <= 0f)
        {
            DefendOwnerFromCloseDogs();
        }
    }

    private void DefendOwnerFromCloseDogs()
    {
        CumulatedCheckDogsSeconds += Time.deltaTime;
        
        if (CumulatedCheckDogsSeconds < CheckDogsSecondsInterval) return;
        
        CumulatedCheckDogsSeconds = 0f;

        MouthDogAI[] mouthDogAIs = FindObjectsOfType<MouthDogAI>();
        foreach (MouthDogAI mouthDogAI in mouthDogAIs)
        {
            if (mouthDogAI == mouthDog || mouthDogAI.isEnemyDead || Vector3.Distance(mouthDogAI.transform.position, ownerPlayer!.transform.position) > 15f) continue;
            
            bool foundPath = false;
            for (float i = 15f; i > 5f; i--)
            {
                Vector3? farPosition = GetPositionFarFromOwner(mouthDogAI, i);
                if (farPosition != null && mouthDog.SetDestinationToPosition(farPosition.Value, true))
                {
                    foundPath = true;
                    break;
                }
            }

            if (foundPath)
            {
                mouthDog.agent.speed = 13f;
                mouthDog.creatureAnimator.Play("Base Layer.Chase");
                targetEnemy = mouthDogAI;
                howled = false;
                HowlTimer = 0f;
                SwitchToTamingBehaviour(TamingBehaviour.TamedDefending);
                return;
            }
        }
    }
    
    private Vector3? GetPositionFarFromOwner(MouthDogAI enemyToDefendFrom, float distance)
    {
        Vector3 enemyPosition = enemyToDefendFrom.transform.position;
        Vector3 direction = Vector3.Normalize(enemyPosition - ownerPlayer!.transform.position);
        Vector3 destinationPoint = enemyPosition + direction * distance;

        RaycastHit hitInfo;
        if (Physics.Raycast(new Ray(destinationPoint, Vector3.down), out hitInfo, 30f, 268437761, QueryTriggerInteraction.Ignore))
        {
            return hitInfo.point;
        }
        
        if (Physics.Raycast(new Ray(destinationPoint, Vector3.up), out hitInfo, 30f, 268437761, QueryTriggerInteraction.Ignore))
        {
            return hitInfo.point;
        }
        
        return null;
    }
    
    internal override void OnTamedDefending()
    {
        base.OnTamedDefending();

        if (HowlTimer > 0f)
        {
            HowlTimer -= Time.deltaTime;
            return;
        }

        if (howled)
        {
            if (targetEnemy != null)
            {
                ((MouthDogAI)targetEnemy!).suspicionLevel = 0;
                targetEnemy = null;
                mouthDog.agent.speed = 5f;
                mouthDog.creatureAnimator.Play("Base Layer.Idle1");
            }
        }
        else
        {
            string clipName = mouthDog.creatureAnimator.GetCurrentAnimatorClipInfo(0)[0].clip.name;
            if (Vector3.Distance(mouthDog.transform.position, mouthDog.destination) < 1f)
            {
                mouthDog.creatureAnimator.Play("Base Layer.ChaseHowl");
                mouthDog.creatureVoice.PlayOneShot(mouthDog.screamSFX);
                HowlTimer = mouthDog.screamSFX.length;
                howled = true;
                HowlCooldown = HowlCooldownSeconds;
                var noisePosition = mouthDog.transform.position;
                ((MouthDogAI)targetEnemy!).lastHeardNoisePosition = noisePosition;
                ((MouthDogAI)targetEnemy!).DetectNoise(noisePosition, float.MaxValue);
            }
        }
    }
}