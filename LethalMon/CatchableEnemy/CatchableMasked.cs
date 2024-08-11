using GameNetcodeStuff;
using LethalMon.Behaviours;

namespace LethalMon.CatchableEnemy;

public class CatchableMasked : CatchableEnemy
{
    public CatchableMasked() : base(11, "Masked", 6)
    {
    }

    public override bool CanBeCapturedBy(EnemyAI enemyAI, PlayerControllerB player)
    {
        var masked = enemyAI as MaskedPlayerEnemy;
        if (masked != null)
            return masked.inSpecialAnimationWithPlayer == null;

        return base.CanBeCapturedBy(enemyAI, player);
    }
}