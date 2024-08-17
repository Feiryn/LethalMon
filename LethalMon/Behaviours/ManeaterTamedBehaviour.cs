using GameNetcodeStuff;
using System.Collections.Generic;
using System;
using LethalMon.Items;
using Unity.Netcode;
using UnityEngine;
using System.Collections;

namespace LethalMon.Behaviours
{
#if DEBUG
    internal class ManeaterTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        private CaveDwellerAI? _maneater = null; // Replace with enemy class
        internal CaveDwellerAI Maneater
        {
            get
            {
                if (_maneater == null)
                    _maneater = (Enemy as CaveDwellerAI)!;

                return _maneater;
            }
        }

        internal override string DefendingBehaviourDescription => "You can change the displayed text when the enemy is defending by something more precise... Or remove this line to use the default one";

        internal override bool CanDefend => false; // You can return false to prevent the enemy from switching to defend mode in some cases (if already doing another action or if the enemy can't defend at all)
        #endregion

        #region Cooldowns
        private const string CooldownId = "monstername_cooldownname";
    
        internal override Cooldown[] Cooldowns => [new Cooldown(CooldownId, "Display text", 20f)];

        private CooldownNetworkBehaviour? cooldown;
        #endregion

        #region Custom behaviours
        internal enum CustomBehaviour
        {
            TestBehavior = 1
        }
        internal override List<Tuple<string, string, Action>>? CustomBehaviourHandler =>
        [
            new (CustomBehaviour.TestBehavior.ToString(), "Behaviour description text", OnTestBehavior)
        ];

        internal override void InitCustomBehaviour(int behaviour)
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
            new ActionKey() { Key = ModConfig.Instance.ActionKey1, Description = "Transform" }
        ];
        internal override List<ActionKey> ActionKeys => _actionKeys;

        internal override void ActionKey1Pressed()
        {
            base.ActionKey1Pressed();

            Maneater.StartTransformationAnim();

            /* USE THIS SOMEWHERE TO SHOW OR HIDE THE CONTROL TIP
                *   EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true/false);
            */
        }
        #endregion

        #region Base Methods
        internal override void Start()
        {
            SetTamedByHost_DEBUG();
            base.Start();
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);

            cooldown = GetCooldownWithId(CooldownId);
        }

        internal override void InitTamingBehaviour(TamingBehaviour behaviour)
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

        public override bool CanBeTeleported()
        {
            // HOST ONLY
            return base.CanBeTeleported();
        }
        #endregion
    }
#endif
}
