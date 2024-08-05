using GameNetcodeStuff;
using System.Collections.Generic;
using System;
using LethalMon.Items;
using UnityEngine;
using System.Collections;
using LethalMon.CustomPasses;
using Unity.Netcode;
using System.Linq;

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
        Animator? _maskAnimator = null;
        Animator? MaskAnimator
        {
            get
            {
                if(_maskAnimator == null)
                    _maskAnimator = Mask?.GetComponent<Animator>();

                return _maskAnimator;
            }
        }

        Color? originalNightVisionColor = null;
        float originalNightVisionIntensity = 366f;

        public NetworkVariable<bool> escapeFromBallEventRunning = new NetworkVariable<bool>(false);
        static readonly float MaximumGhostChaseTime = 5f;
        bool isEscapedDEBUG = false;
        List<MaskedPlayerEnemy> spawnedGhostMimics = new List<MaskedPlayerEnemy>();
        #endregion

        /*#region MirageCompatibility
        public const string MirageReferenceChain = "Mirage";

        private static bool? _mirageEnabled;

        public static bool MirageEnabled
        {
            get
            {
                if (_mirageEnabled == null)
                    _mirageEnabled = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(MirageReferenceChain);

                return _mirageEnabled.Value;
            }
        }
        #endregion*/

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
            if (!isWearingMask || maskTransferCoroutine != null) return;

            if(IsOwnerPlayer)
                StartOfRound.Instance.fearLevel += Time.deltaTime / 10f;

            bool dropMask = false;

            if (MaskAnimator != null)
            {
                MaskAnimator.speed += Time.deltaTime / 10f;
                if (MaskAnimator.speed > 1f) dropMask = true;
            }
            else // Fallback
            {
                timeSinceWearingMask += Time.deltaTime;
                if (timeSinceWearingMask > MaximumMaskWearingTime) dropMask = true;
            }

            if (dropMask && IsOwnerPlayer)
            {
                ownerPlayer!.DamagePlayer(1, true, true, CauseOfDeath.Unknown, 8);

                if (!ownerPlayer.isPlayerDead)
                    GiveBackMaskServerRpc();
            }

            if(IsOwner)
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

            Ghostify(Masked);

            if (!escapeFromBallEventRunning.Value)
            {
                isEscapedDEBUG = true;
                SwitchToDefaultBehaviour(0);
                Masked.enabled = false;
                OnEscapedFromBall(ownerPlayer!);
            }
            return;

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

            ownerPlayer = Utils.AlivePlayers.Where((p) => p.playerClientId == 0ul).First();
            ownClientId = 0ul;
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
            Masked.EnableEnemyMesh(true);

            if (Mask != null)
            {
                originalMaskParent = Mask.transform.parent;
                originalMaskLocalPosition = Mask.transform.localPosition;
            }
        }

        void OnDestroy() => CleanUp();

        void OnDisable() => CleanUp();

        void CleanUp()
        {
            StopAllCoroutines();
            foreach (var ghostMimic in spawnedGhostMimics)
                RoundManager.Instance.DespawnEnemyGameObject(ghostMimic.NetworkObject);
            spawnedGhostMimics.Clear();
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

            if (Mask != null && !Mask.activeSelf)
                Mask.SetActive(true);

            if (IsOwner && !Masked.agent.enabled)
                Masked.agent.enabled = true;
        }

        internal override void OnTamedDefending()
        {
            // OWNER ONLY
            base.OnTamedDefending();

            // idea: decoy
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            // ANY CLIENT
            base.OnEscapedFromBall(playerWhoThrewBall);
            LethalMon.Log("OnEscapedFromBall");

            Masked.inSpecialAnimationWithPlayer = playerWhoThrewBall;
            if (IsOwner)
                StartCoroutine(MaskedEscapeFromBallCoroutine(playerWhoThrewBall));
        }

        internal IEnumerator MaskedEscapeFromBallCoroutine(PlayerControllerB playerWhoThrewBall)
        {
            SetEscapeFromBallEventRunningServerRpc(true);

            LethalMon.Log("MaskedEscapeFromBallCoroutine");
            Masked.agent.enabled = false;
            Masked.enabled = false;
            yield return new WaitForSeconds(1f);
            SetMaskGlowNoSound(true);
            //Masked.creatureSFX.PlayOneShot(Masked.enemyType.audioClips[0]);
            yield return new WaitForSeconds(0.5f);

            float timeGlowingUp = 0f; // Fallback
            float startIntensity = Masked.maskEyesGlowLight.intensity;
            float finalIntensity = startIntensity * 50f;
            while (Masked.maskEyesGlowLight.intensity < finalIntensity && timeGlowingUp < 2f)
            {
                Masked.maskEyesGlowLight.intensity += Time.deltaTime * 50f;
                timeGlowingUp += Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.3f);

            StartCoroutine(SpawnAndHandleGhostMimic(playerWhoThrewBall));
            Masked.maskEyesGlowLight.intensity /= 2f;
            yield return new WaitForSeconds(3.5f);

            StartCoroutine(SpawnAndHandleGhostMimic(playerWhoThrewBall));
            Masked.maskEyesGlowLight.intensity = startIntensity;

            float fallbackTimer = 0f;
            yield return new WaitWhile(() =>
            {
                fallbackTimer += Time.deltaTime;
                return spawnedGhostMimics.Count > 0 && fallbackTimer < 10f;
            });

            CleanUp();

            if (!isEscapedDEBUG)
                Masked.enabled = true;
            else
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
            Masked.agent.enabled = true;

            SetMaskGlowNoSound(false);

            isEscapedDEBUG = false;

            SetEscapeFromBallEventRunningServerRpc(false);
        }

        internal IEnumerator SpawnAndHandleGhostMimic(PlayerControllerB targetPlayer)
        {
            var ghostMimic = SpawnMimic(targetPlayer, Masked.transform.position);
            if (ghostMimic == null)
                yield break;

            ghostMimic.enabled = false;
            spawnedGhostMimics.Add(ghostMimic);

            yield return null;

            Ghostify(ghostMimic);

            RoundManager.Instance.FlickerLights(true, false);
            var mainParticle = ghostMimic.teleportParticle.main;
            mainParticle.simulationSpeed = 15f;
            ghostMimic.teleportParticle.Play();

            ghostMimic.maskEyesGlow[ghostMimic.maskTypeIndex].enabled = true;
            ghostMimic.maskEyesGlowLight.enabled = true;
            ghostMimic.maskEyesGlowLight.color = Color.blue;
            ghostMimic.maskEyesGlowLight.intensity *= 50f;

            yield return new WaitForSeconds(3.5f);

            int counter = 0;
            float timeChasing = 0f;
            while (timeChasing < MaximumGhostChaseTime && !targetPlayer.isPlayerDead)
            {
                if (ghostMimic.agent != null)
                    ghostMimic.agent.speed = 10f;

                counter++;
                if (counter > 10)
                {
                    counter = 0;
                    ghostMimic.SetDestinationToPosition(targetPlayer.transform.position);
                    ghostMimic.SetHandsOutServerRpc(true);
                }

                ghostMimic.CalculateAnimationDirection();

                if (Vector3.Distance(ghostMimic.transform.position, targetPlayer.transform.position) < 1f)
                {
                    targetPlayer.DamagePlayer(1, true, true, CauseOfDeath.Unknown, 1);
                    break;
                }

                timeChasing += Time.deltaTime;
                yield return null;
            }

            Item? giftBox = Utils.GiftBoxItem;
            if (giftBox != null && giftBox.spawnPrefab != null)
            {
                GiftBoxItem giftBoxItem = giftBox.spawnPrefab.GetComponent<GiftBoxItem>();
                var presentParticles = Instantiate(giftBoxItem.PoofParticle);
                presentParticles.transform.position = ghostMimic.transform.position;
                presentParticles.Play();
            }

            spawnedGhostMimics.Remove(ghostMimic);
            RoundManager.Instance.DespawnEnemyGameObject(ghostMimic.NetworkObject);
        }

        internal MaskedPlayerEnemy? SpawnMimic(PlayerControllerB imitatingPlayer, Vector3 position)
        {
            GameObject maskedObject = Instantiate(Masked.enemyType.enemyPrefab, position, Quaternion.Euler(new Vector3(0f, 0f, 0f)));
            maskedObject.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);

            LethalMon.Log("Spawned masked");
            MaskedPlayerEnemy maskedEnemy = maskedObject.GetComponent<MaskedPlayerEnemy>();
            maskedEnemy.SetEnemyOutside(Masked.isOutside);

            return maskedEnemy;
        }

        [ServerRpc(RequireOwnership = false)]
        internal void SetEscapeFromBallEventRunningServerRpc(bool running = true)
        {
            escapeFromBallEventRunning.Value = running;
        }

        internal override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            // ANY CLIENT
            base.OnUpdate(update, doAIInterval);

            Masked.CalculateAnimationDirection();
        }

        internal override void DoAIInterval()
        {
            // ANY CLIENT, every EnemyAI.updateDestinationInterval, if OnUpdate.doAIInterval = true
            base.DoAIInterval();

            if (isEscapedDEBUG) return;

            if (ownerPlayer != null && maskTransferCoroutine == null)
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

        internal override void OnCallFromBall()
        {
            base.OnCallFromBall();

            Masked.EnableEnemyMesh(true);
        }

        internal override void TurnTowardsPosition(Vector3 position)
        {
            // Masked.LookAtPosition(position); // avoid the log putput
            Masked.focusOnPosition = position;
            Masked.lookAtPositionTimer = 1f;
            float num = Vector3.Angle(Masked.transform.forward, position - Masked.transform.position);
            if (position.y - Masked.headTiltTarget.position.y < 0f)
            {
                num *= -1f;
            }
            Masked.verticalLookAngle = num;
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
            
            Masked.SetHandsOutServerRpc(true);

            yield return StartCoroutine(FaceOwner());

            if (IsOwner)
                Masked.agent.speed = 0f;

            yield return new WaitForSeconds(0.3f);
            SetMaskGlowNoSound();

            yield return StartCoroutine(RotateMaskOnPlayerFace());

            SetMaskShaking(false);

            if(IsOwnerPlayer)
                SetMaskGlassified();

            Masked.FinishKillAnimation();

            if(IsOwnerPlayer)
                CustomPassManager.Instance.EnableCustomPass(CustomPassManager.CustomPassType.SeeThroughEnemies, true);
            else
                SetMaskGlowNoSound();

            if(IsOwner)
                Masked.agent.speed = 5f;

            isWearingMask = true;
            SetRedVision();

            Masked.SetHandsOutServerRpc(true);
            maskTransferCoroutine = null;

            yield return null;

            if (IsOwnerPlayer)
                SwitchToCustomBehaviour((int)CustomBehaviour.LendMask);
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
            Masked.SetHandsOutServerRpc(true);

            yield return StartCoroutine(FaceOwner());

            if (IsOwner)
                Masked.agent.speed = 0f;

            SetMaskShaking();
            if (IsOwnerPlayer)
                SetMaskGlassified(false);

            yield return new WaitForSeconds(0.3f);

            SetMaskGlowNoSound();

            yield return StartCoroutine(RotateMaskOnMaskedFace());

            Masked.FinishKillAnimation();

            if (IsOwner)
                Masked.agent.speed = 5f;

            isWearingMask = false;
            SetRedVision(false);

            if(IsOwnerPlayer)
                CustomPassManager.Instance.EnableCustomPass(CustomPassManager.CustomPassType.SeeThroughEnemies, false);

            Masked.SetHandsOutServerRpc(false);

            maskTransferCoroutine = null;
            lendMaskCooldown.Reset();

            yield return null;

            if (IsOwnerPlayer)
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
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

            LethalMon.Log("RotateMaskOnMaskedFace", LethalMon.LogType.Warning);

            Vector3 endPosition;
            Quaternion endRotation;
            if (originalMaskParent != null)
            {
                endPosition = originalMaskParent.transform.position + Vector3.up * originalMaskLocalPosition.y + Masked.transform.forward * originalMaskLocalPosition.z; // todo: find better way
                endRotation = originalMaskParent.transform.rotation;
            }
            else
            {
                endPosition = Masked.transform.position + Vector3.up * 2.3f + Masked.transform.forward * 0.15f;
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
            if (MaskAnimator != null)
                MaskAnimator.speed = shaking ? 1f : 0f; // 1f is default.. unsure why it's constantly shaking by default
        }

        void SetRedVision(bool enable = true)
        {
            if (ownerPlayer == null || ownerPlayer.nightVision.enabled == enable) return;

            if(enable)
            {
                originalNightVisionColor = ownerPlayer.nightVision.color;
                ownerPlayer.nightVision.color = Color.red;
                originalNightVisionIntensity = ownerPlayer.nightVision.intensity;
                ownerPlayer.nightVision.intensity /= 1.5f;
            }
            else
            {
                ownerPlayer.nightVision.color = originalNightVisionColor.GetValueOrDefault(Color.white);
                ownerPlayer.nightVision.intensity = originalNightVisionIntensity;
            }

            ownerPlayer.nightVision.enabled = enable;
        }

        private void Ghostify(MaskedPlayerEnemy maskedEnemy)
        {
            maskedEnemy.rendererLOD0.materials = Enumerable.Repeat(false, Masked.rendererLOD0.materials.Length).Select(x => new Material(Utils.Glass)).ToArray();
            maskedEnemy.rendererLOD1.materials = Enumerable.Repeat(false, Masked.rendererLOD1.materials.Length).Select(x => new Material(Utils.Glass)).ToArray();
            maskedEnemy.rendererLOD2.materials = Enumerable.Repeat(false, Masked.rendererLOD2.materials.Length).Select(x => new Material(Utils.Glass)).ToArray();
            Utils.ReplaceAllMaterialsWith(maskedEnemy.gameObject, (Material _) => Utils.Glass);

            var mask = maskedEnemy.maskTypes[maskedEnemy.maskTypeIndex];
            if (mask == null) return;

            if(!mask.gameObject.TryGetComponent(out Light light))
                light = mask.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 10f;
            light.intensity = 10f;
            light.enabled = true;
            light.color = Color.blue;
        }
        #endregion

        [ServerRpc(RequireOwnership = false)] // Required reroute as the original RPC is prefix-patched by Mirage
        public void HandsOutRerouteServerRpc(NetworkObjectReference maskedRef, bool handsOut)
        {
            HandsOutRerouteClientRpc(maskedRef, handsOut);
        }

        [ClientRpc]
        public void HandsOutRerouteClientRpc(NetworkObjectReference maskedRef, bool handsOut)
        {
            if(!maskedRef.TryGet(out NetworkObject networkObject) || !networkObject.TryGetComponent(out MaskedPlayerEnemy maskedEnemy))
            {
                LethalMon.Log("HandsOutRerouteClientRpc: Unable to get masked enemy from reference.", LethalMon.LogType.Error);
                return;
            }

            maskedEnemy.creatureAnimator.SetBool("HandsOut", handsOut);
            maskedEnemy.handsOut = handsOut;
        }
    }
#endif
}
