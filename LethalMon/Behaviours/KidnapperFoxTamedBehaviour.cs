using GameNetcodeStuff;

namespace LethalMon.Behaviours
{
    internal class KidnapperFoxTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        internal BushWolfEnemy fox { get; private set; }
        #endregion

        #region Base Methods
        internal override void Start()
        {
            base.Start();

            fox = (Enemy as BushWolfEnemy)!;
        }

        internal override void OnTamedFollowing()
        {
            base.OnTamedFollowing();
        }

        internal override void OnTamedDefending()
        {
            base.OnTamedDefending();
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            base.OnEscapedFromBall(playerWhoThrewBall);
        }
        #endregion
    }
}
