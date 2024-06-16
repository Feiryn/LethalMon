using System;
using GameNetcodeStuff;
using LethalMon.AI;
using LethalMon.Patches;
using UnityEngine;

namespace LethalMon.CatchableEnemy;

public class CatchableRedLocustBees : CatchableEnemy
{
    public CatchableRedLocustBees() : base(3, 4)
    {
    }

    public override void CatchFailBehaviour(EnemyAI enemyAI, PlayerControllerB player)
    {
        RedLocustBees ai = (RedLocustBees) enemyAI;
        ai.SetMovingTowardsTargetPlayer(player);
        ai.SwitchToBehaviourState(2);
        RedLocustBeesPatch.AngryUntil.Add(ai.GetInstanceID(), DateTime.Now.AddSeconds(10));
    }

    public override CustomAI AddAiComponent(GameObject gameObject)
    {
        return gameObject.AddComponent<RedLocustBeesCustomAI>();
    }
}