using GameNetcodeStuff;
using LethalMon.Behaviours;

namespace LethalMon.CatchableEnemy
{
    internal class CatchableGhostGirl : CatchableEnemy
    {
        public CatchableGhostGirl() : base(7, "GhostGirl", 9)
        {
        }

        public override bool CanBeCapturedBy(EnemyAI enemyAI, PlayerControllerB player)
        {
            var ghostGirl = enemyAI as DressGirlAI;
            if (ghostGirl == null || !ghostGirl.enemyMeshEnabled)
                return false;

            if (ghostGirl.TryGetComponent(out GhostGirlTamedBehaviour tamedBehaviour) && tamedBehaviour.CurrentCustomBehaviour >= 0)
                return false;

            return base.CanBeCapturedBy(enemyAI!, player);
        }
    }
}
