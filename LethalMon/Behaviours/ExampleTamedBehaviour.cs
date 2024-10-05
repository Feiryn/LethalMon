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
        /*
         * The TamedEnemyBehaviour class is added to all monsters.
         * It controls the behaviour of the monster when it is tamed (and for few other things like the behaviour when the monster fails to be captured).
         *
         * Some behaviours are added to the base enemy's behaviours.
         * After being added, the behaviours of the enemy are the following:
         * - 0..n: Default behaviours
         * - n+1: TamedFollowing (when the monster is following the player)
         * - n+2: TamedDefending (when the monster is defending the player). The monster turns to this state automatically when the player is attacked, and the <cref="TamedEnemyBehaviour.targetEnemy"/> is updated
         * - n+3..: Custom behaviours defined in addition to the following and defending behaviours
         *
         * The corresponding methods are called at each Update() when the monster is in the corresponding state (OnTamedFollowing, OnTamedDefending, or the custom behaviour methods).
         *
         * Please note that the enemy can still switch to the default behaviours. So the original EnemyAI can switch the behaviour to the default ones.
         * You may need to patch the original EnemyAI to prevent the enemy from switching to the default behaviours in some cases.
         *
         * You can still use the original EnemyAI instance to control the monster when it is tamed.
         *
         * The original enemy AI is not called when the monster is tamed.
         *
         * Check the public methods and properties of the TamedEnemyBehaviour class to see what you can override and use. There are many useful methods and properties.
         *
         * Also check already implemented monsters to see how to use the TamedEnemyBehaviour class.
         */
        
        #region Properties
        private TestEnemy? _testEnemy = null;
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
    
        /*
         * The cooldowns of the monster.
         * These classes are definitions only, the cooldowns are managed by the CooldownNetworkBehaviour class and can be accessed with the GetCooldownWithId method.
         */
        public override Cooldown[] Cooldowns => [new Cooldown(CooldownId, "Display text", 20f)];

        private CooldownNetworkBehaviour? cooldown;
        #endregion

        #region Custom behaviours
        // Additional custom behaviours in addition to the following and defending behaviours
        internal enum CustomBehaviour
        {
            TestBehavior = 1
        }
        
        /*
         * Custom behaviours handlers.
         * The first element of the tuple is the behaviour name.
         * The second element is the behaviour description used in the HUD.
         * The third element is the action to execute at each Update when the behaviour is active.
         */
        public override List<Tuple<string, string, Action>>? CustomBehaviourHandler =>
        [
            new (CustomBehaviour.TestBehavior.ToString(), "Behaviour description text", OnTestBehavior)
        ];

        /*
         * This function is called when the monster switches to a custom behaviour.
         * You can use this function to execute code when the monster switches to a custom behaviour.
         * This function is called by everyone.
         */
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

        /*
         * The previously defined custom behaviour action.
         * This function is called at each Update when the custom behaviour is active, by the host only
         */
        internal void OnTestBehavior()
        {
            /* USE THIS SOMEWHERE TO ACTIVATE THE CUSTOM BEHAVIOR
                *   SwitchToCustomBehaviour((int)CustomBehaviour.TestBehavior);
            */
        }
        #endregion

        #region Action Keys
        /*
         * The action keys of the monster.
         * You can use these keys to execute actions when the player presses the action keys.
         */
        private readonly List<ActionKey> _actionKeys =
        [
            new ActionKey() { Key = ModConfig.Instance.ActionKey1, Description = "Action description here" }
        ];
        
        public override List<ActionKey> ActionKeys => _actionKeys;

        /*
         * This function is called when the player presses the action key 1.
         * This function is called by the local player only.
         */
        public override void ActionKey1Pressed()
        {
            base.ActionKey1Pressed();

            /* USE THIS SOMEWHERE TO SHOW OR HIDE THE CONTROL TIP
                *   EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true/false);
            */
        }
        #endregion

        #region Base Methods
        /*
         * This function is called whether the monster is tamed or not.
         * So, use IsOwnedByAPlayer() to check if the monster is tamed before doing tamed specific stuff.
         */
        public override void Start()
        {
            base.Start();

            // Store the cooldown network behaviour
            cooldown = GetCooldownWithId(CooldownId);

            if (IsOwnedByAPlayer())
            {
                // Do stuff when the monster is tamed
            }
        }

        /*
         * This function is called when the monster switches to a tame behaviour (following or defending).
         * You can use this function to execute code when the monster switches to following or defending.
         * This function is called by everyone.
         */
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

        /*
         * This function is called at each Update() when the monster is following the player.
         * This function is called by the host only.
         */
        public override void OnTamedFollowing()
        {
            // OWNER ONLY
            base.OnTamedFollowing();
        }

        /*
         * This function is called at each Update() when the monster is defending the player.
         * This function is called by the host only.
         */
        public override void OnTamedDefending()
        {
            // OWNER ONLY
            base.OnTamedDefending();
        }

        /*
         * This function is called when the monster fails to be captured by the player.
         * You can make the monster do something when it fails to be captured (like become angry).
         */
        public override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            // ANY CLIENT
            base.OnEscapedFromBall(playerWhoThrewBall);
        }

        /*
         * This function is called at each Update() when the monster is tamed, not matter the behaviour.
         * You can use this function to execute global update code like for animations or other stuff.
         * This function is called by everyone.
         */
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

        /*
         * This function is called when the monster is retrieved in the ball.
         * You can cancel things to prevent bugs.
         * This function is called by everyone.
         */
        public override PokeballItem? RetrieveInBall(Vector3 position)
        {
            // ANY CLIENT
            return base.RetrieveInBall(position);
        }

        /*
         * This function is called to verify is the monster can be teleported when a teleportation is requested.
         * You can return false to prevent the monster from being teleported in some cases.
         * This function is called by the host only.
         */
        public override bool CanBeTeleported()
        {
            // HOST ONLY
            return base.CanBeTeleported();
        }
        #endregion

        /*
         * Simple example of an RPC
         */
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
