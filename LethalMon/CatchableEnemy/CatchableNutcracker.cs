using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.CatchableEnemy;

public class CatchableNutcracker : CatchableEnemy
{
    public CatchableNutcracker() : base(8, "Nutcracker", 10)
    {
    }

    public override void BeforeCapture(EnemyAI enemyAI, PlayerControllerB player)
    {
        base.BeforeCapture(enemyAI, player);

        Utils.EnableShotgunHeldByEnemyAi(enemyAI, false);
    }
}