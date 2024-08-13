using GameNetcodeStuff;
using System.Collections.Generic;
using System;
using LethalMon.Items;
using UnityEngine;
using System.Collections;
using LethalMon.CustomPasses;
using Unity.Netcode;
using System.Linq;
using LethalMon.Patches;
using LethalLib.Modules;
using ModelReplacement;
using LethalMon.Compatibility;

namespace LethalMon.Behaviours
{
    internal class MaskedTamedBehaviour : TamedEnemyBehaviour
    {
        #region Static Properties
        static readonly float MaskedNormalSpeed = 3.5f;
        static readonly float MaskedRunningSpeed = 7f;

        static readonly float MaximumMaskWearingTime = 10f;

        static readonly float EscapeEventLightIntensity = 75f;

        static readonly float MaximumGhostLifeTime = 15f;
        static readonly float GhostChaseSpeed = 7f;
        static readonly float GhostZoomUntilDistance = 25f;

        static readonly float GhostAudioToggleDistance = 15f;
        internal readonly Tuple<float, float> GhostVoiceIntervalRange = new Tuple<float, float>(6f, 9f);
        internal bool ghostAnimationInitialized = false;
        #endregion

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

        float timeWearingMask = 0f;

        Coroutine? maskTransferCoroutine = null;

        GameObject? Mask => Masked?.maskTypes[Masked.maskTypeIndex];
        Animator? _maskAnimator = null;
        Animator? MaskAnimator
        {
            get
            {
                if (_maskAnimator == null)
                    _maskAnimator = Mask?.GetComponent<Animator>();

                return _maskAnimator;
            }
        }

        Color? originalNightVisionColor = null;
        float originalNightVisionIntensity = 366f;

        public bool escapeFromBallEventRunning = false;

        // Ghosts
        internal bool isGhostified = false;
        internal float ghostLifetime = 0f;
        internal bool IsGhostAboutToDie => (MaximumGhostLifeTime - ghostLifetime) < 2f;
        internal float GhostSpawnTime => 3.5f + 1f - (GhostChaseSpeed / 3f);
        List<MaskedPlayerEnemy> spawnedGhostMimics = new List<MaskedPlayerEnemy>();
        MaskedTamedBehaviour? parentMimic = null;
        float glitchCooldown = 0f;

        private Material? _ghostMaterial = null;
        internal Material GhostMaterial
        {
            get
            {
                if (_ghostMaterial == null)
                {
                    _ghostMaterial = new Material(Utils.WireframeMaterial);
                    _ghostMaterial.SetColor("_EdgeColor", new Color(0.8f, 0.9f, 1f, 0.15f));
                    _ghostMaterial.SetFloat("_WireframeVal", 1f);
                    _ghostMaterial.SetFloat("_MaxVisibilityDistance", 15f);
                }

                return _ghostMaterial;
            }
        }

        private Material? _ghostEyesMaterial = null;
        internal Material GhostEyesMaterial
        {
            get
            {
                if (_ghostEyesMaterial == null)
                {
                    _ghostEyesMaterial = new Material(Shader.Find("HDRP/Unlit"));
                    _ghostEyesMaterial.color = new Color(0.8f, 0.9f, 1f);
                }

                return _ghostEyesMaterial;
            }
        }

        // Audio
        internal static AudioClip? ghostAmbientSFX = null, ghostHissSFX = null, ghostHissFastSFX = null, ghostPoofSFX = null;

        // Audio (Far)
        internal static AudioClip? ghostAmbientFarSFX = null;

        internal static List<Tuple<AudioClip, AudioClip>> GhostVoices = new List<Tuple<AudioClip, AudioClip>>();
        internal float timeTillNextGhostVoice = 0f;

        internal AudioSource? farAudio = null;
        #endregion

        #region Cooldowns
        private static readonly string CooldownId = "masked_lendmask";

        internal override Cooldown[] Cooldowns => new[] { new Cooldown(CooldownId, "Lending mask", 2f) };

