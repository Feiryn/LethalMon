using System;
using GameNetcodeStuff;
using LethalMon.Behaviours;
using LethalMon.Patches;
using UnityEngine;

namespace LethalMon.CatchableEnemy;

public class CatchableRedLocustBees : CatchableEnemy
{
    public CatchableRedLocustBees() : base(3, "Bees", 4)
    {
    }

    public override void CatchFailBehaviour(EnemyAI enemyAI, PlayerControllerB player)
    {
        RedLocustBees ai = (RedLocustBees) enemyAI;
        ai.SetMovingTowardsTargetPlayer(player);
        ai.SwitchToBehaviourState(2);
        RedLocustBeesPatch.AngryUntil.Add(ai.GetInstanceID(), DateTime.Now.AddSeconds(10));
    }
}