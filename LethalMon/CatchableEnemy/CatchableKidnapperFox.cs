using GameNetcodeStuff;

namespace LethalMon.CatchableEnemy
{
    internal class CatchableKidnapperFox : CatchableEnemy
    {
        public CatchableKidnapperFox() : base(9, "Fox", 4, "Though tamed, Kidnapper foxes will remain as aggressive as ever, shooting their tongue at enemies to damage them for the safety of their owner.\nUpon failing a capture, a chase sequence will commence similar to its behavior when untamed.")
        {
        }

        public override bool CanBeCapturedBy(EnemyAI enemyAI, PlayerControllerB player)
        {
            return !(enemyAI as BushWolfEnemy)!.isHiding;
        }
    }
}
