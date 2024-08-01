using System;
using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;

namespace LethalMon.Behaviours;

public class MouthDogTamedBehaviour : TamedEnemyBehaviour
{
    #region Properties
    internal MouthDogAI mouthDog  { get; private set; }

    private float CumulatedCheckDogsSeconds = 0f;
    
    private static readonly float CheckDogsSecondsInterval = 1f;

    internal float HowlTimer;

    internal override string DefendingBehaviourDescription => "Runs away...";
    #endregion
    
    #region Custom behaviours
    internal enum CustomBehaviour
    {
        Howl = 1
    }
    
    internal override List<Tuple<string, string, Action>>? CustomBehaviourHandler => new()
    {
        new (CustomBehaviour.Howl.ToString(), "Is howling!", OnHowl)
    };

    public void OnHowl()
    {
        if (HowlTimer > 0f)
        {
            HowlTimer -= Time.deltaTime;
            return;
        }
        
        if (targetEnemy != null)
        {
            ((MouthDogAI)targetEnemy!).suspicionLevel = 0;
            targetEnemy = null;
        }
        
        SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
    }

    internal override bool CanDefend => false;

    #endregion
    
    #region Cooldowns

    private static readonly string HowlCooldownId = "mouthdog_howl";
    
    internal override Cooldown[] Cooldowns => new[] { new Cooldown(HowlCooldownId, "Howl", ModConfig.Instance.values.EyelessDogHowlCooldown) };

    private CooldownNetworkBehaviour howlCooldown;

    #endregion
    
