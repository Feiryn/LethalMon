using GameNetcodeStuff;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Collections;
using LethalMon.CustomPasses;
using Unity.Netcode;
using System.Linq;
using System.Reflection;
using LethalMon.Patches;
using LethalLib.Modules;
using LethalMon.Compatibility;

namespace LethalMon.Behaviours
{
    internal class MaskedTamedBehaviour : TamedEnemyBehaviour
    {
        #region Static Properties
        private const float MaskedNormalSpeed = 3.5f;
        private const float MaskedRunningSpeed = 7f;
        
        private const float MaximumMaskWearingTime = 10f;
        
        private const float EscapeEventLightIntensity = 75f;
        
        private const float MaximumGhostLifeTime = 15f;
        private const float GhostChaseSpeed = 4f;
        private const float GhostZoomUntilDistance = 25f;
        private const float GhostGlitchMaxDuration = 1.5f;
        
        private const float GhostAudioToggleDistance = 15f;
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

        internal override bool CanDefend => false;

        // Mask
        private bool _isTransferingMask = false;
        private Material[] _originalMaskMaterials = [];
        private Transform? _originalMaskParent = null;
        private Vector3 _originalMaskLocalPosition = new(-0.01f, 0.14f, 0.22f);
        private float _timeWearingMask = 0f;
        private bool _isWearingMask = false;

        internal GameObject? Mask => Masked?.maskTypes[Masked.maskTypeIndex];

        private Animator? _maskAnimator = null;
        internal Animator? MaskAnimator
        {
            get
            {
                if (_maskAnimator == null)
                    _maskAnimator = Mask?.GetComponent<Animator>();

                return _maskAnimator;
            }
        }

        // Night vision
        private Color? _originalNightVisionColor = null;
        private float _originalNightVisionIntensity = 366f;

        internal bool escapeFromBallEventRunning = false;

        // Ghosts
        private bool isGhostified = false;
        private float ghostLifetime = 0f;
        private static float GhostSpawnTime => 3.5f + 1f - (GhostChaseSpeed / 3f);
        private float GhostTimeToLive => MaximumGhostLifeTime - ghostLifetime;

        private readonly List<MaskedPlayerEnemy> _spawnedGhostMimics = [];
        private MaskedTamedBehaviour? _parentMimic = null;

        private float _aiIntervalTimeBackup = 0f;

        // Audio
        internal static List<Tuple<AudioClip, AudioClip>> GhostVoices = [];
        internal static AudioClip? GhostAmbientSFX = null, GhostHissSFX = null, GhostHissFastSFX = null, GhostPoofSFX = null;

        // Audio (Far)
        private AudioSource? _farAudio = null;
        internal static AudioClip? GhostAmbientFarSFX = null;

        #endregion

        #region Cooldowns
        private const string CooldownId = "masked_lendmask";

        internal override Cooldown[] Cooldowns => [new Cooldown(CooldownId, "Lending mask", ModConfig.Instance.values.MaskedLendCooldown)];

        private readonly CooldownNetworkBehaviour lendMaskCooldown;
        #endregion

        MaskedTamedBehaviour() => lendMaskCooldown = GetCooldownWithId(CooldownId);

        #region Custom behaviours
        internal enum CustomBehaviour
        {
            LendMask = 1,
            Ghostified
        }
        internal override List<Tuple<string, string, Action>>? CustomBehaviourHandler =>
        [
            new (CustomBehaviour.LendMask.ToString(), "Lending mask", OnLendMaskBehavior),
            new (CustomBehaviour.Ghostified.ToString(), "Ghostified", OnGhostBehavior)
        ];

