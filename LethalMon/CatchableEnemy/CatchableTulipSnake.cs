using GameNetcodeStuff;

namespace LethalMon.CatchableEnemy
{
    internal class CatchableTulipSnake : CatchableEnemy
    {
        public CatchableTulipSnake() : base(5, "Tulip Snake", 6)
        {
        }

        public override bool CanBeCapturedBy(EnemyAI enemyAI, PlayerControllerB player)
        {
            if ((enemyAI as FlowerSnakeEnemy)!.clingingToPlayer == player) return false;

            return base.CanBeCapturedBy(enemyAI, player);
        }
    }
}
