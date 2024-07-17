using GameNetcodeStuff;
using LethalMon.Behaviours;

namespace LethalMon.CatchableEnemy
{
    internal class CatchableGhostGirl : CatchableEnemy
    {
        public CatchableGhostGirl() : base(7, "GhostGirl", 10)
        {
        }

        public override bool CanBeCapturedBy(EnemyAI enemyAI, PlayerControllerB player)
        {
            var tamedBehaviour = enemyAI?.GetComponent<GhostGirlTamedBehaviour>();
            if(tamedBehaviour == null)
                return base.CanBeCapturedBy(enemyAI!, player);

            var customBehaviour = tamedBehaviour.CurrentCustomBehaviour;
            return customBehaviour == null || customBehaviour != (int)GhostGirlTamedBehaviour.CustomBehaviour.ScareThrowerAndHunt; // Unable to be captured again during this phase
        }
    }
}
