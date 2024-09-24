using GameNetcodeStuff;
using LethalMon.Behaviours;

namespace LethalMon.CatchableEnemy;

public class CatchableBaboonHawk : CatchableEnemy
{
    public CatchableBaboonHawk() : base(14, "Baboon Hawk", 4, "A tamed Baboon hawk will protect its owner and use its echo-location to track items both inside and outside.\nA failed capture will lead an infant appearing and alerting a horde.")
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