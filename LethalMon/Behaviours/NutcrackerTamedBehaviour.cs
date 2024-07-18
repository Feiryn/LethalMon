using System;
using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;

namespace LethalMon.Behaviours;

public class NutcrackerTamedBehaviour : TamedEnemyBehaviour
{
    #region Properties
    
    internal NutcrackerEnemyAI nutcracker { get; private set; }
    
    private float lookTimer = 0f;

    private readonly float lookTimerInterval = 1f;
    
    #endregion
    
    #region Custom behaviours
    private enum CustomBehaviour
    {
        LookForPlayer = 1,
        Rampage = 2
    }
    internal override List<Tuple<string, Action>>? CustomBehaviourHandler => new()
    {
        new Tuple<string, Action>(CustomBehaviour.LookForPlayer.ToString(), OnLookForPlayer),
        new Tuple<string, Action>(CustomBehaviour.Rampage.ToString(), OnRampage)
    };

    void OnRampage()
    {
        nutcracker.TurnTorsoToTargetDegrees();
    }

    void OnLookForPlayer()
    {
        Vector3 targetPlayerPos = targetPlayer!.transform.position;
        float distanceToPlayer = Vector3.Distance(nutcracker.transform.position, targetPlayerPos);
        if (distanceToPlayer > 50f)
        {
            SwitchToDefaultBehaviour(0);
        }
        else if (distanceToPlayer < 5f && !Physics.Linecast(transform.position, targetPlayerPos, out _, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
        {
            nutcracker.SetDestinationToPosition(nutcracker.transform.position);
            SwitchToCustomBehaviour((int) CustomBehaviour.Rampage);
            StartCoroutine(RampageCoroutine());
        }
        else
        {
            nutcracker.SetTargetDegreesToPosition(targetPlayerPos);
            nutcracker.SetDestinationToPosition(targetPlayerPos);
            nutcracker.TurnTorsoToTargetDegrees();
        }
    }
    
    internal IEnumerator RampageCoroutine()
    {
        nutcracker.torsoTurnSpeed = float.MaxValue;
        int initialAngle = (int) Quaternion.LookRotation(targetPlayer!.transform.position).eulerAngles.x;
        for (int degrees = 0; degrees < 360; degrees += 10)
        {
            nutcracker.targetTorsoDegrees = (initialAngle + degrees - 90 /* Initial nutcracker rotation is 90 degrees */) % 360;
            yield return new WaitForSeconds(0.05f);
            nutcracker.FireGunServerRpc();
        }

        nutcracker.gun.shellsLoaded = 2;
        
        SwitchToDefaultBehaviour(0);
    }

    internal override void OnTamedFollowing()
    {
        base.OnTamedFollowing();

        lookTimer += Time.deltaTime;

        if (lookTimer >= lookTimerInterval)
        {
            lookTimer = 0f;
            
            LookForEnemies();
        }
    }

    private void LookForEnemies()
    {
        foreach (EnemyAI spawnedEnemy in RoundManager.Instance.SpawnedEnemies) // todo: maybe SphereCast with fixed radius instead of checking LoS for any enemy for performance?
        {
            if (spawnedEnemy != null && spawnedEnemy.transform != null && spawnedEnemy != nutcracker && !spawnedEnemy.isEnemyDead && nutcracker.CheckLineOfSightForPosition(spawnedEnemy.transform.position, 70f, 60, 1f))
            {
                targetEnemy = spawnedEnemy;
                SwitchToTamingBehaviour(TamingBehaviour.TamedDefending);
                LethalMon.Log("Targeting " + spawnedEnemy.enemyType.name);
                return;
            }
        }
    }

    internal override void InitTamingBehaviour(TamingBehaviour behaviour)
    {
        base.InitTamingBehaviour(behaviour);

        if (behaviour == TamingBehaviour.TamedDefending)
        {
            if (targetEnemy == null)
            {
                LethalMon.Log("No nutcracker target enemy set", LethalMon.LogType.Warning);
            }
            else
            {
                nutcracker.creatureAnimator.SetInteger("State", 1);
                nutcracker.SetTargetDegreesToPosition(targetEnemy!.transform.position);   
            }
        }
    }

    internal override void OnTamedDefending()
    {
        base.OnTamedDefending();
        
        nutcracker.TurnTorsoToTargetDegrees();
        if (nutcracker is { aimingGun: false, reloadingGun: false, torsoTurning: false })
        {
            LethalMon.Log("Is not aiming nor reloading.");
            if (targetEnemy != null && !targetEnemy.isEnemyDead && nutcracker.CheckLineOfSightForPosition(targetEnemy.transform.position, 70f, 60, 1f))
            {
                LethalMon.Log(targetEnemy + " is in LOS, aiming gun");
                nutcracker.SetTargetDegreesToPosition(targetEnemy!.transform.position);
                nutcracker.AimGunServerRpc(targetEnemy!.transform.position);
            }
            else
            {
                LethalMon.Log("Enemy lost or dead, switch back to following");
                nutcracker.creatureVoice.Stop();
                nutcracker.creatureAnimator.SetInteger("State", 0);
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                targetEnemy = null;
            }
        }
    }

    #endregion

    #region Base Methods

    void Start()
    {
        base.Start();
            
        nutcracker = (Enemy as NutcrackerEnemyAI)!;

        if (ownerPlayer != null)
        {
            nutcracker.creatureVoice.volume = 0.5f;
        }
    }

    internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
    {
        base.OnEscapedFromBall(playerWhoThrewBall);

        Utils.EnableShotgunHeldByEnemyAi(nutcracker, true);

        if (Utils.IsHost)
        {
            targetPlayer = playerWhoThrewBall;
            nutcracker.agent.speed = 10f;
            nutcracker.timeSinceHittingPlayer = 0f; // Prevents nutcracker from leg kicking a player and become stuck

            SwitchToCustomBehaviour((int)CustomBehaviour.LookForPlayer);
        }
    }

    internal override void OnUpdate(bool update = false, bool doAIInterval = true)
    {
        base.OnUpdate(false, false);
        
        if (nutcracker.walkCheckInterval <= 0f)
        {
            nutcracker.walkCheckInterval = 0.1f;
            nutcracker.creatureAnimator.SetBool("IsWalking", (base.transform.position - nutcracker.positionLastCheck).sqrMagnitude > 0.001f);
            nutcracker.positionLastCheck = base.transform.position;
        }
        else
        {
            nutcracker.walkCheckInterval -= Time.deltaTime;
        }
        
        nutcracker.creatureAnimator.SetBool("Aiming", nutcracker.aimingGun);
    }

    internal override void OnRetrieveInBall()
    {
        base.OnRetrieveInBall();

        Utils.DestroyShotgunHeldByEnemyAi(nutcracker);
    }

    #endregion
}