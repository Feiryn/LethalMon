using GameNetcodeStuff;
using System.Collections.Generic;
using System;
using LethalMon.Items;
using UnityEngine;
using System.Collections;
using Unity.Netcode;
using LethalMon.Patches;
using System.Linq;
using static LethalMon.Utils;
using Vector3 = UnityEngine.Vector3;
using Dissonance;
using static UnityEngine.GraphicsBuffer;

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

        private bool IsChild => Maneater.babyContainer.activeSelf;
        private bool IsAdult => Maneater.adultContainer.activeSelf;
        private bool _transformAnimationRecorded = false;

        private float _ownerSpeakingAmplitude = 0f;
        private VoicePlayerState? _ownerVoiceState;

        // Status handling
        internal enum Status
        {
            Calm,
            Scared,
            Stressed,
            VeryStressed,
            Lonely,
            LeftAlone,
            StartAttacking
        }
        private Status _currentStatus = Status.Calm;
        private float _timeAtLastStatusChange = 0f;
        private bool IsScared => _currentStatus == Status.Scared && scaredTimer < UpdateStatusInterval;

        private float scaredTimer = 0f;
        private GameObject? objectScaredOf = null;

        // Chasing & Attacking
        private GameObject? Target => targetPlayer != null ? targetPlayer.gameObject : targetEnemy?.gameObject;
        private bool hasLineOfSightToTarget = false;
        private float _startedScreamingAt = 0f;

        // Constants
        private const float MaximumTargetingRange = 30f;
        private const float AttackDistance = 4f;
        private const float ChaseSpeed = 8f;
        private const float LeapSpeed = 12f;
        private const float ScreamDuration = 2f;

        private const float UpdateStatusInterval = 1f;

        private const float LonelinessPoint = 0.4f; // Point after which the baby feels lonely
        private const float LeftAlonePoint = 0.75f; // Point after which the baby feels left alone, shortly before attacking

        private const float StressedPoint = 0.5f; // ManeaterMemory.likeMeter point below which the baby feels stressed towards it
        private const float VeryStressedPoint = 0.25f; // ManeaterMemory.likeMeter point below which the baby feels under huge pressure, shortly before attacking

        internal bool CanTransform => becomeAggressiveCooldown == null || becomeAggressiveCooldown.IsFinished();

        internal override string DefendingBehaviourDescription => "Defending owner!";

        internal override bool CanDefend => false;
        #endregion

        #region Cooldowns
        private const string BecomeAggressiveCooldownID = "maneater_aggressive";
    
        internal override Cooldown[] Cooldowns => [new Cooldown(BecomeAggressiveCooldownID, "Become aggressive", 10f)];

        private CooldownNetworkBehaviour? becomeAggressiveCooldown;
        #endregion

        #region Custom behaviours
        internal enum CustomBehaviour
        {
            Transforming = 1,
            Chasing,
            Attacking,
        }
        internal override List<Tuple<string, string, Action>>? CustomBehaviourHandler =>
        [
            new (CustomBehaviour.Transforming.ToString(), "Transforming", OnTransformBehaviour),
            new (CustomBehaviour.Chasing.ToString(), "Chasing", OnChasingBehaviour),
            new (CustomBehaviour.Attacking.ToString(), "Attacking", OnAttackingBehaviour)
        ];

        internal override void InitCustomBehaviour(int behaviour)
        {
            // ANY CLIENT
            base.InitCustomBehaviour(behaviour);

            switch ((CustomBehaviour)behaviour)
            {
                case CustomBehaviour.Transforming:
                    if (IsOwner)
                    {
                        if (IsChild)
                            BecomeAdultServerRpc();
                        else
                            BecomeChildServerRpc();

                        Invoke(nameof(Transformed), 2.2f);
                    }
                    break;
                case CustomBehaviour.Chasing:
                    if (IsOwner)
                        Maneater.agent.speed = ChaseSpeed;
                    break;
                case CustomBehaviour.Attacking:
                    if (IsOwner)
                    {
                        Maneater.leapSpeed = LeapSpeed;
                        Maneater.DoLeapServerRpc();
                    }
                    // ...
                    break;
                default:
                    break;
            }
        }

        internal void EndChasing() => SwitchToCustomBehaviour((int)CustomBehaviour.Transforming);

        internal void OnChasingBehaviour()
        {
            if (!IsOwner) return;

            if (Maneater.screaming)
            {
                if (Time.realtimeSinceStartup - _startedScreamingAt < ScreamDuration)
                    return;
                else
                    StopScreaming();
            }

            if (targetEnemy != null)
            {
                var distance = DistanceToTargetEnemy;
                if (targetEnemy.isEnemyDead || distance > MaximumTargetingRange + 2f)
                {
                    if(targetEnemy.isEnemyDead)
                        _maneaterMemory.Remove(targetEnemy.gameObject);
                    targetEnemy = null;
                    return;
                }

                AttackWhenPossible(distance);
            }
            else if (targetPlayer != null)
            {
                var distance = DistanceToTargetPlayer;
                if (targetPlayer.isPlayerDead || distance > MaximumTargetingRange + 2f)
                {
                    if(targetPlayer.isPlayerDead)
                        _maneaterMemory.Remove(targetPlayer.gameObject);
                    targetPlayer = null;
                    return;
                }

                AttackWhenPossible(distance);
            }
            else if (!DetermineNextTarget())
            {
                EndChasing();
                return;
            }
        }

        internal void AttackWhenPossible(float distanceToTarget)
        {
            if (distanceToTarget < AttackDistance)
            {
                if (Maneater.CheckLineOfSightForPosition(Target!.transform.position))
                    SwitchToCustomBehaviour((int)CustomBehaviour.Attacking);
            }
        }

        internal void OnTransformBehaviour() { }

        internal void Transformed()
        {
            if (IsAdult)
                SwitchToCustomBehaviour((int)CustomBehaviour.Attacking);
            else
            {
                becomeAggressiveCooldown?.Reset();
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
            }
        }
        internal void OnAttackingBehaviour()
        {
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

            if (IsChild)
                BecomeAdultServerRpc();
            else
                BecomeChildServerRpc();
        }
        #endregion

        #region Base Methods
        internal override void Start()
        {
            SetTamedByHost_DEBUG();
            base.Start();
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);

            becomeAggressiveCooldown = GetCooldownWithId(BecomeAggressiveCooldownID);

            if (IsTamed)
                SetOwnerVoiceState();
        }

        internal override void InitTamingBehaviour(TamingBehaviour behaviour)
        {
            // ANY CLIENT
            base.InitTamingBehaviour(behaviour);

            switch(behaviour)
            {
                case TamingBehaviour.TamedFollowing:
                    EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true);
                    Maneater.lonelinessMeter = 0f;
                    UpdateHUDStatus();

                    Maneater.clickingAudio1.volume = 0f;
                    Maneater.clickingAudio2.volume = 0f;
                    break;

                case TamingBehaviour.TamedDefending:
                    break;

                default: break;
            }
        }

        internal override void OnTamedFollowing()
        {
            // OWNER ONLY
            if(!Maneater.holdingBaby)
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
            base.OnUpdate(update, doAIInterval);

            Maneater.SetClickingAudioVolume();

            if (!IsOwner) return;

            CheckPlayerVoices();

            if (IsChild)
                BabyUpdate();
            else
                AdultUpdate();
        }

        internal override void DoAIInterval()
        {
            //base.DoAIInterval();
            LethalMon.Log("DoAIInterval maneater");

            if (IsChild)
                BabyAIInterval();
            else
                AdultAIInterval();

            if (Enemy.moveTowardsDestination)
            {
                Enemy.agent.SetDestination(Enemy.destination);
            }
            Enemy.SyncPositionToClients();
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

        #region NoiseDetection
        internal void SetOwnerVoiceState()
        {
            foreach (var voicePlayerState in StartOfRound.Instance.voiceChatModule.Players)
            {
                if (!voicePlayerState.IsLocalPlayer) continue;
                _ownerVoiceState = voicePlayerState;
                break;
            }
        }

        internal void CheckPlayerVoices()
        {
            if (!IsOwner) return;

            foreach (var player in StartOfRound.Instance.allPlayerScripts)
            {
                if (player == null || player.isPlayerDead || !player.isPlayerControlled) continue;

                if (player?.voicePlayerState == null || player.isPlayerDead || !player.isPlayerControlled || !player.voicePlayerState.IsSpeaking) continue;
                LethalMon.Log(player.name + "is speaking");
                DetectNoise(player.gameObject, player.voicePlayerState.Volume);
            }
        }

        internal bool IsDetectingNoise(Vector3 noisePosition, float noiseLoudness, int timesPlayedInOneSpot = 0, int noiseID = 0)
        {
            if (noiseID == 6 || noiseID == 7) return false;

            return IsOwner && !Maneater.isEnemyDead &&
                   !IsAdult && (CustomBehaviour)CurrentCustomBehaviour.GetValueOrDefault(0) != CustomBehaviour.Transforming &&      // No voice checking in adult phase (for now!)
                   noiseLoudness > 0.1f && Vector3.Distance(noisePosition, Maneater.transform.position + Vector3.up * 0.4f) > 0.8f; // Too quiet or own voice
        }

        internal void DetectNoise(Vector3 noisePosition, float noiseLoudness, int timesPlayedInOneSpot = 0, int noiseID = 0)
        {
            //LethalMon.Log("NoiseID: " + noiseID);
            if (!IsDetectingNoise(noisePosition, noiseLoudness, timesPlayedInOneSpot, noiseID)) return;

            var noiseSource = FindNoiseSourceAtPosition(noisePosition);
            if (noiseSource == null) return;

            //LethalMon.Log("Noise came from " + noiseSource.name + ". ID: " + noiseID);

            DetectNoise(noiseSource, noiseLoudness * 10f);
        }
        internal void DetectNoise(GameObject noiseSource, float noiseLoudness)
        {
            AdjustNoiseLoudness(ref noiseLoudness, noiseSource.transform.position);
            RecognizedNoiseBy(noiseSource, noiseLoudness);

            Maneater.timeAtLastHeardNoise = Time.realtimeSinceStartup;
        }

        internal GameObject? FindNoiseSourceAtPosition(Vector3 position)
        {
            var overlappingSources = Physics.OverlapSphere(position, 3f, 1 << (int)LayerMasks.Mask.Enemies, QueryTriggerInteraction.Collide).Where(ns => ns?.gameObject != null);
            if (!overlappingSources.Any()) return null;

            var noiseSourcesInRange = overlappingSources.Select(source => source.GetComponentInParent<EnemyAI>()).ToArray();

            LethalMon.Log("Noise potentially from " + noiseSourcesInRange.Length + " sources.");
            if (noiseSourcesInRange.Length > 1)
                Array.Sort(noiseSourcesInRange, (x, y) => Vector3.Distance(x.transform.position, position).CompareTo(Vector3.Distance(y.transform.position, position)));

            var noiseSources = noiseSourcesInRange.Where(ns => ns?.gameObject != null && ns != Maneater);
            return noiseSources.Any() ? noiseSources.First().gameObject : null;
        }

        internal void AdjustNoiseLoudness(ref float noiseLoudness, Vector3 noisePosition)
        {
            if (Physics.Linecast(Maneater.transform.position, noisePosition, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
                noiseLoudness *= 0.6f; // Something in between Menateater and noise

            if (IsScared)
                noiseLoudness *= 1.2f; // Maneater is scared. Noise feels louder

            float distance = Vector3.Distance(noisePosition, Maneater.transform.position);
            noiseLoudness *= Mathf.Max(10f - distance, 1f) / 10f; // Weaker the further it is away
        }

        internal void RecognizedNoiseBy(GameObject noiseSource, float noiseLoudness)
        {
            if (Time.realtimeSinceStartup - Maneater.timeAtLastHeardNoise > 2f && noiseLoudness > 0.5f) // Baby got scared
            {
                _currentStatus = Status.Scared;
                scaredTimer = 0f;
                objectScaredOf = noiseSource;
                noiseLoudness *= Mathf.Min(Time.realtimeSinceStartup - Maneater.timeAtLastHeardNoise, 4f);
                BabyGotScared();
            }

            if (!_maneaterMemory.ContainsKey(noiseSource.gameObject))
                _maneaterMemory.Add(noiseSource.gameObject, 1f);

            _maneaterMemory[noiseSource.gameObject] -= noiseLoudness * Maneater.AIIntervalTime / 5f;
        }
        #endregion

        #region Memories

        private Dictionary<GameObject, float> _maneaterMemory = [];

        internal KeyValuePair<GameObject, float>? LeastLikedMemory
        {
            get
            {
                if (_maneaterMemory.Count == 0)
                    return null;

                var min = _maneaterMemory.Min(mem => mem.Value);
                return _maneaterMemory.Where(mem => mem.Value == min).First();
            }
        }

        internal void SortOutDeadMemories()
        {
            for(int i = _maneaterMemory.Count - 1; i >= 0; i--)
            {
                var key = _maneaterMemory.ElementAt(i).Key;
                if (key.TryGetComponent(out PlayerControllerB player))
                {
                    if(player.isPlayerDead)
                        _maneaterMemory.Remove(key);
                }
                else if (key.TryGetComponent(out EnemyAI enemyAI) && enemyAI.isEnemyDead)
                    _maneaterMemory.Remove(key);
            }
        }

        internal List<GameObject> MemoriesForStatus(Status status)
        {
            switch(status)
            {
                case Status.Stressed:
                    var stressingMemories = _maneaterMemory.Where(mem => mem.Value < StressedPoint);
                    return stressingMemories.Any() ? stressingMemories.Select(m => m.Key).ToList() : [];
                case Status.VeryStressed:
                    var veryStressingMemories = _maneaterMemory.Where(mem => mem.Value < StressedPoint);
                    return veryStressingMemories.Any() ? veryStressingMemories.Select(m => m.Key).ToList() : [];
                default: return [];
            }
        }

        internal List<GameObject> VeryStressingMemories
        {
            get
            {
                var memories = _maneaterMemory.Where(mem => mem.Value < StressedPoint);
                return memories.Any() ? memories.Select(m => m.Key).ToList() : [];
            }
        }

        internal void IncreaseLikeMeterBy(float addition, GameObject source)
        {
            if (!_maneaterMemory.ContainsKey(source))
                _maneaterMemory.Add(source, 1f);

            var updatedLikeMeter = _maneaterMemory[source] + addition;
            if (updatedLikeMeter > 1f)
                _maneaterMemory.Remove(source);
            else
                _maneaterMemory[source] = updatedLikeMeter;
        }

        internal void DecreaseLikeMeterBy(float reduction, GameObject source)
        {
            if (!_maneaterMemory.ContainsKey(source))
                _maneaterMemory.Add(source, 1f);

            _maneaterMemory[source] = Mathf.Max(_maneaterMemory[source] - reduction, 0f);
        }

        internal string MemoryName(GameObject memory)
        {
            if (memory == CurrentPlayer) return "you";

            if (memory.TryGetComponent(out PlayerControllerB player))
                return player.playerUsername;

            if (memory.TryGetComponent(out EnemyAI enemyAI))
                return enemyAI.enemyType.name;

            return memory.name;
        }
        #endregion

        #region Status
        private Status CurrentStatus
        {
            get
            {
                if(IsScared) return Status.Scared;

                if (Maneater.lonelinessMeter >= 1f)
                    return Status.StartAttacking;

                var memory = LeastLikedMemory;
                if (!memory.HasValue || Maneater.lonelinessMeter > (1f - memory.Value.Value))
                {
                    if (Maneater.lonelinessMeter > 1f)
                        return Status.StartAttacking;

                    if (Maneater.lonelinessMeter > LeftAlonePoint)
                        return Status.LeftAlone;

                    return Maneater.lonelinessMeter > LonelinessPoint ? Status.Lonely : Status.Calm;
                }

                var likeMeter = memory.Value.Value;

                if (likeMeter <= 0.05f)
                    return Status.StartAttacking;

                if (likeMeter < VeryStressedPoint)
                    return Status.VeryStressed;

                if (likeMeter < StressedPoint)
                    return Status.Stressed;

                return Status.Calm;
            }
        }

        internal void UpdateStatus()
        {
            if(IsScared)
            {
                scaredTimer += Time.deltaTime * Maneater.AIIntervalTime * (Maneater.holdingBaby ? 2f : 1f);
                return;
            }

            if(_currentStatus == Status.Scared)
                BabyNotScaredAnymore();

            if (Time.realtimeSinceStartup - _timeAtLastStatusChange < UpdateStatusInterval) return;

            var currentStatus = CurrentStatus;
            if (_currentStatus == currentStatus)
                return;
            _currentStatus = currentStatus;

            UpdateHUDStatus();
        }

        internal void UpdateHUDStatus()
        {
            switch (_currentStatus)
            {
                case Status.Calm:
                    HUDManagerPatch.UpdateTamedMonsterAction("Feels good.");
                    break;
                case Status.Scared:
                    HUDManagerPatch.UpdateTamedMonsterAction("Scared of " + (objectScaredOf != null ? objectScaredOf.name : "old memories"));
                    break;
                case Status.Stressed:
                    HUDManagerPatch.UpdateTamedMonsterAction("Stressed from " + string.Join(", ", MemoriesForStatus(_currentStatus).Select(MemoryName)));
                    break;
                case Status.VeryStressed:
                    HUDManagerPatch.UpdateTamedMonsterAction("Very stressed from " + string.Join(", ", MemoriesForStatus(_currentStatus).Select(MemoryName)) + ". ATTENTION!");
                    break;
                case Status.Lonely:
                    HUDManagerPatch.UpdateTamedMonsterAction("Starts to feel lonely.");
                    break;
                case Status.LeftAlone:
                    HUDManagerPatch.UpdateTamedMonsterAction("Feels left alone. ATTENTION!");
                    break;
                default:
                    break;
            }
        }
        #endregion

        #region Baby
        internal void BabyUpdate()
        {
            if (_maneaterMemory.Count > 0)
            {
                string log = "";
                foreach (var memory in _maneaterMemory)
                    log += MemoryName(memory.Key) + "[" + memory.Value + "]     ";
                LethalMon.Log(log);
            }

            UpdateLonelinessAndLikeMeter();

            UpdateStatus();
            if(_currentStatus == Status.StartAttacking && CanTransform)
            {
                SwitchToCustomBehaviour((int)CustomBehaviour.Transforming);
                return;
            }
        }

        internal void BabyAIInterval()
        {
            //LethalMon.Log("Loneliness: " + Maneater.lonelinessMeter);
            for (int i = _maneaterMemory.Count - 1; i >= 0; i--)
            {
                var memory = _maneaterMemory.ElementAt(i).Key;
                if (!Maneater.CheckLineOfSightForPosition(memory.transform.position))
                    IncreaseLikeMeterBy(Time.deltaTime / 5f * (Maneater.playerHolding == ownerPlayer ? 2.5f : 1.5f), memory); // Calming down if not in LoS
            }
        }

        internal void UpdateLonelinessAndLikeMeter()
        {
            var feelsLonely = Maneater.lonelinessMeter > 0f;
            if(Maneater.holdingBaby)
            {
                var ownerIsHoldingBaby = Maneater.playerHolding == ownerPlayer;
                if (Maneater.rockingBaby > 0)
                {
                    // Is shaking
                    if (ownerIsHoldingBaby)
                    {
                        if (!feelsLonely)
                            DecreaseLikeMeterBy(Time.deltaTime / 6f * Maneater.rockingBaby, ownerPlayer!.gameObject);
                    }
                    else
                    {
                        DecreaseLikeMeterBy(Time.deltaTime / 2f * Maneater.rockingBaby, ownerPlayer!.gameObject);
                    }
                }

                Maneater.lonelinessMeter -= Time.deltaTime / 20f * (Maneater.rockingBaby + 1); // 0 = no shaking, 1 = normal, 2 = hard
            }
            else
            {
                Maneater.lonelinessMeter += Time.deltaTime / 30f * (1f + Mathf.Clamp(DistanceToOwner / 5f, 0f, 1f));
            }


            if (_ownerVoiceState != null && _ownerVoiceState.IsSpeaking)
            {
                float loudness = Mathf.Clamp(_ownerVoiceState.Amplitude * 10f, 0f, 1f);
                AdjustNoiseLoudness(ref loudness, CurrentPlayer.transform.position);

                //LethalMon.Log("Local player is speaking. Volume: " + loudness);
                Maneater.lonelinessMeter -= Time.deltaTime * loudness;
                /*for (int i = _maneaterMemory.Count - 1; i >= 0; --i)
                    IncreaseLikeMeterBy(Time.deltaTime / 5f * loudness, _maneaterMemory.ElementAt(i).Key);*/
            }

            Maneater.lonelinessMeter = Mathf.Clamp(Maneater.lonelinessMeter, 0f, 1f);
        }

        internal void CalmDown()
        {
            if (!IsOwner) return;

            _currentStatus = Status.Calm;
            Maneater.lonelinessMeter = 0f;
            foreach (var memory in _maneaterMemory)
            {
                _maneaterMemory[memory.Key] *= 5f;
                if (memory.Value > 1f)
                    _maneaterMemory.Remove(memory.Key);
            }
        }

        internal void BabyGotScared()
        {

        }

        internal void BabyNotScaredAnymore()
        {

        }
        #endregion

        #region Adult
        internal void AdultUpdate()
        {

        }
        internal void AdultAIInterval()
        {

        }

        internal bool DetermineNextTarget()
        {
            SortOutDeadMemories();

            var m = _maneaterMemory.Where(mem => mem.Value < StressedPoint);
            if (!m.Any())
            {
                if (Maneater.lonelinessMeter >= 1f)
                    SetTarget(ownerPlayer!);
            }

            var stressedMemories = m.ToArray();

            Array.Sort(stressedMemories, (x, y) => x.Value.CompareTo(y.Value)); // Sort by likeMeter

            var memoriesInLoS = stressedMemories.Where(m => Maneater.CheckLineOfSightForPosition(m.Key.transform.position, 180f));
            if (memoriesInLoS.Any())
            {
                if(1f - Maneater.lonelinessMeter < memoriesInLoS.First().Value)
                {
                    SetTarget(ownerPlayer!);
                    return true;
                }
                // Target in line of sight. Take the one with the lowest likeMeter
                hasLineOfSightToTarget = true;
                SetTarget(memoriesInLoS.First().Key);
                return true;
            }

            if (1f - Maneater.lonelinessMeter < stressedMemories.First().Value)
            {
                SetTarget(ownerPlayer!);
                return true;
            }

            GameObject? target = null;
            float targetingLikelyness = 0f;
            foreach (var memory in stressedMemories)
            {
                LethalMon.Log("LikeMeter of potential target: " + memory.Value);
                float hateMeter = 1f - memory.Value;
                var distance = Vector3.Distance(memory.Key.transform.position, Maneater.transform.position);
                var likelyness = hateMeter - distance / 30f;
                if(likelyness > 0f) // if hateMeter is e.g. 0.7f, then the distance has to be below 21f, for 0.9f it's 27f
                {
                    // can be targeted
                    if(likelyness > targetingLikelyness)
                    {
                        targetingLikelyness = likelyness;
                        target = memory.Key;
                    }
                }
            }

            if (target != null)
                SetTarget(target);

            return target != null;
        }

        internal void SetTarget(GameObject target)
        {
            if (target.TryGetComponent(out PlayerControllerB player))
                SetTarget(player);
            else if (target.TryGetComponent(out EnemyAI enemyAI))
                SetTarget(enemyAI);
        }

        internal void SetTarget(PlayerControllerB player)
        {
            targetPlayer = player;
            Maneater.SetMovingTowardsTargetPlayer(targetPlayer);
            Maneater.addPlayerVelocityToDestination = 0f;
        }

        internal void SetTarget(EnemyAI enemy) => targetEnemy = enemy;
        #endregion

        public IEnumerator EndSpecialAnimationAfterLanding() // taken from DropBabyAnimation
        {
            float time = Time.realtimeSinceStartup;
            yield return new WaitUntil(() => Maneater.propScript.isHeld || Maneater.propScript.reachedFloorTarget || Time.realtimeSinceStartup - time > 2f);

            Maneater.inSpecialAnimation = false;

            if(IsOwner)
            {
                Maneater.agent.enabled = false;
                Maneater.agent.enabled = true;
            }
        }

        #region Transforming
        [ServerRpc(RequireOwnership = false)]
        public void BecomeChildServerRpc()
        {
            BecomeChildClientRpc();
        }

        [ClientRpc]
        public void BecomeChildClientRpc()
        {
            CalmDown();
            Maneater.agent.acceleration = 35f;
            Maneater.agent.angularSpeed = 220;
            Maneater.syncMovementSpeed = 0.26f;
            Maneater.addPlayerVelocityToDestination = 0f;
            Maneater.updatePositionThreshold = 0.26f;
            Maneater.propScript.EnablePhysics(enable: true);
            Maneater.propScript.grabbable = true;
            Maneater.propScript.grabbableToEnemies = true;
            Maneater.propScript.enabled = true;
            Maneater.inSpecialAnimation = true;
            Maneater.agent.enabled = false;
            Maneater.StartCoroutine(becomeChildAnimation());
        }

        private IEnumerator becomeChildAnimation()
        {
            Maneater.creatureSFX.volume = 0.5f;
            Maneater.creatureSFX.PlayOneShot(Maneater.transformationSFX);
            WalkieTalkie.TransmitOneShotAudio(Maneater.creatureSFX, Maneater.transformationSFX);

            yield return StartCoroutine(Utils.StartPlaybackOfAnimator(Maneater.creatureAnimator, true));

            LethalMon.Log("Enabling baby.");
            Maneater.babyContainer.SetActive(value: true);
            Maneater.adultContainer.SetActive(value: false);
            yield return new WaitForSeconds(0.05f);

            yield return StartCoroutine(Utils.StartPlaybackOfAnimator(Maneater.babyCreatureAnimator, true));
            Maneater.babyCreatureAnimator.SetBool("Transform", false);

            Maneater.creatureSFX.volume = 1f;

            // todo: find out why it plays the animation normally again afterwards
            Maneater.inSpecialAnimation = false;
            if (IsOwner)
                Maneater.agent.enabled = true;
        }

        [ServerRpc(RequireOwnership = false)]
        public void BecomeAdultServerRpc()
        {
            BecomeAdultClientRpc();
        }

        [ClientRpc]
        public void BecomeAdultClientRpc()
        {
            if (!_transformAnimationRecorded)
                Maneater.StartCoroutine(RecordTransformAnimation());
            Maneater.StartTransformationAnim();
            Maneater.addPlayerVelocityToDestination = 0f;
        }

        public IEnumerator RecordTransformAnimation()
        {
            yield return StartCoroutine(Utils.RecordAnimation(Maneater.babyCreatureAnimator, 0.5f));
            yield return new WaitForSeconds(0.05f);
            yield return StartCoroutine(Utils.RecordAnimation(Maneater.creatureAnimator, 1.7f));
            _transformAnimationRecorded = true;
        }
        #endregion

        #region Animations
        internal void StopScreaming()
        {
            Maneater.screaming = false;
            Maneater.creatureAnimator.SetBool("Screaming", value: false);
        }
        #endregion
    }
#endif
}
