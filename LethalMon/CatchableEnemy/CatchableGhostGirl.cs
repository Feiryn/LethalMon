using GameNetcodeStuff;
using LethalMon.Behaviours;

namespace LethalMon.CatchableEnemy
{
    internal class CatchableGhostGirl() : CatchableEnemy("GhostGirl", 9,
        "Don't be afraid of her, friendly Ghost girls will blink away anything that threatens their friend's safety, even damaging them in the process!\nA failed capture will cause her to terrorize you and hunt you down.")
    {
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
