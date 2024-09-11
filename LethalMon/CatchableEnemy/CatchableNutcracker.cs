using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.CatchableEnemy;

public class CatchableNutcracker : CatchableEnemy
{
    public CatchableNutcracker() : base(8, "Nutcracker", 9, "As devoted guardians, they will guard their master and shoot any enemies within line of sight. Though be careful to stay away from the line of fire.\nWhen the thrower fails to capture a Nutcracker, it will enter a manic state where it will rotate wildly while shooting in all directions.")
    {
    }

    public override void BeforeCapture(EnemyAI enemyAI, PlayerControllerB player)
    {
        base.BeforeCapture(enemyAI, player);

        Utils.EnableShotgunHeldByEnemyAi(enemyAI, false);
    }
}