    #region Base Methods
    internal override void Start()
    {
        base.Start();

        mouthDog = (Enemy as MouthDogAI)!;
        if (mouthDog == null)
            mouthDog = gameObject.AddComponent<MouthDogAI>();

        howlCooldown = GetCooldownWithId(HowlCooldownId);
        
        if (ownerPlayer != null)
        {
            mouthDog.gameObject.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f);
            mouthDog.creatureSFX.volume = 0f;
            mouthDog.creatureVoice.pitch = 1.4f;
            mouthDog.breathingSFX = null;
            WalkMode();
        }
    }

    internal override void OnUpdate(bool update = false, bool doAIInterval = true)
    {
        base.OnUpdate(update, false);

        var position = mouthDog.transform.position;
        mouthDog.creatureAnimator.SetFloat("speedMultiplier", Vector3.ClampMagnitude(position - mouthDog.previousPosition, 1f).sqrMagnitude / (Time.deltaTime / 4f));
        mouthDog.previousPosition = position;
    }
    
    internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
    {
        if (Utils.IsHost)
            StartCoroutine(EscapedFromBallCoroutine(playerWhoThrewBall));
    }

    internal override void OnTamedFollowing()
    {
        base.OnTamedFollowing();

        if (howlCooldown.IsFinished())
        {
            DefendOwnerFromCloseDogs();
        }
    }
    
    internal override void OnTamedDefending()
    {
        base.OnTamedDefending();

        if (Vector3.Distance(mouthDog.transform.position, mouthDog.destination) < 1f)
        {
            HowlTimer = mouthDog.screamSFX.length;
            howlCooldown.Reset();
            var noisePosition = mouthDog.transform.position;
            ((MouthDogAI)targetEnemy!).lastHeardNoisePosition = noisePosition;
            ((MouthDogAI)targetEnemy!).DetectNoise(noisePosition, float.MaxValue);
            
            SwitchToCustomBehaviour((int) CustomBehaviour.Howl);
        }
    }

    internal override void InitTamingBehaviour(TamingBehaviour behaviour)
    {
        base.InitTamingBehaviour(behaviour);
        
        switch (behaviour)
        {
            case TamingBehaviour.TamedFollowing:
                WalkMode();
                break;
            case TamingBehaviour.TamedDefending:
                ChaseMode();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(behaviour), behaviour, null);
        }
    }

    internal override void InitCustomBehaviour(int behaviour)
    {
        base.InitCustomBehaviour(behaviour);

        if (behaviour == (int)CustomBehaviour.Howl)
        {
            Howl();
        }
    }

    public override bool CanBeTeleported()
    {
        return !IsCurrentBehaviourTaming(TamingBehaviour.TamedDefending);
    }
    #endregion

    #region Methods
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
    
    private void DefendOwnerFromCloseDogs()
    {
        CumulatedCheckDogsSeconds += Time.deltaTime;
        
        if (CumulatedCheckDogsSeconds < CheckDogsSecondsInterval) return;
        
        CumulatedCheckDogsSeconds = 0f;

        MouthDogAI[] mouthDogAIs = FindObjectsOfType<MouthDogAI>();
        foreach (MouthDogAI mouthDogAI in mouthDogAIs)
        {
            TamedEnemyBehaviour? tamedEnemyBehaviour = mouthDogAI.GetComponentInParent<TamedEnemyBehaviour>();
            if (mouthDogAI == mouthDog || mouthDogAI.isEnemyDead || Vector3.Distance(mouthDogAI.transform.position, ownerPlayer!.transform.position) > 20f || (tamedEnemyBehaviour != null && tamedEnemyBehaviour.IsOwnedByAPlayer())) continue;
            
            bool foundPath = false;
            for (float i = 15f; i > 7f && !foundPath; i--)
            {
                Vector3[] farPositions = GetPositionsFarFromOwner(mouthDogAI, i);
                foreach (Vector3 farPosition in farPositions)
                {
                    if (mouthDog.SetDestinationToPosition(farPosition, true))
                    {
                        foundPath = true;
                        break;
                    }
                }
            }

            if (foundPath)
            {
                targetEnemy = mouthDogAI;
                HowlTimer = 0f;
                SwitchToTamingBehaviour(TamingBehaviour.TamedDefending);
                return;
            }
        }
    }
    
    private Vector3[] GetPositionsFarFromOwner(MouthDogAI enemyToDefendFrom, float distance)
    {
        const int sidesPoints = 10;
        const float sidesPointsDistance = 0.5f;

        Vector3 enemyPosition = enemyToDefendFrom.transform.position;
        Vector3 direction = Vector3.Normalize(enemyPosition - ownerPlayer!.transform.position);
        Vector3 destinationPoint = enemyPosition + direction * distance;
        
        Vector3[] positionsArray = new Vector3[1 + sidesPoints * 2];
        positionsArray[0] = destinationPoint;

        Vector2 projectedDirectionOnXY = new Vector2(direction.x, direction.y);
        Vector2 perpendicular = Vector2.Perpendicular(projectedDirectionOnXY);

        for (int i = 1; i <= sidesPoints; ++i)
        {
            positionsArray[1 + (i - 1) * 2] = new Vector3(destinationPoint.x + perpendicular.x * sidesPointsDistance * i, destinationPoint.y + perpendicular.y * sidesPointsDistance * i, destinationPoint.z);
            positionsArray[2 + (i - 1) * 2] = new Vector3(destinationPoint.x - perpendicular.x * sidesPointsDistance * i, destinationPoint.y - perpendicular.y * sidesPointsDistance * i, destinationPoint.z);
        }

        for (int i = 0; i < positionsArray.Length; ++i)
        {
            RaycastHit hitInfo;
            if (Physics.Raycast(new Ray(positionsArray[i], Vector3.down), out hitInfo, 30f, 268437761, QueryTriggerInteraction.Ignore))
            {
                positionsArray[i] = hitInfo.point;
            }
            else if (Physics.Raycast(new Ray(positionsArray[i], Vector3.up), out hitInfo, 30f, 268437761, QueryTriggerInteraction.Ignore))
            {
                positionsArray[i] = hitInfo.point;
            }
        }

        return positionsArray;
    }
    
    private void WalkMode()
    {
        if (mouthDog != null)
        {
            if (mouthDog.agent != null)
            {
                mouthDog.agent.speed = 5f;
            }
            mouthDog.creatureAnimator.Play("Base Layer.Idle1");
        }
    }
    
    private void ChaseMode()
    {
        if (mouthDog.agent != null)
        {
            mouthDog.agent.speed = 13f;
        }
        mouthDog.creatureAnimator.Play("Base Layer.Chase");
    }

    private void Howl()
    {
        mouthDog.creatureAnimator.Play("Base Layer.ChaseHowl");
        mouthDog.creatureVoice.PlayOneShot(mouthDog.screamSFX);
    }
    #endregion
}