using GameNetcodeStuff;
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

        NutcrackerEnemyAI nutcrackerEnemyAI = (NutcrackerEnemyAI)enemyAI;
        if (nutcrackerEnemyAI.gun != null)
        {
            nutcrackerEnemyAI.gun.GetComponent<MeshRenderer>().enabled = false;
            nutcrackerEnemyAI.gun.gameObject.SetActive(false);
        }
    }
}