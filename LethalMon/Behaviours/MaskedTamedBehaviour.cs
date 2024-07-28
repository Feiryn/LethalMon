using GameNetcodeStuff;
using System.Collections.Generic;
using System;
using LethalMon.Items;
using UnityEngine;
using System.Collections;
using LethalMon.CustomPasses;
using static UnityEngine.Rendering.DebugUI;

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

        bool isWearingMask = false;

        static readonly float MaximumMaskWearingTime = 2f;
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

            ownerPlayer = Utils.CurrentPlayer;
            ownClientId = 0ul;
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
        }

        void OnDestroy()
        {
            StopAllCoroutines();
        }

        internal override void InitTamingBehaviour(TamingBehaviour behaviour)
        {
            // ANY CLIENT
            base.InitTamingBehaviour(behaviour);

            switch (behaviour)
            {
                case TamingBehaviour.TamedFollowing:
                    EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true);
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

            Masked.SetSuit(playerWhoThrewBall.currentSuitID);
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
            StartCoroutine(LendMaskCoroutine());
            EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, false);
        }

        IEnumerator LendMaskCoroutine()
        {
            // todo: wear mask animation + glassify


            // ---------------------------------------------------------------------
            // KillPlayerAnimationClientRpc code
            Masked.inSpecialAnimationWithPlayer = ownerPlayer!;
            Masked.inSpecialAnimationWithPlayer.inAnimationWithEnemy = Masked;
            if (Masked.inSpecialAnimationWithPlayer == GameNetworkManager.Instance.localPlayerController)
                Masked.inSpecialAnimationWithPlayer.CancelSpecialTriggerAnimations();

            //Masked.creatureAnimator.SetBool("killing", value: true);
            Masked.agent.enabled = false;
            Masked.inSpecialAnimationWithPlayer.inSpecialInteractAnimation = true;
            Masked.inSpecialAnimationWithPlayer.snapToServerPosition = true;
            Vector3 origin = ((!Masked.inSpecialAnimationWithPlayer.IsOwner) ? Masked.inSpecialAnimationWithPlayer.transform.parent.TransformPoint(Masked.inSpecialAnimationWithPlayer.serverPlayerPosition) : Masked.inSpecialAnimationWithPlayer.transform.position);
            Vector3 vector = Masked.transform.position - Masked.transform.forward * 2f;
            vector.y = origin.y;
            Masked.playerRay = new Ray(origin, vector - Masked.inSpecialAnimationWithPlayer.transform.position);

            // ---------------------------------------------------------------------
            // killAnimation code
            Vector3 endPosition = Masked.playerRay.GetPoint(0.7f);
            Masked.inSpecialAnimationWithPlayer.disableSyncInAnimation = true;
            Masked.inSpecialAnimationWithPlayer.disableLookInput = true;
            RoundManager.Instance.tempTransform.position = Masked.inSpecialAnimationWithPlayer.transform.position;
            RoundManager.Instance.tempTransform.LookAt(endPosition);
            Quaternion startingPlayerRot = Masked.inSpecialAnimationWithPlayer.transform.rotation;
            Quaternion targetRot = RoundManager.Instance.tempTransform.rotation;
            Vector3 startingPosition = Masked.transform.position;
            for (int i = 0; i < 8; i++)
            {
                if (i > 0)
                {
                    Masked.transform.LookAt(Masked.inSpecialAnimationWithPlayer.transform.position);
                    Masked.transform.eulerAngles = new Vector3(0f, base.transform.eulerAngles.y, 0f);
                }
                Masked.transform.position = Vector3.Lerp(startingPosition, endPosition, i / 8f);
                Masked.inSpecialAnimationWithPlayer.transform.rotation = Quaternion.Lerp(startingPlayerRot, targetRot, i / 8f);
                Masked.inSpecialAnimationWithPlayer.transform.eulerAngles = new Vector3(0f, Masked.inSpecialAnimationWithPlayer.transform.eulerAngles.y, 0f);
                yield return null;
            }
            Masked.transform.position = endPosition;
            Masked.inSpecialAnimationWithPlayer.transform.rotation = targetRot;
            Masked.inSpecialAnimationWithPlayer.transform.eulerAngles = new Vector3(0f, Masked.inSpecialAnimationWithPlayer.transform.eulerAngles.y, 0f);
            yield return new WaitForSeconds(0.3f);
            SetMaskGlowNoSound();

            yield return StartCoroutine(RotateMaskOnPlayerFace());

            Masked.FinishKillAnimation();
            isWearingMask = true;
            CustomPassManager.Instance.EnableCustomPass(CustomPassManager.CustomPassType.SeeThroughEnemies, true);
        }

        IEnumerator RotateMaskOnPlayerFace()
        {
            var mask = Masked.maskTypes[Masked.maskTypeIndex];
            mask.transform.SetParent(null);
            var maskStartingPos = mask.transform.position;
            var maskEndingPos = Masked.playerRay.GetPoint(0.1f);
            maskEndingPos.y = maskStartingPos.y;
            var maskStartingRot = mask.transform.rotation;
            var maskEndingRot = Masked.inSpecialAnimationWithPlayer.transform.rotation;
            var maskProgress = 0f;
            while (maskProgress < 1f)
            {
                maskProgress += Time.deltaTime / 2f;
                mask.transform.position = Vector3.Lerp(maskStartingPos, maskEndingPos, maskProgress);
                mask.transform.rotation = Quaternion.Lerp(maskStartingRot, maskEndingRot, Mathf.Min(maskProgress * 1.5f, 1f));
                yield return null;
            }

            mask.transform.SetParent(Masked.inSpecialAnimationWithPlayer.transform, true);
        }

        void GiveBackMask()
        {
            lendMaskCooldown.Reset();
            StartCoroutine(GiveBackMaskCoroutine());
        }

        IEnumerator GiveBackMaskCoroutine()
        {
            // todo: give back mask animation
            yield return null;
            isWearingMask = false;
            CustomPassManager.Instance.EnableCustomPass(CustomPassManager.CustomPassType.SeeThroughEnemies, false);
        }

        void SetMaskGlowNoSound(bool enable = true) // SetMaskGlow without sound
        {
            Masked.maskEyesGlow[Masked.maskTypeIndex].enabled = enable;
            Masked.maskEyesGlowLight.enabled = enable;
        }
        #endregion
    }
#endif
}
