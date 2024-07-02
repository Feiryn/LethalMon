using GameNetcodeStuff;
using LethalMon.Behaviours;
using UnityEngine;

namespace LethalMon.CatchableEnemy;

public class CatchableFlowerman : CatchableEnemy
{
    public CatchableFlowerman() : base(1, "Bracken", 9)
    {
    }

    public override void CatchFailBehaviour(EnemyAI enemyAI, PlayerControllerB player)
    {
        (enemyAI as FlowermanAI)!.AddToAngerMeter(float.MaxValue);
    }
}