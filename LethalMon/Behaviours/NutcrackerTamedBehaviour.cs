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
    
    #endregion
    
    #region Custom behaviours
    private enum CustomBehaviour
    {
        LookForPlayer = 1,
        Rampage = 2,
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
            SwitchToDefaultBehaviour(1);
        }
        else if (distanceToPlayer < 3f || nutcracker.CheckLineOfSightForPosition(targetPlayerPos))
        {
            nutcracker.SetDestinationToPosition(nutcracker.transform.position);
            SwitchToCustomBehaviour((int) CustomBehaviour.Rampage);
            StartCoroutine(RampageCoroutine());
        }
        else
        {
            nutcracker.SetDestinationToPosition(targetPlayerPos);
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
        SwitchToDefaultBehaviour(1);
    }
    #endregion

    #region Base Methods

    void Start()
    {
        base.Start();
            
        nutcracker = (Enemy as NutcrackerEnemyAI)!;
    }

    internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
    {
        base.OnEscapedFromBall(playerWhoThrewBall);

        if (nutcracker.gun != null)
        {
            nutcracker.gun.gameObject.SetActive(true);
            nutcracker.gun.GetComponent<MeshRenderer>().enabled = true;
        }

        if (nutcracker.IsOwner)
        {
            targetPlayer = playerWhoThrewBall;

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
    }

    #endregion
}