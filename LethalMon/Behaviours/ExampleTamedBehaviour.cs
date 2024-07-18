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
        internal TestEnemy testEnemy { get; private set; } // Replace with enemy class
        #endregion

        #region Custom behaviours
        internal enum CustomBehaviour
        {
            TestBehavior = 1
        }
        internal override List<Tuple<string, Action>>? CustomBehaviourHandler => new()
        {
            { new (CustomBehaviour.TestBehavior.ToString(), OnTestBehavior) }
        };

        internal override void InitCustomBehaviour(int behaviour)
        {
            // OWNER ONLY
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
        private List<ActionKey> _actionKeys = new List<ActionKey>()
        {
            new ActionKey() { actionKey = ModConfig.Instance.ActionKey1, description = "Action description here" }
        };
        internal override List<ActionKey> ActionKeys => _actionKeys;

        internal override void ActionKey1Pressed()
        {
            base.ActionKey1Pressed();

            /* USE THIS SOMEWHERE TO SHOW OR HIDE THE CONTROL TIP
                *   EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true/false);
            */
        }
        #endregion

        #region Base Methods
        internal override void Start()
        {
            base.Start();

            testEnemy = (Enemy as TestEnemy)!;
        }

        internal override void InitTamingBehaviour(TamingBehaviour behaviour)
        {
            // OWNER ONLY
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

        internal override void OnTamedFollowing()
        {
            // OWNER ONLY
            base.OnTamedFollowing();
        }

        internal override void OnTamedDefending()
        {
            // OWNER ONLY
            base.OnTamedDefending();
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            // ANY CLIENT
            base.OnEscapedFromBall(playerWhoThrewBall);
        }

        internal override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            // ANY CLIENT
            base.OnUpdate(update, doAIInterval);
        }

        internal override void DoAIInterval()
        {
            // ANY CLIENT, every EnemyAI.updateDestinationInterval, if OnUpdate.doAIInterval = true
            base.DoAIInterval();
        }

        public override PokeballItem? RetrieveInBall(Vector3 position)
        {
            // ANY CLIENT
            return base.RetrieveInBall(position);
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