        internal override void InitCustomBehaviour(int behaviour)
        {
            // ANY CLIENT
            base.InitCustomBehaviour(behaviour);

            switch ((CustomBehaviour)behaviour)
            {
                case CustomBehaviour.LendMask:
                    SetMaskShaking(false);
                    break;

                case CustomBehaviour.Ghostified:
                    ghostLifetime = 0f;

                    if (targetPlayer == null) return;

                    GhostAppeared();

                    var pitch = UnityEngine.Random.Range(0.75f, 1.25f);

                    Masked.movementAudio.clip = GhostAmbientSFX;
                    Masked.movementAudio.volume = 0.5f;
                    Masked.movementAudio.Play();
                    Masked.movementAudio.pitch = pitch;

                    if (_farAudio != null)
                    {
                        _farAudio.clip = GhostAmbientFarSFX;
                        _farAudio.volume = 0.5f;
                        _farAudio.Play();
                        _farAudio.pitch = pitch;
                    }

                    Masked.transform.LookAt(targetPlayer.transform);
                    _aiIntervalTimeBackup = Masked.AIIntervalTime;
                    Masked.AIIntervalTime = 0.05f;

                    if (IsOwner)
                    {
                        Invoke(nameof(GhostGlitchAnimationServerRpc), GhostSpawnTime + UnityEngine.Random.Range(0f, 2f));
                        if (GhostVoices.Count > 0)
                            Invoke(nameof(PlayRandomGhostVoiceServerRpc), GhostSpawnTime + UnityEngine.Random.Range(0f, 4f));
                    }
                    break;
            }
        }

