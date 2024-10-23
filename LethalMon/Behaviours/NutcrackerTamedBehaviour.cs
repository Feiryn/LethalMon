using System;
using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.Behaviours;

internal class NutcrackerTamedBehaviour : TamedEnemyBehaviour
{
    #region Properties
    internal NutcrackerEnemyAI? _nutcracker = null;
    internal NutcrackerEnemyAI Nutcracker
    {
        get
        {
            if (_nutcracker == null)
                _nutcracker = (Enemy as NutcrackerEnemyAI)!;

            return _nutcracker;
        }
    }

    public override string DefendingBehaviourDescription => "Shoots at an enemy!";
    #endregion
    
    #region Custom behaviours
    private enum CustomBehaviour
    {
        LookForPlayer = 1,
        Rampage = 2
    }
    public override List<Tuple<string, string, Action>>? CustomBehaviourHandler =>
    [
        new Tuple<string, string, Action>(CustomBehaviour.LookForPlayer.ToString(), "Is looking for you!", OnLookForPlayer),
        new Tuple<string, string, Action>(CustomBehaviour.Rampage.ToString(), "Is on a rampage!", OnRampage)
    ];

    void OnRampage() { }

    void OnLookForPlayer()
    {
        Vector3 targetPlayerPos = targetPlayer!.transform.position;
        float distanceToPlayer = DistanceToTargetPlayer;
        if (distanceToPlayer > 50f)
        {
            SwitchToDefaultBehaviour(0);
        }
        else if (distanceToPlayer < 5f && !Physics.Linecast(transform.position, targetPlayerPos, out _, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
        {
            Nutcracker.SetDestinationToPosition(Nutcracker.transform.position);
            SwitchToCustomBehaviour((int) CustomBehaviour.Rampage);
            StartCoroutine(RampageCoroutine());
        }
        else
        {
            Nutcracker.SetTargetDegreesToPosition(targetPlayerPos);
            SetTorsoTargetDegreesServerRPC();
            Nutcracker.SetDestinationToPosition(targetPlayerPos);
        }
    }
    
    internal IEnumerator RampageCoroutine()
    {
        Nutcracker.torsoTurnSpeed = float.MaxValue;
        int initialAngle = (int) Quaternion.LookRotation(targetPlayer!.transform.position).eulerAngles.x;
        for (int degrees = 0; degrees < 360; degrees += 10)
        {
            Nutcracker.targetTorsoDegrees = (initialAngle + degrees - 90 /* Initial nutcracker rotation is 90 degrees */) % 360;
            SetTorsoTargetDegreesServerRPC();
            yield return new WaitForSeconds(0.05f);
            Nutcracker.FireGunServerRpc();
        }

        Nutcracker.gun.shellsLoaded = 2;
        
        SwitchToDefaultBehaviour(0);
    }

    public override void OnTamedFollowing()
    {
        base.OnTamedFollowing();

        TargetNearestEnemy(true, false);
    }

    public override void InitTamingBehaviour(TamingBehaviour behaviour)
    {
        base.InitTamingBehaviour(behaviour);

        if (behaviour == TamingBehaviour.TamedDefending)
        {
            LethalMon.Log("Enter defending mode");
            Nutcracker.creatureAnimator.SetInteger("State", 1);
            if (Utils.IsHost && HasTargetEnemy)
            {
                Nutcracker.SetTargetDegreesToPosition(targetEnemy!.transform.position);   
                SetTorsoTargetDegreesServerRPC();
            }
        }
        else if (Nutcracker != null && Nutcracker.creatureVoice != null && Nutcracker.creatureAnimator != null)
        {
            LethalMon.Log("Enter following mode");
            Nutcracker.creatureVoice.Stop();
            Nutcracker.creatureAnimator.SetInteger("State", 0);
        }
    }
    
    

    public override void OnTamedDefending()
    {
        base.OnTamedDefending();
        
        if (Nutcracker is { aimingGun: false, reloadingGun: false, torsoTurning: false })
        {
            LethalMon.Log("Is not aiming nor reloading.");
            if (HasTargetEnemy && !targetEnemy!.isEnemyDead && targetEnemy.agent.enabled && Nutcracker.CheckLineOfSightForPosition(targetEnemy.transform.position, 70f, 60, 1f))
            {
                LethalMon.Log(targetEnemy + " is in LOS, aiming gun");
                Nutcracker.SetTargetDegreesToPosition(targetEnemy!.transform.position);
                SetTorsoTargetDegreesServerRPC();
                Nutcracker.AimGunServerRpc(targetEnemy!.transform.position);
            }
            else
            {
                LethalMon.Log("Enemy lost or dead, switch back to following");
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                targetEnemy = null;
            }
        }
    }

    #endregion

    #region Base Methods

    public override void Start()
    {
        base.Start();

        if (IsTamed)
        {
            Nutcracker.creatureVoice.volume = 0.5f;
            Nutcracker.GetComponentInChildren<PlayAudioAnimationEvent>().audioToPlay.volume = 0.25f;
        }
    }

    public override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
    {
        base.OnEscapedFromBall(playerWhoThrewBall);

        Utils.EnableShotgunHeldByEnemyAi(Nutcracker, true);

        if (Utils.IsHost)
        {
            targetPlayer = playerWhoThrewBall;
            Nutcracker.agent.speed = 10f;
            Nutcracker.timeSinceHittingPlayer = 0f; // Prevents nutcracker from leg kicking a player and become stuck

            SwitchToCustomBehaviour((int)CustomBehaviour.LookForPlayer);
        }
    }

    public override void OnUpdate(bool update = false, bool doAIInterval = true)
    {
        base.OnUpdate(false, false);

        Nutcracker.TurnTorsoToTargetDegrees();
        
        if (!Nutcracker.isEnemyDead && !Nutcracker.GrabGunIfNotHolding())
            return;
        
        if (Nutcracker.walkCheckInterval <= 0f)
        {
            Nutcracker.walkCheckInterval = 0.1f;
            Nutcracker.creatureAnimator.SetBool("IsWalking", (base.transform.position - Nutcracker.positionLastCheck).sqrMagnitude > 0.001f);
            Nutcracker.positionLastCheck = base.transform.position;
        }
        else
        {
            Nutcracker.walkCheckInterval -= Time.deltaTime;
        }
        
        Nutcracker.creatureAnimator.SetBool("Aiming", Nutcracker.aimingGun);
    }

    public override void OnRetrieveInBall()
    {
        base.OnRetrieveInBall();

        Utils.DestroyShotgunHeldByEnemyAi(Nutcracker);
    }

    #endregion
    
    #region RPCs
    [ServerRpc(RequireOwnership = false)]
    public void SetTorsoTargetDegreesServerRPC()
    {
        SetTorsoTargetDegreesClientRPC(Nutcracker.targetTorsoDegrees, Nutcracker.torsoTurnSpeed);
    }
    
    [ClientRpc]
    public void SetTorsoTargetDegreesClientRPC(int targetDegrees, float turnSpeed)
    {
        Nutcracker.targetTorsoDegrees = targetDegrees;
        LethalMon.Log("Target degrees received: " + Nutcracker.targetTorsoDegrees + " at speed " + turnSpeed);
    }
    #endregion
}