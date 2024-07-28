using GameNetcodeStuff;
using System.Collections.Generic;
using System;
using LethalMon.Items;
using UnityEngine;
using System.Collections;
using LethalMon.CustomPasses;

namespace LethalMon.Behaviours
{
#if DEBUG
    internal class MaskedTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        internal MaskedPlayerEnemy? _masked = null;
        internal MaskedPlayerEnemy Masked
        {
            get
            {
                if (_masked == null)
                    _masked = (Enemy as MaskedPlayerEnemy)!;

                return _masked;
            }
        }

        // internal override string DefendingBehaviourDescription => "Y";

        internal override bool CanDefend => false;

        Coroutine? lendMaskCoroutine = null;
        bool isWearingMask = false;

        static readonly float MaximumMaskWearingTime = 20f;
        float timeSinceWearingMask = 0f;
        #endregion

        #region Cooldowns
        private static readonly string CooldownId = "masked_lendmask";

        internal override Cooldown[] Cooldowns => new[] { new Cooldown(CooldownId, "Lending mask", 2f) };

        private CooldownNetworkBehaviour lendMaskCooldown;
        #endregion

        #region Custom behaviours
        internal enum CustomBehaviour
        {
            LendMask = 1
        }
        internal override List<Tuple<string, string, Action>>? CustomBehaviourHandler => new()
        {
            new (CustomBehaviour.LendMask.ToString(), "Lending mask", OnLendMaskBehavior)
        };

        internal override void InitCustomBehaviour(int behaviour)
        {
            // OWNER ONLY
            base.InitCustomBehaviour(behaviour);

            switch ((CustomBehaviour)behaviour)
            {
                case CustomBehaviour.LendMask:
                    break;

                default:
                    break;
            }
        }

        internal void OnLendMaskBehavior()
        {
            if (!isWearingMask) return;

            timeSinceWearingMask += Time.deltaTime;
            if(timeSinceWearingMask > MaximumMaskWearingTime)
            {
                GiveBackMask();
                if(IsOwner)
                    SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
            }
        }
        #endregion

        #region Action Keys
        private List<ActionKey> _actionKeys = new List<ActionKey>()
        {
            new ActionKey() { actionKey = ModConfig.Instance.ActionKey1, description = "Lend mask" }
        };
        internal override List<ActionKey> ActionKeys => _actionKeys;

        internal override void ActionKey1Pressed()
        {
            base.ActionKey1Pressed();

            if (ownerPlayer == null) return;

            if(lendMaskCooldown.IsFinished())
                LendMask();
        }
        #endregion

        #region Base Methods
        internal override void Start()
        {
            base.Start();

            lendMaskCooldown = GetCooldownWithId(CooldownId);
        }

        void OnDestroy()
        {
            if(lendMaskCoroutine != null)
                StopCoroutine(lendMaskCoroutine);
        }

        internal override void InitTamingBehaviour(TamingBehaviour behaviour)
        {
            // ANY CLIENT
            base.InitTamingBehaviour(behaviour);

            switch (behaviour)
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

        #region MaskLending
        void LendMask()
        {
            LethalMon.Log("LendMask");
            lendMaskCoroutine = StartCoroutine(LendMaskCoroutine());
            EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, false);
        }

        IEnumerator LendMaskCoroutine()
        {
            // todo: wear mask animation + glassify
            yield return null;
            isWearingMask = true;
            CustomPassManager.Instance.EnableCustomPass(CustomPassManager.CustomPassType.SeeThroughEnemies, true);
        }

        void GiveBackMask()
        {
            lendMaskCooldown.Reset();
            lendMaskCoroutine = StartCoroutine(LendMaskCoroutine());
        }

        IEnumerator GiveBackMaskCoroutine()
        {
            // todo: give back mask animation
            yield return null;
            isWearingMask = false;
            CustomPassManager.Instance.EnableCustomPass(CustomPassManager.CustomPassType.SeeThroughEnemies, false);
        }
        #endregion
    }
#endif
}
