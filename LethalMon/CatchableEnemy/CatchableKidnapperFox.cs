using GameNetcodeStuff;

namespace LethalMon.CatchableEnemy
{
    internal class CatchableKidnapperFox : CatchableEnemy
    {
        public CatchableKidnapperFox() : base(9, "Fox", 4)
        {
        }

        public override bool CanBeCapturedBy(EnemyAI enemyAI, PlayerControllerB player)
        {
            return !(enemyAI as BushWolfEnemy)!.isHiding;
        }
    }
}
