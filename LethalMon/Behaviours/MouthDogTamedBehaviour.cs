using System;
using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;

namespace LethalMon.Behaviours;

public class MouthDogTamedBehaviour : TamedEnemyBehaviour
{
    #region Properties
    private MouthDogAI? _mouthDog = null; // Replace with enemy class

    internal MouthDogAI MouthDog
    {
        get
        {
            if (_mouthDog == null)
                _mouthDog = (Enemy as MouthDogAI)!;

            return _mouthDog;
        }
    }

    private float _cumulatedCheckDogsSeconds = 0f;
    
    private const float CheckDogsSecondsInterval = 1f;

    private float _howlTimer = 0f;

    private MouthDogAI? _targetDog = null;

    internal override string DefendingBehaviourDescription => "Runs away...";
    #endregion
    
    #region Custom behaviours
    internal enum CustomBehaviour
    {
        Howl = 1
    }
    
    internal override List<Tuple<string, string, Action>>? CustomBehaviourHandler =>
    [
        new (CustomBehaviour.Howl.ToString(), "Is howling!", OnHowl)
    ];

    public void OnHowl()
    {
        if (_howlTimer > 0f)
        {
            _howlTimer -= Time.deltaTime;
            return;
        }
        
        if (_targetDog != null)
        {
            _targetDog.suspicionLevel = 0;
            _targetDog = null;
        }
        
        SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
    }

    internal override bool CanDefend => false;

    #endregion
    
    #region Cooldowns

    private const string HowlCooldownId = "mouthdog_howl";
    
    internal override Cooldown[] Cooldowns => [new Cooldown(HowlCooldownId, "Howl", ModConfig.Instance.values.EyelessDogHowlCooldown)];

    private CooldownNetworkBehaviour? howlCooldown;
    #endregion

    #region Base Methods
    internal override void Start()
    {
        base.Start();

        howlCooldown = GetCooldownWithId(HowlCooldownId);

        if (IsTamed)
        {
            MouthDog.gameObject.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f);
            MouthDog.creatureSFX.volume = 0f;
            MouthDog.creatureVoice.pitch = 1.4f;
            MouthDog.breathingSFX = null;
            WalkMode();
        }
    }

    internal override void OnUpdate(bool update = false, bool doAIInterval = true)
    {
        base.OnUpdate(update, false);

        var position = MouthDog.transform.position;
        MouthDog.creatureAnimator.SetFloat("speedMultiplier", Vector3.ClampMagnitude(position - MouthDog.previousPosition, 1f).sqrMagnitude / (Time.deltaTime / 4f));
        MouthDog.previousPosition = position;
    }
    
    internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
    {
        if (Utils.IsHost)
            StartCoroutine(EscapedFromBallCoroutine(playerWhoThrewBall));
    }

    internal override void OnTamedFollowing()
    {
        base.OnTamedFollowing();

        if (howlCooldown != null && howlCooldown.IsFinished())
        {
            DefendOwnerFromCloseDogs();
        }
    }
    
    internal override void OnTamedDefending()
    {
        base.OnTamedDefending();

        if (_targetDog == null) return;

        if (Vector3.Distance(MouthDog.transform.position, MouthDog.destination) < 1f)
        {
            _howlTimer = MouthDog.screamSFX.length;
            howlCooldown?.Reset();
            var noisePosition = MouthDog.transform.position;
            _targetDog.lastHeardNoisePosition = noisePosition;
            _targetDog.DetectNoise(noisePosition, float.MaxValue);
            
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
            Howl();
    }

    public override bool CanBeTeleported() => !IsCurrentBehaviourTaming(TamingBehaviour.TamedDefending);
    #endregion

    #region Methods
    private IEnumerator EscapedFromBallCoroutine(PlayerControllerB playerWhoThrewBall)
    {
        int chaseTime = 5;
        while (chaseTime > 0 && !playerWhoThrewBall.isPlayerDead)
        {
            chaseTime--;
            MouthDog.DetectNoise(playerWhoThrewBall.transform.position, float.MaxValue);
            yield return new WaitForSeconds(5f);
        }
    }
    
    private void DefendOwnerFromCloseDogs()
    {
        _cumulatedCheckDogsSeconds += Time.deltaTime;
        
        if (_cumulatedCheckDogsSeconds < CheckDogsSecondsInterval) return;

        _cumulatedCheckDogsSeconds = 0f;

        var mouthDogAIs = FindObjectsOfType<MouthDogAI>();
        foreach (var mouthDogAI in mouthDogAIs)
        {
            if (mouthDogAI == MouthDog || mouthDogAI.isEnemyDead || Vector3.Distance(mouthDogAI.transform.position, ownerPlayer!.transform.position) > 20f) continue;

            TamedEnemyBehaviour? tamedEnemyBehaviour = mouthDogAI.GetComponentInParent<TamedEnemyBehaviour>();
            if (tamedEnemyBehaviour != null && tamedEnemyBehaviour.IsOwnedByAPlayer()) continue;
            
            bool foundPath = false;
            for (float i = 15f; i > 7f && !foundPath; i--)
            {
                Vector3[] farPositions = GetPositionsFarFromOwner(mouthDogAI, i);
                foreach (Vector3 farPosition in farPositions)
                {
                    if (MouthDog.SetDestinationToPosition(farPosition, true))
                    {
                        foundPath = true;
                        break;
                    }
                }
            }

            if (foundPath)
            {
                _targetDog = mouthDogAI;
                _howlTimer = 0f;
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

        Vector2 projectedDirectionOnXY = new(direction.x, direction.y);
        Vector2 perpendicular = Vector2.Perpendicular(projectedDirectionOnXY);

        for (int i = 1; i <= sidesPoints; ++i)
        {
            positionsArray[1 + (i - 1) * 2] = new Vector3(destinationPoint.x + perpendicular.x * sidesPointsDistance * i, destinationPoint.y + perpendicular.y * sidesPointsDistance * i, destinationPoint.z);
            positionsArray[2 + (i - 1) * 2] = new Vector3(destinationPoint.x - perpendicular.x * sidesPointsDistance * i, destinationPoint.y - perpendicular.y * sidesPointsDistance * i, destinationPoint.z);
        }

        for (int i = 0; i < positionsArray.Length; ++i)
        {
            if (Physics.Raycast(new Ray(positionsArray[i], Vector3.down), out RaycastHit hitInfo, 30f, 268437761, QueryTriggerInteraction.Ignore))
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
        if (MouthDog != null)
        {
            if (MouthDog.agent != null)
            {
                MouthDog.agent.speed = 5f;
            }
            MouthDog.creatureAnimator.Play("Base Layer.Idle1");
        }
    }
    
    private void ChaseMode()
    {
        if (MouthDog.agent != null)
        {
            MouthDog.agent.speed = 13f;
        }
        MouthDog.creatureAnimator.Play("Base Layer.Chase");
    }

    private void Howl()
    {
        MouthDog.creatureAnimator.Play("Base Layer.ChaseHowl");
        MouthDog.creatureVoice.PlayOneShot(MouthDog.screamSFX);
    }
    #endregion
}