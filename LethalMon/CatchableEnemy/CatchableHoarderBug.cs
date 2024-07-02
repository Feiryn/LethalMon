using GameNetcodeStuff;
using LethalMon.Behaviours;
using UnityEngine;

namespace LethalMon.CatchableEnemy;

public class CatchableHoarderBug : CatchableEnemy
{
    public CatchableHoarderBug() : base(2, "Hoarding Bug", 3)
    {
    }

    public override void CatchFailBehaviour(EnemyAI enemyAI, PlayerControllerB player)
    {
        HoarderBugAI ai = (HoarderBugAI) enemyAI;
        ai.angryTimer = 10f;
        ai.angryAtPlayer = player;
    }
}