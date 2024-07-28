using GameNetcodeStuff;

namespace LethalMon.CatchableEnemy;

public class CatchableButler : CatchableEnemy
{
    public CatchableButler() : base(10, "Butler", 4)
    {
    }


    public override void BeforeCapture(EnemyAI enemyAI, PlayerControllerB player)
    {
        base.BeforeCapture(enemyAI, player);

        ButlerEnemyAI butler = (ButlerEnemyAI) enemyAI;
        butler.ambience1.Stop();
        butler.ambience2.Stop();
    }
}