        internal void OnLendMaskBehavior()
        {
            if (!_isWearingMask || _isTransferingMask) return;

            if (IsOwnerPlayer)
            {
                StartOfRound.Instance.fearLevel += Time.deltaTime / 10f;

                _timeWearingMask += Time.deltaTime;
                if (MaskAnimator != null && _timeWearingMask > (MaximumMaskWearingTime / 1.5f))
                    MaskAnimator.speed += Time.deltaTime / (MaximumMaskWearingTime / 4f);

                if (_timeWearingMask > MaximumMaskWearingTime)
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
        }

        internal static void LoadGhostAudio(AssetBundle assetBundle)
        {
            GhostAmbientSFX =       assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostAmbient.ogg");
            GhostAmbientFarSFX =    assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostAmbientFar.ogg");
            GhostHissSFX =          assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostHiss.ogg");
            GhostHissFastSFX =      assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostHissFast.ogg");
            GhostPoofSFX =          assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostPoof.ogg");

            var ghostLaughSFX =     assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostLaugh.ogg");
            var ghostLaughFarSFX =  assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostLaughFar.ogg");
            if (ghostLaughSFX != null && ghostLaughFarSFX != null)
                GhostVoices.Add(new Tuple<AudioClip, AudioClip>(ghostLaughSFX, ghostLaughFarSFX));

            var ghostLaugh2SFX =    assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostLaugh2.ogg");
            var ghostLaugh2FarSFX = assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostLaugh2Far.ogg");
            if (ghostLaugh2SFX != null && ghostLaugh2FarSFX != null)
                GhostVoices.Add(new Tuple<AudioClip, AudioClip>(ghostLaugh2SFX, ghostLaugh2FarSFX));

            var ghostCrySFX =       assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostCry.ogg");
            var ghostCryFarSFX =    assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostCryFar.ogg");
            if (ghostCrySFX != null && ghostCryFarSFX != null)
                GhostVoices.Add(new Tuple<AudioClip, AudioClip>(ghostCrySFX, ghostCryFarSFX));

            var ghostCry2SFX =      assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostCry2.ogg");
            var ghostCry2SFarFX =   assetBundle.LoadAsset<AudioClip>("Assets/Audio/Masked/GhostCry2Far.ogg");
            if (ghostCry2SFX != null && ghostCry2SFarFX != null)
                GhostVoices.Add(new Tuple<AudioClip, AudioClip>(ghostCry2SFX, ghostCry2SFarFX));
        }

        [ServerRpc(RequireOwnership = false)]
        internal void PlayRandomGhostVoiceServerRpc()
        {
            PlayRandomGhostVoiceClientRpc(UnityEngine.Random.RandomRangeInt(0, GhostVoices.Count - 1));

            var nextGhostVoiceIn = UnityEngine.Random.Range(3f, Mathf.Max(4f, 8f));
            if (GhostTimeToLive > nextGhostVoiceIn + 2f)
                Invoke(nameof(PlayRandomGhostVoiceClientRpc), nextGhostVoiceIn);
        }

        [ClientRpc]
        internal void PlayRandomGhostVoiceClientRpc(int index)
        {
            var usingFarSFX = Vector3.Distance(Masked.transform.position, Utils.CurrentPlayer.transform.position) > GhostAudioToggleDistance;
            Masked.creatureVoice.PlayOneShot(usingFarSFX ? GhostVoices[index].Item2 : GhostVoices[index].Item1);
        }

        [ServerRpc(RequireOwnership = false)]
        public void GhostGlitchAnimationServerRpc()
        {
            GhostGlitchAnimationClientRpc(UnityEngine.Random.Range(1f, GhostGlitchMaxDuration));

            var nextGhostGlitchIn = UnityEngine.Random.Range(2f, 4f);
            if (GhostTimeToLive > nextGhostGlitchIn + GhostGlitchMaxDuration)
                Invoke(nameof(GhostGlitchAnimationServerRpc), nextGhostGlitchIn);
        }

        [ClientRpc]
        public void GhostGlitchAnimationClientRpc(float duration)
        {
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
        private readonly List<ActionKey> _actionKeys =
        [
            new ActionKey() { Key = ModConfig.Instance.ActionKey1, Description = "Lend mask" }
        ];
        internal override List<ActionKey> ActionKeys => _actionKeys;

        internal override void ActionKey1Pressed()
        {
            base.ActionKey1Pressed();

            if (!IsTamed || _isTransferingMask) return;

            if (_isWearingMask)
            {
                GiveBackMaskServerRpc();
                return;
            }

            if (lendMaskCooldown != null && lendMaskCooldown.IsFinished())
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

            if (Mask != null)
            {
                _originalMaskParent = Mask.transform.parent;
                _originalMaskLocalPosition = Mask.transform.localPosition;
            }
            
            if (IsTamed)
                Masked.creatureAnimator.Play("Base Layer.Idle");
        }

        public override void OnDestroy()
        {
            CleanUp();
            
            base.OnDestroy();
        }

        void OnDisable() => CleanUp();

        internal void CleanUp()
        {
            if (_farAudio != null)
                Destroy(_farAudio);

            _parentMimic?._spawnedGhostMimics.Remove(Masked);

            StopAllCoroutines();
            for (int i = _spawnedGhostMimics.Count - 1; i >= 0; i--)
            {
                var ghostMimic = _spawnedGhostMimics[i];
                if (ghostMimic == null) continue;

                if(ghostMimic.IsSpawned)
                    RoundManager.Instance.DespawnEnemyGameObject(ghostMimic.NetworkObject);
                Destroy(ghostMimic);
            }
            _spawnedGhostMimics.Clear();

            MaskedPlayerEnemyPatch.lastColliderIDs.Remove(Masked.GetInstanceID());
        }

        internal override void InitTamingBehaviour(TamingBehaviour behaviour)
        {
            // ANY CLIENT
            base.InitTamingBehaviour(behaviour);

            if (behaviour == TamingBehaviour.TamedFollowing)
            {
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true);
                SetMaskShaking(false);
            }
        }

        internal override void LeaveCustomBehaviour(int behaviour)
        {
            base.LeaveCustomBehaviour(behaviour);
            
            if (behaviour == (int) CustomBehaviour.Ghostified)
                Masked.AIIntervalTime = _aiIntervalTimeBackup;
        }

        // OnTamedDefending idea: decoy

        internal override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            // ANY CLIENT
            if (_isTransferingMask) return;

            base.OnUpdate(update, doAIInterval);

            if (!isGhostified)
                Masked.CalculateAnimationDirection();
        }