        private CooldownNetworkBehaviour lendMaskCooldown;
        #endregion

        #region Custom behaviours
        internal enum CustomBehaviour
        {
            LendMask = 1,
            Ghostified
        }
        internal override List<Tuple<string, string, Action>>? CustomBehaviourHandler => new()
        {
            new (CustomBehaviour.LendMask.ToString(), "Lending mask", OnLendMaskBehavior),
            new (CustomBehaviour.Ghostified.ToString(), "Ghostified", OnGhostBehavior)
        };

        internal override void InitCustomBehaviour(int behaviour)
        {
            // OWNER ONLY
            base.InitCustomBehaviour(behaviour);

            switch ((CustomBehaviour)behaviour)
            {
                case CustomBehaviour.LendMask:
                    SetMaskShaking(false);
                    break;

                case CustomBehaviour.Ghostified:
                    Masked.updateDestinationInterval = 0.1f;
                    ghostLifetime = 0f;

                    if (targetPlayer == null) return;

                    GhostAppearedServerRpc();
                    Masked.SetMovingTowardsTargetPlayer(targetPlayer);

                    var pitch = UnityEngine.Random.Range(0.75f, 1.25f);

                    Masked.movementAudio.clip = ghostAmbientSFX;
                    Masked.movementAudio.volume = 0.5f;
                    Masked.movementAudio.Play();
                    Masked.movementAudio.pitch = pitch;

                    if (farAudio != null)
                    {
                        farAudio.clip = ghostAmbientFarSFX;
                        farAudio.volume = 0.5f;
                        farAudio.Play();
                        farAudio.pitch = pitch;
                    }

                    RoundManager.Instance.tempTransform.position = Masked.transform.position;
                    RoundManager.Instance.tempTransform.LookAt(targetPlayer.transform);
                    Masked.transform.rotation = RoundManager.Instance.tempTransform.rotation;
                    break;

                default:
                    break;
            }
        }

        internal void OnLendMaskBehavior()
        {
            if (!isWearingMask || maskTransferCoroutine != null) return;

            if (IsOwnerPlayer)
            {
                StartOfRound.Instance.fearLevel += Time.deltaTime / 10f;

                timeWearingMask += Time.deltaTime;
                if (MaskAnimator != null && timeWearingMask > (MaximumMaskWearingTime / 1.5f))
                    MaskAnimator.speed += Time.deltaTime / (MaximumMaskWearingTime / 4f);

                if (timeWearingMask > MaximumMaskWearingTime)
                {
                    ownerPlayer!.DamagePlayer(1, true, true, CauseOfDeath.Unknown, 8);

                    if (!ownerPlayer.isPlayerDead)
                        GiveBackMaskServerRpc();
                }
            }

            if (IsOwner)
                FollowOwner();
        }

        internal void OnGhostBehavior()
        {
            ghostLifetime += Time.deltaTime; // Spawn animation
            if (ghostLifetime < GhostSpawnTime) return;

            if (targetPlayer == null || targetPlayer.isPlayerDead || ghostLifetime > MaximumGhostLifeTime)
            {
                LethalMon.Log("Player dead or ghost lifetime reached. Despawn ghost.");
                GhostDisappearsServerRpc();
                return;
            }

            if (Vector3.Distance(targetPlayer.transform.position, Masked.transform.position) > GhostZoomUntilDistance)
                Masked.agent!.speed = 100f; // zooming!
            else
                Masked.agent!.speed = GhostChaseSpeed;

            Masked.CalculateAnimationDirection();
            Masked.LookAtFocusedPosition();

            if (IsOwner)
            {
                glitchCooldown -= Time.deltaTime;
                if (glitchCooldown <= 0f)
                {
                    if (!IsGhostAboutToDie)
                        GhostGlitchAnimationServerRpc();
                    glitchCooldown = UnityEngine.Random.Range(2f, 4f);
                }

                timeTillNextGhostVoice -= Time.deltaTime;
                if (timeTillNextGhostVoice <= 0f)
                {
                    timeTillNextGhostVoice = UnityEngine.Random.Range(GhostVoiceIntervalRange.Item1, GhostVoiceIntervalRange.Item2);
                    if (!IsGhostAboutToDie)
                        PlayRandomGhostVoiceServerRpc();
                }
            }
        }

