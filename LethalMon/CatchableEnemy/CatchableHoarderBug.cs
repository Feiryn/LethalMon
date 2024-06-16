using GameNetcodeStuff;
using LethalMon.AI;
using UnityEngine;

namespace LethalMon.CatchableEnemy;

public class CatchableHoarderBug : CatchableEnemy
{
    public CatchableHoarderBug() : base(2, 3)
    {
    }

    public override void CatchFailBehaviour(EnemyAI enemyAI, PlayerControllerB player)
    {
        HoarderBugAI ai = (HoarderBugAI) enemyAI;
        ai.angryTimer = 10f;
        ai.angryAtPlayer = player;
    }

    public override CustomAI AddAiComponent(GameObject gameObject)
    {
        return gameObject.AddComponent<HoarderBugCustomAI>();
    }
}