        internal override void DoAIInterval()
        {
            // ANY CLIENT, every EnemyAI.updateDestinationInterval, if OnUpdate.doAIInterval = true
            base.DoAIInterval();

            if (ownerPlayer == null || isGhostified) return;

            if (!_isTransferingMask)
            {
                var shouldRun = DistanceToOwner > 8f;
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

        internal override void OnCallFromBall()
        {
            base.OnCallFromBall();

            Masked.EnableEnemyMesh(true);

            if (IsTamed)
            {
                Masked.stareAtTransform = ownerPlayer!.gameplayCamera.transform;
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

            Masked.inSpecialAnimationWithPlayer = playerWhoThrewBall;
            targetPlayer = playerWhoThrewBall;
            StartCoroutine(MaskedEscapeFromBallCoroutine());
        }

        internal IEnumerator MaskedEscapeFromBallCoroutine()
        {
            escapeFromBallEventRunning = true;

            Masked.enabled = false;
            if (IsOwner)
                Masked.agent.enabled = false;

            if (targetPlayer != null)
                Masked.transform.LookAt(targetPlayer.transform);

            bool maskEnabled = MirageCompatibility.IsMaskEnabled(Masked.gameObject);
            if (!maskEnabled)
            {
                // Mask disabled by another mod (e.g. Mirage)
                MirageCompatibility.ShowMaskOf(Masked.gameObject);
                Utils.GetRenderers(Mask).ForEach((r) => r.enabled = false);
            }

            yield return new WaitForSeconds(0.5f);

            SetMaskGlowNoSound(true);

            yield return new WaitForSeconds(0.5f);

            float timeGlowingUp = 0f; // Fallback
            float startIntensity = Masked.maskEyesGlowLight.intensity;
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
                return _spawnedGhostMimics.Count > 0 && fallbackTimer < triggerFallbackAfter;
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

            _spawnedGhostMimics.Add(ghostMimic);

            yield return null; // Call Start() once before switching

            tamedBehaviour.Masked.enabled = false;

            if (!MirageCompatibility.IsMaskEnabled(tamedBehaviour.Masked.gameObject)) // Mask disabled by another mod (e.g. Mirage)
                MirageCompatibility.ShowMaskOf(tamedBehaviour.Masked.gameObject);

            Masked.maskEyesGlow[Masked.maskTypeIndex].enabled = true;
            Masked.maskEyesGlowLight.enabled = true;

            tamedBehaviour._parentMimic = this;
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
                if (Masked.agent.speed > 50f && GhostHissFastSFX != null)
                    Utils.PlaySoundAtPosition(Utils.CurrentPlayer.transform.position, GhostHissFastSFX);
                else if (GhostHissSFX != null)
                    Utils.PlaySoundAtPosition(Utils.CurrentPlayer.transform.position, GhostHissSFX);
            }
        }

        public void GhostAppeared()
        {
            Ghostify(Masked);

            var mainParticle = Masked.teleportParticle.main;
            mainParticle.simulationSpeed = 20f;
            Masked.teleportParticle.Play();

            if (Vector3.Distance(Masked.transform.position, Utils.CurrentPlayer.transform.position) > 20f)
                RoundManager.Instance.FlickerLights(true, false);

            Masked.creatureAnimator.SetFloat("VelocityY", 1f);
            Masked.creatureAnimator.speed *= GhostChaseSpeed / 5f;

            Masked.SetMovingTowardsTargetPlayer(targetPlayer);
            Masked.addPlayerVelocityToDestination = 0f;
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

            if (GhostPoofSFX != null)
                Utils.PlaySoundAtPosition(Masked.transform.position, GhostPoofSFX);

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
            maskedEnemy.rendererLOD0.materials = Enumerable.Repeat(false, Masked.rendererLOD0.materials.Length).Select(x => new Material(Utils.GhostMaterial)).ToArray();
            maskedEnemy.rendererLOD1.materials = Enumerable.Repeat(false, Masked.rendererLOD1.materials.Length).Select(x => new Material(Utils.GhostMaterial)).ToArray();
            maskedEnemy.rendererLOD2.materials = Enumerable.Repeat(false, Masked.rendererLOD2.materials.Length).Select(x => new Material(Utils.GhostMaterial)).ToArray();

            // Remove unmodified badges
            var spineTransform = maskedEnemy.transform.Find("ScavengerModel/metarig/spine/spine.001/spine.002/spine.003");
            if (spineTransform != null)
            {
                spineTransform.Find("LevelSticker")?.gameObject.SetActive(false);
                spineTransform.Find("BetaBadge")?.gameObject.SetActive(false);
            }

            var enableMaskEyes = true;
            if (ModelReplacementAPICompatibility.Enabled)
            {
                var model = ModelReplacementAPICompatibility.FindCurrentReplacementModelIn(Masked.gameObject, isEnemy: true);
                if (model != null)
                {
                    Utils.ReplaceAllMaterialsWith(model, (_) => new Material(Utils.GhostMaterial));
                    enableMaskEyes = false;
                }
            }

            var mask = Mask;
            if (mask != null)
            {
                // Set mask invisible and stable
                Utils.GetRenderers(mask).ForEach((r) => r.enabled = false);
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

            var eyeRenderer = maskedEnemy.maskEyesGlow[maskedEnemy.maskTypeIndex];
            if (eyeRenderer != null)
            {
                // Change eye color to blue-white
                eyeRenderer.material = new Material(Utils.GhostEyesMaterial);
                eyeRenderer.enabled = enableMaskEyes;
            }

            // Add farAudio
            if (_farAudio == null)
            {
                _farAudio = Masked.gameObject.AddComponent<AudioSource>();
                _farAudio.maxDistance = GhostAudioToggleDistance * 5f;
                _farAudio.minDistance = GhostAudioToggleDistance;
                _farAudio.rolloffMode = AudioRolloffMode.Linear;
                _farAudio.spatialBlend = 1f; // default 0
                _farAudio.priority = 127; // default 128
            }

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
            StartCoroutine(LendMaskCoroutine());
            if (IsOwnerPlayer)
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, false);
        }

        IEnumerator LendMaskCoroutine()
        {
            _isTransferingMask = true;

            Masked.inSpecialAnimationWithPlayer = ownerPlayer;

            Masked.SetHandsOutClientRpc(true);

            yield return StartCoroutine(FaceOwner());

            yield return new WaitForSeconds(0.3f);

            SetMaskGlowNoSound();

            yield return StartCoroutine(RotateMaskOnPlayerFace());

            SetMaskShaking(false);

            if (IsOwnerPlayer)
                SetMaskGlassified();

            FinishKillAnimation(false);

            if (IsOwnerPlayer)
            {
                SetRedVision();
                CustomPassManager.Instance.EnableCustomPass(CustomPassManager.CustomPassType.SeeThroughEnemies, true);
            }
            else
                SetMaskGlowNoSound();

            _isWearingMask = true;

            Masked.SetHandsOutClientRpc(false);

            _isTransferingMask = false;

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
            StartCoroutine(GiveBackMaskCoroutine());
        }

        IEnumerator GiveBackMaskCoroutine()
        {
            _isTransferingMask = true;

            Masked.inSpecialAnimationWithPlayer = ownerPlayer;

            Masked.SetHandsOutClientRpc(true);

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

            FinishKillAnimation(false);

            _isWearingMask = false;
            _timeWearingMask = 0f;

            SetMaskGlowNoSound(false);

            Masked.SetHandsOutClientRpc(false);

            _isTransferingMask = false;
            Masked.inSpecialAnimationWithPlayer = null;

            lendMaskCooldown?.Reset();

            if (IsOwner)
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
        }

        IEnumerator RotateMaskOnPlayerFace()
        {
            if (ownerPlayer == null) yield break;

            var maskEndingPos = ownerPlayer.gameplayCamera.transform.position + ownerPlayer.gameplayCamera.transform.forward * 0.15f - Vector3.up * 0.1f;
            var maskEndingRot = ownerPlayer.transform.rotation;

            yield return StartCoroutine(RotateMaskTo(maskEndingPos, maskEndingRot, 1f, IsOwnerPlayer ? ownerPlayer.headCostumeContainerLocal.transform : ownerPlayer.headCostumeContainer.transform));
        }

        IEnumerator RotateMaskOnMaskedFace()
        {
            if (ownerPlayer == null || _originalMaskParent == null) yield break;

            LethalMon.Log("originalMaskLocalPosition: " + _originalMaskLocalPosition);
            var endPosition = _originalMaskParent.transform.position + Vector3.up * _originalMaskLocalPosition.y + Masked.transform.forward * _originalMaskLocalPosition.z;

            yield return StartCoroutine(RotateMaskTo(endPosition, _originalMaskParent.transform.rotation, 1f, _originalMaskParent ?? Masked.transform));

            if (Mask != null)
                Mask.transform.localPosition = _originalMaskLocalPosition;
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
            Masked.maskEyesGlow[Masked.maskTypeIndex].enabled = enable;
            Masked.maskEyesGlowLight.enabled = enable;
        }

        void SetMaskGlassified(bool glassified = true)
        {
            var mr = Mask?.transform.Find("Mesh").GetComponent<MeshRenderer>();
            if (mr == null) return;

            if (glassified)
            {
                if (_originalMaskMaterials.Length > 0) return; // Already glassified
                _originalMaskMaterials = Utils.ReplaceAllMaterialsWith(mr, (m) => new Material(Utils.Glass));
            }
            else
            {
                if (_originalMaskMaterials.Length == 0) return; // Unable to revert materials

                foreach (var m in mr.materials)
                    DestroyImmediate(m);

                mr.materials = _originalMaskMaterials;
                _originalMaskMaterials = [];
            }
        }

        void SetMaskShaking(bool shaking = true)
        {
            if (MaskAnimator == null) return;

            MaskAnimator.speed = shaking ? 1f : 0f; // 1f is default.. unsure why it's constantly shaking by default
        }

        void SetRedVision(bool enable = true)
        {
            if (ownerPlayer == null) return;

            if (enable)
            {
                _originalNightVisionColor = ownerPlayer.nightVision.color;
                ownerPlayer.nightVision.color = new Color(1f, 0.42f, 0f);
                _originalNightVisionIntensity = ownerPlayer.nightVision.intensity;
                ownerPlayer.nightVision.intensity /= 1.5f;
            }
            else
            {
                ownerPlayer.nightVision.color = _originalNightVisionColor.GetValueOrDefault(Color.white);
                ownerPlayer.nightVision.intensity = _originalNightVisionIntensity;
            }

            ownerPlayer.nightVision.enabled = enable;
        }

        /// <summary>
        /// Little trick to prevent the masked from switching of state when finishing the kill animation
        /// </summary>
        /// <param name="killPlayer">Kill the player at the end of the animation or not</param>
        private void FinishKillAnimation(bool killPlayer)
        {
            PropertyInfo? property = typeof(NetworkObject).GetProperty("IsSpawned");
            if (Masked.NetworkObject.IsSpawned && property != null)
            {
                property.GetSetMethod(true).Invoke(Masked.NetworkObject, [false]);
                Masked.FinishKillAnimation(killPlayer);
                property.GetSetMethod(true).Invoke(Masked.NetworkObject, [true]);
            }
            else
            {
                Masked.FinishKillAnimation(killPlayer);
            }
        }
        #endregion
    }
}