        internal static void LoadGhostAudio(AssetBundle assetBundle)
        {
            ghostAmbientSFX = assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostAmbient.ogg");
            ghostAmbientFarSFX = assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostAmbientFar.ogg");
            ghostHissSFX = assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostHiss.ogg");
            ghostHissFastSFX = assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostHissFast.ogg");
            ghostPoofSFX = assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostPoof.ogg");

            var ghostLaughSFX = assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostLaugh.ogg");
            var ghostLaughFarSFX = assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostLaughFar.ogg");
            if (ghostLaughSFX != null && ghostLaughFarSFX != null)
                GhostVoices.Add(new Tuple<AudioClip, AudioClip>(ghostLaughSFX, ghostLaughFarSFX));

            var ghostLaugh2SFX = assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostLaugh2.ogg");
            var ghostLaugh2FarSFX = assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostLaugh2Far.ogg");
            if (ghostLaugh2SFX != null && ghostLaugh2FarSFX != null)
                GhostVoices.Add(new Tuple<AudioClip, AudioClip>(ghostLaugh2SFX, ghostLaugh2FarSFX));

            var ghostCrySFX = assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostCry.ogg");
            var ghostCryFarSFX = assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostCryFar.ogg");
            if (ghostCrySFX != null && ghostCryFarSFX != null)
                GhostVoices.Add(new Tuple<AudioClip, AudioClip>(ghostCrySFX, ghostCryFarSFX));

            var ghostCry2SFX = assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostCry2.ogg");
            var ghostCry2SFarFX = assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostCry2Far.ogg");
            if (ghostCry2SFX != null && ghostCry2SFarFX != null)
                GhostVoices.Add(new Tuple<AudioClip, AudioClip>(ghostCry2SFX, ghostCry2SFarFX));
        }

        [ServerRpc(RequireOwnership = false)]
        internal void PlayRandomGhostVoiceServerRpc()
        {
            LethalMon.Log("PlayRandomGhostVoiceServerRpc. Voices: " + GhostVoices.Count);
            if (GhostVoices.Count == 0) return;

            var index = UnityEngine.Random.RandomRangeInt(0, GhostVoices.Count - 1);
            PlayRandomGhostVoiceClientRpc(index);
        }

        [ClientRpc]
        internal void PlayRandomGhostVoiceClientRpc(int index)
        {
            if (GhostVoices.Count == 0) return;

            if (index >= GhostVoices.Count)
                index = GhostVoices.Count - 1;

            var usingFarSFX = Vector3.Distance(Masked.transform.position, Utils.CurrentPlayer.transform.position) > GhostAudioToggleDistance;
            LethalMon.Log("PlayRandomGhostVoiceClientRpc. Play voice: " + index);
            Masked.creatureVoice.PlayOneShot(usingFarSFX ? GhostVoices[index].Item2 : GhostVoices[index].Item1);
        }

        [ServerRpc(RequireOwnership = false)] // Required reroute as the original RPC is prefix-patched by Mirage
        public void GhostGlitchAnimationServerRpc()
        {
            GhostGlitchAnimationClientRpc(UnityEngine.Random.Range(1f, 1.5f));
        }

        [ClientRpc]
        public void GhostGlitchAnimationClientRpc(float duration)
        {
            /*
             IsMoving / Bool
             VelocityZ / Float
             VelocityX / Float
             Running / Bool
             HandsOut / Bool
             killing / Bool
             Dead / Bool
             Crouching / Bool
             HitEnemy / Trigger
             Stunned / Bool
             */

            StartCoroutine(GlitchAnimation(duration));
        }

