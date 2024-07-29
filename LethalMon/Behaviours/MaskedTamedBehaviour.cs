﻿using GameNetcodeStuff;
using System.Collections.Generic;
using System;
using LethalMon.Items;
using UnityEngine;
using System.Collections;
using LethalMon.CustomPasses;
using static UnityEngine.Rendering.DebugUI;
using Unity.Netcode;

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
        List<Material> originalMaskMaterials = new();
        Transform? originalMaskParent = null;
        Vector3 originalMaskLocalPosition = Vector3.zero;

        static readonly float MaximumMaskWearingTime = 2f;
        float timeSinceWearingMask = 0f;

        Coroutine? maskTransferCoroutine = null;

        GameObject? Mask => Masked?.maskTypes[Masked.maskTypeIndex];

        Color? originalNightVisionColor = null;
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
            if (!IsOwnerPlayer || !isWearingMask) return;

            timeSinceWearingMask += Time.deltaTime;
            if(timeSinceWearingMask > MaximumMaskWearingTime)
            {
                GiveBackMaskServerRpc();
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
            }

            FollowOwner();
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

            if (ownerPlayer == null || maskTransferCoroutine != null) return;

            if(isWearingMask)
            {
                GiveBackMaskServerRpc();
                return;
            }

            if(lendMaskCooldown.IsFinished())
                LendMaskServerRpc();
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

            if (Mask != null)
            {
                originalMaskParent = Mask.transform.parent;
                originalMaskLocalPosition = Mask.transform.localPosition;
            }
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

            if(Masked.agent && !Masked.agent.enabled)
                Masked.agent.enabled = true;
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

            Masked.CalculateAnimationDirection();

            if (isWearingMask && IsOwnerPlayer)
            {
                ownerPlayer!.insanityLevel -= Time.deltaTime / 10f;
                LethalMon.Log("Insanity: " + ownerPlayer!.insanityLevel);
            }
        }

        internal override void DoAIInterval()
        {
            // ANY CLIENT, every EnemyAI.updateDestinationInterval, if OnUpdate.doAIInterval = true
            base.DoAIInterval();

            if (ownerPlayer != null)
            {
                Masked.running = Vector3.Distance(Masked.transform.position, ownerPlayer.transform.position) > 5f;
                Masked.agent.speed = Masked.running ? 7f : 3.8f;
            }
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

        internal override void TurnTowardsPosition(Vector3 position)
        {
            Masked.LookAtPosition(position);
        }

        internal override void LateUpdate()
        {
            base.LateUpdate();

            Masked.LookAtFocusedPosition();
        }
        #endregion

        #region MaskLending
        [ServerRpc(RequireOwnership = false)]
        void LendMaskServerRpc()
        {
            LendMaskClientRpc();
        }

        [ClientRpc]
        void LendMaskClientRpc()
        {
            LethalMon.Log("LendMask");
            maskTransferCoroutine = StartCoroutine(LendMaskCoroutine());
            if(IsOwnerPlayer)
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, false);
        }

        IEnumerator LendMaskCoroutine()
        {
            if (ownerPlayer == null) yield break;

            Masked.SetHandsOutClientRpc(true);

            yield return StartCoroutine(FaceOwner());

            yield return new WaitForSeconds(0.3f);
            SetMaskGlowNoSound();

            yield return StartCoroutine(RotateMaskOnPlayerFace());

            SetMaskShaking(false);

            if(IsOwnerPlayer)
                SetMaskGlassified();

            Masked.FinishKillAnimation();
            isWearingMask = true;
            SetRedVision();
            /*if (Mask != null)
                Mask.layer = (int)Utils.LayerMasks.Mask.Props;*/
            CustomPassManager.Instance.EnableCustomPass(CustomPassManager.CustomPassType.SeeThroughEnemies, true);

            Masked.SetHandsOutClientRpc(false);
            maskTransferCoroutine = null;
        }

        [ServerRpc(RequireOwnership = false)]
        void GiveBackMaskServerRpc()
        {
            GiveBackMaskClientRpc();
        }

        [ClientRpc]
        void GiveBackMaskClientRpc()
        {
            LethalMon.Log("GiveBackMask");
            maskTransferCoroutine = StartCoroutine(GiveBackMaskCoroutine());
        }


        IEnumerator GiveBackMaskCoroutine()
        {
            Masked.SetHandsOutClientRpc(true);

            yield return StartCoroutine(FaceOwner());

            SetMaskShaking();
            if (IsOwnerPlayer)
                SetMaskGlassified(false);

            yield return new WaitForSeconds(0.3f);

            SetMaskGlowNoSound();

            yield return StartCoroutine(RotateMaskOnMaskedFace());

            Masked.FinishKillAnimation();
            isWearingMask = false;
            SetRedVision(false);
            /*if(Mask != null)
                Mask.layer = (int)Utils.LayerMasks.Mask.Enemies;*/
            CustomPassManager.Instance.EnableCustomPass(CustomPassManager.CustomPassType.SeeThroughEnemies, false);

            Masked.SetHandsOutClientRpc(false);
            maskTransferCoroutine = null;

            lendMaskCooldown.Reset();
        }

        IEnumerator RotateMaskOnPlayerFace()
        {
            if (ownerPlayer == null) yield break;

            var maskEndingPos = ownerPlayer.gameplayCamera.transform.position + ownerPlayer.gameplayCamera.transform.forward * 0.15f - Vector3.up * 0.1f;
            var maskEndingRot = ownerPlayer.transform.rotation;

            yield return StartCoroutine(RotateMaskTo(maskEndingPos, maskEndingRot, 1f, ownerPlayer.gameplayCamera.transform));
        }

        IEnumerator RotateMaskOnMaskedFace()
        {
            if (ownerPlayer == null) yield break;

            Vector3 endPosition;
            Quaternion endRotation;
            if (originalMaskParent != null)
            {
                LethalMon.Log("originalMaskParent not null: " + originalMaskParent.gameObject.name + " / " + originalMaskLocalPosition);
                endPosition = originalMaskParent.transform.position;
                endRotation = originalMaskParent.transform.rotation;
            }
            else
            {
                endPosition = Masked.transform.position + Vector3.up * 2f + Masked.transform.forward * 0.2f;
                endRotation = Masked.transform.rotation;
            }

            yield return StartCoroutine(RotateMaskTo(endPosition, endRotation, 1f, originalMaskParent != null ? originalMaskParent : Masked.transform));

            if (Mask != null)
                Mask.transform.localPosition = originalMaskLocalPosition;
        }

        IEnumerator RotateMaskTo(Vector3 endPosition, Quaternion endRotation, float duration = 1f, Transform? bindingTo = null)
        {
            var currentMask = Mask;
            if (currentMask == null) yield break;

            currentMask.transform.SetParent(null);
            var maskStartingPos = currentMask.transform.position;
            var maskStartingRot = currentMask.transform.rotation;
            var maskProgress = 0f;
            while (maskProgress < duration)
            {
                maskProgress += Time.deltaTime;
                currentMask.transform.position = Vector3.Lerp(maskStartingPos, endPosition, maskProgress);
                currentMask.transform.rotation = Quaternion.Lerp(maskStartingRot, endRotation, Mathf.Min(maskProgress * 1.5f, 1f));
                yield return null;
            }

            if (bindingTo != null)
                currentMask.transform.SetParent(bindingTo, true);
        }

        IEnumerator FaceOwner()
        {
            if (ownerPlayer == null) yield break;

            Masked.inSpecialAnimationWithPlayer = ownerPlayer;
            ownerPlayer.inAnimationWithEnemy = Masked;
            if (ownerPlayer == GameNetworkManager.Instance.localPlayerController)
                ownerPlayer.CancelSpecialTriggerAnimations();

            Masked.agent.enabled = false;
            ownerPlayer.inSpecialInteractAnimation = true;
            ownerPlayer.snapToServerPosition = true;
            Vector3 origin = ownerPlayer.IsOwner ? ownerPlayer.transform.position : ownerPlayer.transform.parent.TransformPoint(ownerPlayer.serverPlayerPosition);
            Vector3 vector = Masked.transform.position - Masked.transform.forward * 2f;
            vector.y = origin.y;
            Masked.playerRay = new Ray(origin, vector - ownerPlayer.transform.position);

            // ---------------------------------------------------------------------
            // killAnimation code
            Vector3 endPosition = Masked.playerRay.GetPoint(0.7f);
            ownerPlayer.disableSyncInAnimation = true;
            ownerPlayer.disableLookInput = true;
            RoundManager.Instance.tempTransform.position = ownerPlayer.transform.position;
            RoundManager.Instance.tempTransform.LookAt(endPosition);
            Quaternion startingPlayerRot = ownerPlayer.transform.rotation;
            Quaternion targetRot = RoundManager.Instance.tempTransform.rotation;
            Vector3 startingPosition = Masked.transform.position;
            for (int i = 0; i < 8; i++)
            {
                if (i > 0)
                {
                    Masked.transform.LookAt(ownerPlayer.transform.position);
                    Masked.transform.eulerAngles = new Vector3(0f, Masked.transform.eulerAngles.y, 0f);
                }
                Masked.transform.position = Vector3.Lerp(startingPosition, endPosition, i / 8f);
                ownerPlayer.transform.rotation = Quaternion.Lerp(startingPlayerRot, targetRot, i / 8f);
                ownerPlayer.transform.eulerAngles = new Vector3(0f, ownerPlayer.transform.eulerAngles.y, 0f);
                yield return null;
            }
            Masked.transform.position = endPosition;
            ownerPlayer.transform.rotation = targetRot;
            ownerPlayer.transform.eulerAngles = new Vector3(0f, ownerPlayer.transform.eulerAngles.y, 0f);

            Masked.LookAtPosition(ownerPlayer.transform.position);
            Masked.LookAtFocusedPosition();
        }

        void SetMaskGlowNoSound(bool enable = true) // SetMaskGlow without sound
        {
            Masked.maskEyesGlow[Masked.maskTypeIndex].enabled = enable;
            Masked.maskEyesGlowLight.enabled = enable;
        }

        void SetMaskGlassified(bool glassified = true)
        {
            var currentMask = Mask;
            if (currentMask == null) return;

            var mr = currentMask.transform.Find("Mesh").GetComponent<MeshRenderer>();
            if (mr == null) return;

            if(glassified)
            {
                if (originalMaskMaterials.Count > 0) return; // Already glassified

                List<Material> materials = new();
                foreach (var m in mr.materials)
                {
                    materials.Add(Utils.Glass);
                    originalMaskMaterials.Add(m);
                }
                mr.materials = materials.ToArray();
            }
            else
            {
                if (originalMaskMaterials.Count == 0) return; // Unable to revert materials
                foreach(var m in mr.materials)
                    DestroyImmediate(m);

                mr.materials = originalMaskMaterials.ToArray();
                originalMaskMaterials.Clear();
            }
        }

        void SetMaskShaking(bool shaking = true)
        {
            var maskAnimator = Mask?.GetComponent<Animator>();
            if (maskAnimator != null)
                maskAnimator.speed = shaking ? 1f : 0f; // 1f is default.. unsure why it's constantly shaking by default
        }

        void SetRedVision(bool enable = true)
        {
            if (ownerPlayer == null) return;

            if(enable)
            {
                originalNightVisionColor = ownerPlayer.nightVision.color;
                ownerPlayer.nightVision.color = Color.red;
                ownerPlayer.nightVision.enabled = true;
                LethalMon.Log("INTENSITY: " + ownerPlayer.nightVision.intensity);
                ownerPlayer.nightVision.intensity /= 2f;
            }
            else
            {
                ownerPlayer.nightVision.color = originalNightVisionColor.GetValueOrDefault(Color.white);
                ownerPlayer.nightVision.enabled = false;
                ownerPlayer.nightVision.intensity *= 2f;
            }
        }
        #endregion
    }
#endif
}