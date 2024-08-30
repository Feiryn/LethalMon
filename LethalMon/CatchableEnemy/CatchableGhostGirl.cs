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
            var tamedBehaviour = enemyAI?.GetComponent<GhostGirlTamedBehaviour>();
            if(tamedBehaviour == null)
                return base.CanBeCapturedBy(enemyAI!, player);

            return tamedBehaviour.CurrentCustomBehaviour < 0;
        }
    }
}