        public IEnumerator GlitchAnimation(float duration)
        {
            Masked.creatureAnimator.speed *= 2f;
            yield return new WaitForSeconds(0.1f);
            Masked.creatureAnimator.SetBool("Stunned", true);
            yield return new WaitForSeconds(duration);
            Masked.creatureAnimator.SetBool("Stunned", false);
            yield return new WaitForSeconds(0.1f);
            Masked.creatureAnimator.speed /= 2f;
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

            if (isWearingMask)
            {
                GiveBackMaskServerRpc();
                return;
            }

            if (lendMaskCooldown.IsFinished())
                LendMaskServerRpc();
        }
        #endregion

        #region Base Methods
        internal override void Awake()
        {
            base.Awake();
            MirageCompatibility.SaveHeadMasksOf(Masked.gameObject);
        }

        internal override void Start()
        {
            base.Start();

            lendMaskCooldown = GetCooldownWithId(CooldownId);

            if (Mask != null)
            {
                originalMaskParent = Mask.transform.parent;
                originalMaskLocalPosition = Mask.transform.localPosition;
            }

            if (ownerPlayer != null) // Baye Layers: Roam, Follow, Wait
                Masked.creatureAnimator.Play("Base Layer.Wait"); // wip, not working yet

            //StartCoroutine(SetOwnerDEBUG());
        }

        internal IEnumerator SetOwnerDEBUG()
        {
            ownerPlayer = Utils.AlivePlayers.Where((p) => p.playerClientId == 0ul).First();
            ownClientId = 0ul;
            Masked.EnableEnemyMesh(true);
            Masked.enabled = false;

            if (IsOwner)
            {
                yield return new WaitForSeconds(0.3f);
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
            }
        }

        void OnDestroy() => CleanUp();

        void OnDisable() => CleanUp();

        internal void CleanUp()
        {
            if (parentMimic != null)
                parentMimic.spawnedGhostMimics.Remove(Masked);

            StopAllCoroutines();
            for (int i = spawnedGhostMimics.Count - 1; i >= 0; i--)
            {
                var ghostMimic = spawnedGhostMimics[i];
                if (ghostMimic == null || !ghostMimic.IsSpawned) continue;

                RoundManager.Instance.DespawnEnemyGameObject(ghostMimic.NetworkObject);
                DestroyImmediate(ghostMimic);
            }
            spawnedGhostMimics.Clear();

            MaskedPlayerEnemyPatch.lastColliderIDs.Remove(Masked.GetInstanceID());
        }

        internal override void InitTamingBehaviour(TamingBehaviour behaviour)
        {
            // ANY CLIENT
            base.InitTamingBehaviour(behaviour);

            switch (behaviour)
            {
                case TamingBehaviour.TamedFollowing:
                    EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true);
                    SetMaskShaking(false);
                    break;

                case TamingBehaviour.TamedDefending:
                    break;

                default: break;
            }
        }

        // OnTamedDefending idea: decoy

        internal override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            // ANY CLIENT
            if (maskTransferCoroutine != null) return;

            base.OnUpdate(update, doAIInterval);

