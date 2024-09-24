using GameNetcodeStuff;

namespace LethalMon.CatchableEnemy;

public class CatchableFlowerman : CatchableEnemy
{
    public CatchableFlowerman() : base(1, "Bracken", 7, "Once sent out, it will drag potential threats away from its owner.\nA failed capture will cause it enter an enraged state and chase the thrower.")
    {
    }

    public override void BeforeCapture(EnemyAI enemyAI, PlayerControllerB player)
    {
        base.BeforeCapture(enemyAI, player);
        
        Compatibility.SnatchingBrackenCompatibility.DropPlayer((FlowermanAI) enemyAI);
    }
}