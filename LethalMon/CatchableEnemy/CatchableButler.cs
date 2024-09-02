using GameNetcodeStuff;

namespace LethalMon.CatchableEnemy;

public class CatchableButler : CatchableEnemy
{
    public CatchableButler() : base(10, "Butler", 4, "Butlers can clean up dead monsters bodies and turn them into scraps.\nWhen they are failed to be captured, they will speed up and stab the thrower.")
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