using GameNetcodeStuff;

namespace LethalMon.CatchableEnemy
{
    internal class CatchableTulipSnake : CatchableEnemy
    {
        public CatchableTulipSnake() : base(5, "Tulip Snake", 6, "When captured, Tulip snakes become much more obedient, allowing their owner to easily control where they go during flight.\nFailing to capture a Tulip snake will lead to them clinging to you, making themselves impossible to capture until they let go.")
        {
        }

        public override bool CanBeCapturedBy(EnemyAI enemyAI, PlayerControllerB player)
        {
            if ((enemyAI as FlowerSnakeEnemy)!.clingingToPlayer == player) return false;

            return base.CanBeCapturedBy(enemyAI, player);
        }
    }
}