            if (!isGhostified)
                Masked.CalculateAnimationDirection();
        }

        internal override void DoAIInterval()
        {
            // ANY CLIENT, every EnemyAI.updateDestinationInterval, if OnUpdate.doAIInterval = true
            base.DoAIInterval();

            if (ownerPlayer == null || isGhostified) return;

            if (maskTransferCoroutine == null)
            {
                var shouldRun = Vector3.Distance(Masked.transform.position, ownerPlayer.transform.position) > 8f;
                if (shouldRun != Masked.running)
                {
                    Masked.running = shouldRun;
                    Masked.creatureAnimator.SetBool("Running", shouldRun);
                    if (IsOwner)
                        Masked.agent.speed = Masked.running ? MaskedRunningSpeed : MaskedNormalSpeed;
                }
            }
            else
            {
                if (Masked.running)
                    Masked.creatureAnimator.SetBool("Running", false);
                Masked.running = false;
                if (IsOwner)
                    Masked.agent.speed = 0f;
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

            if (ownerPlayer != null)
            {
                Masked.stareAtTransform = ownerPlayer.gameplayCamera.transform;
                Masked.lookAtPositionTimer = 0f;
            }
        }

        internal override void TurnTowardsPosition(Vector3 position)
        {
            Masked.lookAtPositionTimer = 0f;
            if (Masked.agent != null)
                Masked.LookAtFocusedPosition();
        }
        #endregion

        #region EscapedFromBall - GhostEvent
        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            // ANY CLIENT
            base.OnEscapedFromBall(playerWhoThrewBall);
            LethalMon.Log("OnEscapedFromBall");

            Masked.inSpecialAnimationWithPlayer = playerWhoThrewBall;
            targetPlayer = playerWhoThrewBall;
            StartCoroutine(MaskedEscapeFromBallCoroutine(playerWhoThrewBall));
        }

        internal IEnumerator MaskedEscapeFromBallCoroutine(PlayerControllerB playerWhoThrewBall)
        {
            escapeFromBallEventRunning = true;

            LethalMon.Log("MaskedEscapeFromBallCoroutine");

            Masked.enabled = false;
            if (IsOwner)
                Masked.agent.enabled = false;

            if (targetPlayer != null)
            {
                RoundManager.Instance.tempTransform.position = Masked.transform.position;
                RoundManager.Instance.tempTransform.LookAt(targetPlayer.transform);
                Masked.transform.rotation = RoundManager.Instance.tempTransform.rotation;
            }

            bool maskEnabled = MirageCompatibility.IsMaskEnabled(Masked.gameObject);
            if (!maskEnabled)
            {
                // Mask disabled by another mod (e.g. Mirage)
                LethalMon.Log("Mask disabled.");
                MirageCompatibility.ShowMaskOf(Masked.gameObject);

                if (Mask != null)
                {
                    LethalMon.Log("Disable mask renderer");
                    foreach (var mr in Utils.GetRenderers(Mask))
                        mr.enabled = false;
                }
            }

            yield return new WaitForSeconds(0.5f);

            SetMaskGlowNoSound(true);

            yield return new WaitForSeconds(0.5f);

            float timeGlowingUp = 0f; // Fallback
            float startIntensity = Masked.maskEyesGlowLight.intensity;
            LethalMon.Log("StartIntensity: " + startIntensity);
            float finalIntensity = startIntensity * EscapeEventLightIntensity;
            while (Masked.maskEyesGlowLight.intensity < finalIntensity && timeGlowingUp < 2f)
            {
                Masked.maskEyesGlowLight.intensity += Time.deltaTime * EscapeEventLightIntensity;
                timeGlowingUp += Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.2f);

            if (IsOwner)
                StartCoroutine(SpawnGhostMimic());
            Masked.maskEyesGlowLight.intensity /= 1.5f;

            yield return new WaitForSeconds(6f);

            if (IsOwner)
                StartCoroutine(SpawnGhostMimic());
            Masked.maskEyesGlowLight.intensity = startIntensity;

            if (!maskEnabled)
                MirageCompatibility.ShowMaskOf(Masked.gameObject, false); // return to previous state

            if (!IsOwner) yield break;

            // Owner only from here on
            float triggerFallbackAfter = MaximumGhostLifeTime + 1f;
            float fallbackTimer = 0f;
            yield return new WaitWhile(() =>
            {
                fallbackTimer += Time.deltaTime;
                return spawnedGhostMimics.Count > 0 && fallbackTimer < triggerFallbackAfter;
            });

            if (fallbackTimer >= triggerFallbackAfter)
                LethalMon.Log("Fallback triggered on MaskedEscapeFromBallCoroutine.", LethalMon.LogType.Warning);

            CleanUp();
            if (Masked.agent != null)
                Masked.agent.enabled = true;
            Masked.enabled = true;

            EscapeFromBallEventEndedServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        public void EscapeFromBallEventEndedServerRpc()
        {
            EscapeFromBallEventEndedClientRpc();
        }

        [ClientRpc]
        public void EscapeFromBallEventEndedClientRpc()
        {
            SetMaskGlowNoSound(false);

            if (MirageCompatibility.Enabled)
                MirageCompatibility.ShowMaskOf(Masked.gameObject, false);

            escapeFromBallEventRunning = false;
            Masked.inSpecialAnimationWithPlayer = null;
            targetPlayer = null;
        }

        internal IEnumerator SpawnGhostMimic()
        {
            var ghostMimic = SpawnMimic(Masked.transform.position);
            if (ghostMimic == null)
                yield break;

            if (!ghostMimic.TryGetComponent(out MaskedTamedBehaviour tamedBehaviour))
            {
                LethalMon.Log("Ghost mimic has no tamed behaviour handler.", LethalMon.LogType.Error);
                yield break;
            }

            spawnedGhostMimics.Add(ghostMimic);

            yield return null; // Call Start() once before switching

            tamedBehaviour.Masked.enabled = false;

            if (!MirageCompatibility.IsMaskEnabled(tamedBehaviour.Masked.gameObject)) // Mask disabled by another mod (e.g. Mirage)
                MirageCompatibility.ShowMaskOf(tamedBehaviour.Masked.gameObject);

            Masked.maskEyesGlow[Masked.maskTypeIndex].enabled = true;
            Masked.maskEyesGlowLight.enabled = true;
            LethalMon.Log("Intensity: " + Masked.maskEyesGlowLight.intensity);

            tamedBehaviour.parentMimic = this;
            if (targetPlayer != null)
                tamedBehaviour.SyncGhostTargetServerRpc(targetPlayer.playerClientId);
            tamedBehaviour.SwitchToCustomBehaviour((int)CustomBehaviour.Ghostified);
        }

        [ServerRpc]
        public void SyncGhostTargetServerRpc(ulong playerID) => SyncGhostTargetClientRpc(playerID);

        [ClientRpc]
        public void SyncGhostTargetClientRpc(ulong playerID)
        {
            targetPlayer = Utils.AllPlayers.Where((p) => p.playerClientId == playerID).First();
            Masked.stareAtTransform = targetPlayer.transform;
            Masked.lookAtPositionTimer = 0f;
        }

        [ServerRpc]
        public void GhostHitPlayerServerRpc(ulong playerID)
        {
            if (targetPlayer != null && targetPlayer.playerClientId == playerID)
            {
                GhostHitTargetPlayerClientRpc();
                GhostDisappearsClientRpc();
            }
            else
            {
                GhostHitNonTargetPlayerClientRpc(playerID);
            }
        }

        [ClientRpc]
        public void GhostHitTargetPlayerClientRpc()
        {
            if (targetPlayer == Utils.CurrentPlayer)
                targetPlayer.DamagePlayer(2, true, true, CauseOfDeath.Unknown, 1);
        }

        [ClientRpc]
        public void GhostHitNonTargetPlayerClientRpc(ulong playerID)
        {
            if (playerID == Utils.CurrentPlayerID)
            {
                if (Masked.agent.speed > 50f && ghostHissFastSFX != null)
                    Utils.PlaySoundAtPosition(Utils.CurrentPlayer.transform.position, ghostHissFastSFX);
                else if (ghostHissSFX != null)
                    Utils.PlaySoundAtPosition(Utils.CurrentPlayer.transform.position, ghostHissSFX);
            }
        }

        [ServerRpc]
        public void GhostAppearedServerRpc()
        {
            GhostAppearedClientRpc();
        }

        [ClientRpc]
        public void GhostAppearedClientRpc()
        {
            Ghostify(Masked);

            var mainParticle = Masked.teleportParticle.main;
            mainParticle.simulationSpeed = 20f;
            Masked.teleportParticle.Play();

            if (Vector3.Distance(Masked.transform.position, Utils.CurrentPlayer.transform.position) > 20f)
                RoundManager.Instance.FlickerLights(true, false);

            Masked.creatureAnimator.SetFloat("VelocityY", 1f);
            Masked.creatureAnimator.speed *= GhostChaseSpeed / 5f;
        }

        [ServerRpc]
        public void GhostDisappearsServerRpc()
        {
            GhostDisappearsClientRpc();
        }

        [ClientRpc]
        public void GhostDisappearsClientRpc()
        {
            Utils.SpawnPoofCloudAt(Masked.transform.position + Vector3.up * 1.5f);

            if (ghostPoofSFX != null)
                Utils.PlaySoundAtPosition(Masked.transform.position, ghostPoofSFX);

            if (IsOwner)
            {
                RoundManager.Instance.DespawnEnemyGameObject(Masked.NetworkObject);
                Destroy(Masked, 0.5f);
            }
        }

        internal MaskedPlayerEnemy? SpawnMimic(Vector3 position)
        {
            GameObject maskedObject = Instantiate(Masked.enemyType.enemyPrefab, position, Quaternion.Euler(new Vector3(0f, 0f, 0f)));
            maskedObject.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);

            MaskedPlayerEnemy maskedEnemy = maskedObject.GetComponent<MaskedPlayerEnemy>();
            maskedEnemy.SetEnemyOutside(Masked.isOutside);

            return maskedEnemy;
        }

        private void Ghostify(MaskedPlayerEnemy maskedEnemy)
        {
            // Ghostify masked body
            maskedEnemy.rendererLOD0.materials = Enumerable.Repeat(false, Masked.rendererLOD0.materials.Length).Select(x => new Material(GhostMaterial)).ToArray();
            maskedEnemy.rendererLOD1.materials = Enumerable.Repeat(false, Masked.rendererLOD1.materials.Length).Select(x => new Material(GhostMaterial)).ToArray();
            maskedEnemy.rendererLOD2.materials = Enumerable.Repeat(false, Masked.rendererLOD2.materials.Length).Select(x => new Material(GhostMaterial)).ToArray();

            // Remove unmodified badges
            var spineTransform = maskedEnemy.transform.Find("ScavengerModel/metarig/spine/spine.001/spine.002/spine.003");
            if (spineTransform != null)
            {
                spineTransform.Find("LevelSticker")?.gameObject.SetActive(false);
                spineTransform.Find("BetaBadge")?.gameObject.SetActive(false);
            }

            var eyeRenderer = maskedEnemy.maskEyesGlow[maskedEnemy.maskTypeIndex];
            if (ModelReplacementAPICompatibility.Enabled)
            {
                var model = ModelReplacementAPICompatibility.FindCurrentReplacementModelIn(Masked.gameObject, isEnemy: true);
                if (model != null)
                    Utils.CallNextFrame(() => Utils.ReplaceAllMaterialsWith(model, (_) => new Material(GhostMaterial)));
                eyeRenderer.enabled = false;
            }
            else
            {
                // Change eye color to blue-white
                eyeRenderer.material = new Material(GhostEyesMaterial);
                eyeRenderer.enabled = true;
            }

            var mask = Mask;
            if (mask != null)
            {
                // Set mask invisible and stable
                foreach (var mr in Utils.GetRenderers(mask))
                    mr.enabled = false;
                mask.GetComponent<Animator>().speed = 0f;

                // Add light
                if (!mask.gameObject.TryGetComponent(out Light light))
                    light = mask.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 5f;
                light.intensity = 3f;
                light.color = new Color(0.8f, 0.9f, 1f);
                light.enabled = true;
            }

            // Add farAudio
            farAudio = Masked.gameObject.AddComponent<AudioSource>();
            farAudio.maxDistance = GhostAudioToggleDistance * 5f;
            farAudio.minDistance = GhostAudioToggleDistance;
            farAudio.rolloffMode = AudioRolloffMode.Linear;
            farAudio.spatialBlend = 1f; // default 0
            farAudio.priority = 127; // default 128

            Masked.movementAudio.maxDistance = GhostAudioToggleDistance * 1.5f;
            Masked.movementAudio.rolloffMode = AudioRolloffMode.Linear;

            Utilities.FixMixerGroups(Masked.gameObject);

            isGhostified = true;
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
            if (IsOwnerPlayer)
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, false);
        }

        IEnumerator LendMaskCoroutine()
        {
            if (ownerPlayer == null) yield break;

            Masked.inSpecialAnimationWithPlayer = ownerPlayer;

            Masked.SetHandsOutClientRpc(true);

            yield return StartCoroutine(FaceOwner());

            yield return new WaitForSeconds(0.3f);

            SetMaskGlowNoSound();

            yield return StartCoroutine(RotateMaskOnPlayerFace());

            SetMaskShaking(false);

            if (IsOwnerPlayer)
                SetMaskGlassified();

            Masked.FinishKillAnimation();

            if (IsOwnerPlayer)
            {
                SetRedVision();
                CustomPassManager.Instance.EnableCustomPass(CustomPassManager.CustomPassType.SeeThroughEnemies, true);
            }
            else
                SetMaskGlowNoSound();

            isWearingMask = true;

            Masked.SetHandsOutClientRpc(false);

            maskTransferCoroutine = null;

            Masked.inSpecialAnimationWithPlayer = null;

            yield return null;

            if (IsOwner)
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
            if (ownerPlayer == null) yield break;

            Masked.inSpecialAnimationWithPlayer = ownerPlayer;

            Masked.SetHandsOutServerRpc(true);

            yield return StartCoroutine(FaceOwner());

            SetMaskShaking();
            if (IsOwnerPlayer)
                SetMaskGlassified(false);

            yield return new WaitForSeconds(0.3f);

            SetMaskGlowNoSound();

            if (IsOwnerPlayer)
            {
                SetRedVision(false);
                CustomPassManager.Instance.EnableCustomPass(CustomPassManager.CustomPassType.SeeThroughEnemies, false);
            }

            yield return StartCoroutine(RotateMaskOnMaskedFace());

            Masked.FinishKillAnimation();

            isWearingMask = false;
            timeWearingMask = 0f;

            SetMaskGlowNoSound(false);

            Masked.SetHandsOutServerRpc(false);

            maskTransferCoroutine = null;
            Masked.inSpecialAnimationWithPlayer = null;

            lendMaskCooldown.Reset();

            yield return null;

            if (IsOwner)
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
            if (Masked.agent != null)
                Masked.LookAtFocusedPosition();
        }

        void SetMaskGlowNoSound(bool enable = true) // SetMaskGlow without sound
        {
            LethalMon.Log("SetMaskGlowNoSound: " + enable);
            Masked.maskEyesGlow[Masked.maskTypeIndex].enabled = enable;
            Masked.maskEyesGlowLight.enabled = enable;
        }

        void SetMaskGlassified(bool glassified = true)
        {
            var currentMask = Mask;
            if (currentMask == null) return;

            var mr = currentMask.transform.Find("Mesh").GetComponent<MeshRenderer>();
            if (mr == null) return;

            if (glassified)
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
                foreach (var m in mr.materials)
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
            if (ownerPlayer == null) return;

            if (enable)
            {
                originalNightVisionColor = ownerPlayer.nightVision.color;
                ownerPlayer.nightVision.color = new Color(1f, 0.42f, 0f);
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
        #endregion
    }
}
