using GameNetcodeStuff;
using System.Collections.Generic;
using System;
using LethalMon.Items;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.Behaviours
{
#if DEBUG
    internal class ExampleTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        private TestEnemy? _testEnemy = null; // Replace with enemy class
        internal TestEnemy TestEnemy
        {
            get
            {
                if (_testEnemy == null)
                    _testEnemy = (Enemy as TestEnemy)!;

                return _testEnemy;
            }
        }

        public override string DefendingBehaviourDescription => "You can change the displayed text when the enemy is defending by something more precise... Or remove this line to use the default one";

        public override bool CanDefend => false; // You can return false to prevent the enemy from switching to defend mode in some cases (if already doing another action or if the enemy can't defend at all)
        #endregion

        #region Cooldowns
        private const string CooldownId = "monstername_cooldownname";
    
        public override Cooldown[] Cooldowns => [new Cooldown(CooldownId, "Display text", 20f)];

        private CooldownNetworkBehaviour? cooldown;
        #endregion

        #region Custom behaviours
        internal enum CustomBehaviour
        {
            TestBehavior = 1
        }
        public override List<Tuple<string, string, Action>>? CustomBehaviourHandler =>
        [
            new (CustomBehaviour.TestBehavior.ToString(), "Behaviour description text", OnTestBehavior)
        ];

        public override void InitCustomBehaviour(int behaviour)
        {
            // ANY CLIENT
            base.InitCustomBehaviour(behaviour);

            switch ((CustomBehaviour)behaviour)
            {
                case CustomBehaviour.TestBehavior:
                    break;

                default:
                    break;
            }
        }

        internal void OnTestBehavior()
        {
            /* USE THIS SOMEWHERE TO ACTIVATE THE CUSTOM BEHAVIOR
                *   SwitchToCustomBehaviour((int)CustomBehaviour.TestBehavior);
            */
        }
        #endregion

        #region Action Keys
        private readonly List<ActionKey> _actionKeys =
        [
            new ActionKey() { Key = ModConfig.Instance.ActionKey1, Description = "Action description here" }
        ];
        public override List<ActionKey> ActionKeys => _actionKeys;

        public override void ActionKey1Pressed()
        {
            base.ActionKey1Pressed();

            /* USE THIS SOMEWHERE TO SHOW OR HIDE THE CONTROL TIP
                *   EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true/false);
            */
        }
        #endregion

        #region Base Methods
        public override void Start()
        {
            base.Start();

            cooldown = GetCooldownWithId(CooldownId);
        }

        public override void InitTamingBehaviour(TamingBehaviour behaviour)
        {
            // ANY CLIENT
            base.InitTamingBehaviour(behaviour);

            switch(behaviour)
            {
                case TamingBehaviour.TamedFollowing:
                    break;

                case TamingBehaviour.TamedDefending:
                    break;

                default: break;
            }
        }

        public override void OnTamedFollowing()
        {
            // OWNER ONLY
            base.OnTamedFollowing();
        }

        public override void OnTamedDefending()
        {
            // OWNER ONLY
            base.OnTamedDefending();
        }

        public override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            // ANY CLIENT
            base.OnEscapedFromBall(playerWhoThrewBall);
        }

        public override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            // ANY CLIENT
            base.OnUpdate(update, doAIInterval);
        }

        public override void DoAIInterval()
        {
            // ANY CLIENT, every EnemyAI.updateDestinationInterval, if OnUpdate.doAIInterval = true
            base.DoAIInterval();
        }

        public override PokeballItem? RetrieveInBall(Vector3 position)
        {
            // ANY CLIENT
            return base.RetrieveInBall(position);
        }

        public override bool CanBeTeleported()
        {
            // HOST ONLY
            return base.CanBeTeleported();
        }
        #endregion

        #region RPCs
        [ServerRpc(RequireOwnership = false)]
        public void TestServerRpc(float someParameter, Vector3 anotherParameter)
        {
            // HOST ONLY
            TestClientRpc(someParameter, anotherParameter);
        }

        [ClientRpc]
        public void TestClientRpc(float someParameter, Vector3 anotherParameter)
        {
            // ANY CLIENT (HOST INCLUDED)
        }
        #endregion
    }
#endif
}
