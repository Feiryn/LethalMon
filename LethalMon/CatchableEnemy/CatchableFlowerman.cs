using GameNetcodeStuff;

namespace LethalMon.CatchableEnemy;

public class CatchableFlowerman : CatchableEnemy
{
    public CatchableFlowerman() : base(1, "Bracken", 7)
    {
    }

    public override void BeforeCapture(EnemyAI enemyAI, PlayerControllerB player)
    {
        base.BeforeCapture(enemyAI, player);
        
        Compatibility.SnatchingBrackenCompatibility.DropPlayer((FlowermanAI) enemyAI);
    }
}