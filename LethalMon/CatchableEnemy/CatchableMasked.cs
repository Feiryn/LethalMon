using GameNetcodeStuff;
using LethalMon.Behaviours;

namespace LethalMon.CatchableEnemy;

public class CatchableMasked : CatchableEnemy
{
    public CatchableMasked() : base(11, "Masked", 5, "Once befriended, they will lend you their mask. This mask wll allow the wearer to sense enemies through hard surfaces.\nIf you fail to capture them, ghost-like projections will manifest from their body to attack you.")
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