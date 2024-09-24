using GameNetcodeStuff;

namespace LethalMon.CatchableEnemy;

public class CatchableButler : CatchableEnemy
{
    public CatchableButler() : base(10, "Butler", 4, "It will clean up any carcases and residue left behind by enemies, salvaging valuables from them in the process.\nA failed capture will cause them to brutally stab the thrower to death.")
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