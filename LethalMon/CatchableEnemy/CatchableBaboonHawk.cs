using GameNetcodeStuff;
using LethalMon.Behaviours;

namespace LethalMon.CatchableEnemy;

public class CatchableBaboonHawk : CatchableEnemy
{
    public CatchableBaboonHawk() : base(14, "Baboon Hawk", 4)
    {
    }

    public override bool CanBeCapturedBy(EnemyAI enemyAI, PlayerControllerB player)
    {
        if (enemyAI.TryGetComponent(out BaboonHawkTamedBehaviour baboonTamedBehaviour) && baboonTamedBehaviour.isEscapeFromBallCoroutineRunning)
            return false;

        if (enemyAI.TryGetComponent(out BaboonHawkTamedBehaviour.TinyHawkBehaviour _))
            return false;

        return base.CanBeCapturedBy(enemyAI, player);
    }
}