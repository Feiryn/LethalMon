using GameNetcodeStuff;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using System.Collections;
using LethalMon.CustomPasses;
using static LethalMon.Utils;
using System;

namespace LethalMon.Behaviours
{
    internal class BaboonHawkTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        private BaboonBirdAI? _baboonHawk = null;
        internal BaboonBirdAI BaboonHawk
        {
            get
            {
                if (_baboonHawk == null)
                    _baboonHawk = (Enemy as BaboonBirdAI)!;

                return _baboonHawk;
            }
        }

        internal override float TargetingRange => 5f;

        internal static AudioClip? EchoLotSFX = null;
        internal const float MaximumEchoDistance = 50f;
        internal const float EchoKeepAlive = 3f; // Keep-alive once full distance is reached
        internal static readonly Color EchoLotColor = new(1f, 1f, 0f, 0.45f);

        private float _idleTimer = 0f;
        internal bool isEscapeFromBallCoroutineRunning = false;
        #endregion

        #region Cooldowns
        private const string EchoLotCooldownId = "baboonhawk_echolot";
        private const string HittingEnemyCooldownId = "baboonhawk_hittingenemy";

        internal override Cooldown[] Cooldowns => [
            new Cooldown(EchoLotCooldownId, "Echo lot", 15f),
            new Cooldown(HittingEnemyCooldownId, "Hitting enemy", 5f)
            ];

        private CooldownNetworkBehaviour? echoLotCooldown;
        private CooldownNetworkBehaviour? hittingEnemyCooldown;
        #endregion

        #region Action Keys
        private readonly List<ActionKey> _actionKeys =
        [
            new ActionKey() { Key = ModConfig.Instance.ActionKey1, Description = "Scan for items" }
        ];
        internal override List<ActionKey> ActionKeys => _actionKeys;

        internal override void ActionKey1Pressed()
        {
            base.ActionKey1Pressed();
            if (echoLotCooldown != null && echoLotCooldown.IsFinished())
                StartEchoLotServerRpc();
        }
        #endregion

        #region Base Methods
        internal override void Start()
        {
            base.Start();

            echoLotCooldown = GetCooldownWithId(EchoLotCooldownId);
            hittingEnemyCooldown = GetCooldownWithId(HittingEnemyCooldownId);

            if (IsTamed)
            {
                BaboonHawk.transform.localScale = Vector3.one * 0.75f;
            }

            if(BaboonHawk.agent != null)
                BaboonHawk.agent.angularSpeed = 0f;

            //BaboonHawk.creatureAnimator.Play("Base Layer.BaboonIdle");
            StartCoroutine(SkipSpawnAnim());
        }
        internal IEnumerator SkipSpawnAnim()
        {
            BaboonHawk.creatureAnimator.speed = 1000f;
            yield return null;
            BaboonHawk.creatureAnimator.speed = 1f;
        }

        internal override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            base.OnUpdate(update, false);

            BaboonHawk.CalculateAnimationDirection();

            if (BaboonHawk.miscAnimationTimer > 0f)
                BaboonHawk.miscAnimationTimer -= Time.deltaTime;

            if (BaboonHawk.headLookRig.weight < 1f)
                BaboonHawk.headLookRig.weight = Mathf.Lerp(BaboonHawk.headLookRig.weight, 1f, Time.deltaTime * 10f);
            
            // Idle animations
            if (CurrentTamingBehaviour == TamingBehaviour.TamedFollowing)
            {
                if (BaboonHawk.agentLocalVelocity.x == 0f && BaboonHawk.agentLocalVelocity.z == 0f) // Idle
                    _idleTimer += Time.deltaTime;
                else
                    _idleTimer = 0f;

                SetSitting(_idleTimer > 1f && _idleTimer <= 6f);
                SetSleeping(_idleTimer > 6f);
            }
        }

        internal override void TurnTowardsPosition(Vector3 position)
        {
            if(IsSleeping) return;

            base.TurnTowardsPosition(position);

            BaboonHawk.AnimateLooking(position);
        }

        internal override void InitTamingBehaviour(TamingBehaviour behaviour)
        {
            base.InitTamingBehaviour(behaviour);

            switch(behaviour)
            {
                case TamingBehaviour.TamedFollowing:
                    SetAggressive(false);
                    SetFighting(false);
                    break;
                case TamingBehaviour.TamedDefending:
                    if (targetEnemy == null) return;
                    
                    MoveTowards(targetEnemy.transform.position);

                    SetAggressive(true);
                    SetFighting(true);
                    _idleTimer = 0f;
                    BaboonHawk.addPlayerVelocityToDestination = 0f;
                    BaboonHawk.aggressionAudio.clip = BaboonHawk.enemyType.audioClips[2];
                    BaboonHawk.aggressionAudio.Play();
                    BaboonHawk.aggressionAudio.volume = 0f;
                    if (Utils.IsHost)
                        BaboonHawk.StartMiscAnimationServerRpc(0);
                    BaboonHawk.timeSinceHitting = 0f; // Skips the normal OnCollideWithEnemy
                    break;
                default: break;
            }
        }

        internal override void OnTamedFollowing()
        {
            base.OnTamedFollowing();

            // Aduio
            if (BaboonHawk.aggressionAudio.volume > 0f)
                BaboonHawk.aggressionAudio.volume = Mathf.Max(BaboonHawk.aggressionAudio.volume + Time.deltaTime * 5f, 0f);
            else if (BaboonHawk.aggressionAudio.isPlaying)
                BaboonHawk.aggressionAudio.Stop();

            // Targeting
            TargetNearestEnemy();
        }

        internal override void OnTamedDefending()
        {
            base.OnTamedDefending();

            if (!HasTargetEnemy || targetEnemy!.isEnemyDead || DistanceToOwner > 10f)
            {
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                return;
            }

            if (BaboonHawk.aggressionAudio.volume < 1f)
                BaboonHawk.aggressionAudio.volume = Mathf.Min(BaboonHawk.aggressionAudio.volume + Time.deltaTime * 4f, 1f);

            if (!IsCollidingWithTargetEnemy)
            {
                BaboonHawk.SetDestinationToPosition(targetEnemy.transform.position);
                TurnTowardsPosition(targetEnemy.transform.position);
                if (IsOwner)
                    BaboonHawk.agent.speed = hittingEnemyCooldown != null ? Mathf.Clamp(hittingEnemyCooldown.CurrentTimer * 2f, 1f, 10f) : 4f; // Slow down after hit
                return;
            }

            if(hittingEnemyCooldown != null && hittingEnemyCooldown.IsFinished())
            {
                LethalMon.Log("BaboonHawk hitting target");
                hittingEnemyCooldown.Reset();

                BaboonHawk.creatureAnimator.ResetTrigger("Hit");
                BaboonHawk.creatureAnimator.SetTrigger("Hit");
                BaboonHawk.creatureSFX.PlayOneShot(BaboonHawk.enemyType.audioClips[5]);
                WalkieTalkie.TransmitOneShotAudio(BaboonHawk.creatureSFX, BaboonHawk.enemyType.audioClips[5]);
                RoundManager.Instance.PlayAudibleNoise(BaboonHawk.creatureSFX.transform.position, 8f, 0.7f);
                targetEnemy.HitEnemy(1, null, playHitSFX: true);
                return;
            }

            if (BaboonHawk.miscAnimationTimer <= 0f && hittingEnemyCooldown != null && hittingEnemyCooldown.CurrentTimer > 2f)
                BaboonHawk.StartMiscAnimationServerRpc(UnityEngine.Random.RandomRangeInt(0, BaboonHawk.enemyType.miscAnimations.Length - 1));
        }

        internal override bool EnemyMeetsTargetingConditions(EnemyAI enemyAI)
        {
            if (!enemyAI.enemyType.canDie) return false; // pointless to attack

            return base.EnemyMeetsTargetingConditions(enemyAI);
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            // ANY CLIENT
            base.OnEscapedFromBall(playerWhoThrewBall);

            StartCoroutine(EscapedFromBallCoroutine());
        }

        internal IEnumerator EscapedFromBallCoroutine()
        {
            isEscapeFromBallCoroutineRunning = true;

            if (IsOwner)
            {
                BaboonHawk.agent.enabled = false;
                BaboonHawk.enabled = false;
            }

            BaboonHawk.creatureAnimator.SetBool("sit", true);

            yield return new WaitForSeconds(2f);

            RoundManager.PlayRandomClip(BaboonHawk.creatureVoice, BaboonHawk.cawScreamSFX, randomize: true, 1f, 1105);

            yield return new WaitForSeconds(1f);

            var spawnPos = BaboonHawk.transform.position - BaboonHawk.transform.forward * 0.5f;
            if (Utils.IsHost)
            {
                var tinyHawk = Utils.SpawnEnemyAtPosition(Utils.Enemy.BaboonHawk, spawnPos) as BaboonBirdAI;
                if (tinyHawk != null)
                    SpawnedTinyHawkServerRpc(tinyHawk.NetworkObject);
            }

            Utils.PlaySoundAtPosition(spawnPos, StartOfRound.Instance.playerHitGroundSoft);

            yield return new WaitForSeconds(0.3f);

            BaboonHawk.creatureAnimator.SetBool("sit", false);

            // yield return new WaitForSeconds(10f); // For testing purposes

            if (IsOwner)
            {
                BaboonHawk.enabled = true;
                BaboonHawk.agent.enabled = true;
            }

            isEscapeFromBallCoroutineRunning = false;
        }

        internal override void OnCallFromBall()
        {
            base.OnCallFromBall();

            if(IsOwnerPlayer)
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true);
        }
        #endregion

        #region Methods
        internal static void LoadAudio(AssetBundle assetBundle)
        {
            EchoLotSFX = assetBundle.LoadAsset<AudioClip>("Assets/Audio/BaboonHawk/EchoLot.ogg");
        }
        internal bool IsSleeping => BaboonHawk.creatureAnimator != null && BaboonHawk.creatureAnimator.GetBool("sleep");
        internal void SetSleeping(bool sleeping = true) => BaboonHawk.creatureAnimator?.SetBool("sleep", sleeping);
        internal bool IsSitting => BaboonHawk.creatureAnimator != null && BaboonHawk.creatureAnimator.GetBool("sit");
        internal void SetSitting(bool sitting = true) => BaboonHawk.creatureAnimator?.SetBool("sit", sitting);
        internal bool IsFighting => BaboonHawk.creatureAnimator != null && BaboonHawk.creatureAnimator.GetBool("fighting");
        internal void SetFighting(bool fighting = true) => BaboonHawk.creatureAnimator?.SetBool("fighting", fighting);
        internal bool IsAggressive => BaboonHawk.creatureAnimator != null && BaboonHawk.creatureAnimator.GetBool("aggressiveDisplay");
        internal void SetAggressive(bool aggressive = true) => BaboonHawk.creatureAnimator?.SetBool("aggressiveDisplay", aggressive);
        #endregion

        #region EchoLot
        [ServerRpc(RequireOwnership = false)]
        internal void StartEchoLotServerRpc()
        {
            StartEchoLotClientRpc();
        }

        [ClientRpc]
        internal void StartEchoLotClientRpc()
        {
            echoLotCooldown?.Reset();

            HUDManager.Instance.scanEffectAnimator.transform.position = BaboonHawk.transform.position;
            HUDManager.Instance.scanEffectAnimator.SetTrigger("scan");
            ownerPlayer?.StartCoroutine(EchoLotColorAdjust());

            if (EchoLotSFX != null)
                BaboonHawk.creatureSFX.PlayOneShot(EchoLotSFX);

            ownerPlayer?.StartCoroutine(EchoLotScanCoroutine()); // Putting it on the ownerPlayer just in case they retreive the enemy during the process
        }

        internal IEnumerator EchoLotColorAdjust()
        {
            var scanMaterial = HUDManager.Instance.scanEffectAnimator.GetComponent<MeshRenderer>()?.material;
            if (scanMaterial == null) yield break;

            var originalColor = scanMaterial.color;
            scanMaterial.color = EchoLotColor;
            yield return new WaitForSeconds(1f);
            scanMaterial.color = originalColor;
        }

        internal IEnumerator EchoLotScanCoroutine()
        {
            var customPass = CustomPassManager.Instance.EnableCustomPass(CustomPassManager.CustomPassType.SeeThroughItems, true) as SeeThroughCustomPass;
            if (customPass == null) yield break;

            customPass.maxVisibilityDistance = 0f;

            yield return new WaitWhile(() =>
            {
                customPass.maxVisibilityDistance += Time.deltaTime * MaximumEchoDistance; // takes 1s
                return customPass.maxVisibilityDistance < MaximumEchoDistance;
            });

            yield return new WaitForSeconds(EchoKeepAlive);

            yield return new WaitWhile(() =>
            {
                customPass.maxVisibilityDistance -= Time.deltaTime * MaximumEchoDistance * 2f; // takes half a sec
                return customPass.maxVisibilityDistance > 0f;
            });
            customPass.enabled = false;
        }
        #endregion

        #region TinyHawk
        [ServerRpc]
        internal void SpawnedTinyHawkServerRpc(NetworkObjectReference hawkRef) => SpawnedTinyHawkClientRpc(hawkRef);

        [ClientRpc]
        internal void SpawnedTinyHawkClientRpc(NetworkObjectReference hawkRef)
        {
            if (hawkRef.TryGet(out NetworkObject networkObject) && networkObject.TryGetComponent(out EnemyAI tinyHawk))
                tinyHawk.gameObject.AddComponent<TinyHawkBehaviour>().motherBird = BaboonHawk;
        }

        // TODO: Make it a Networkbehaviour somehow...
        internal class TinyHawkBehaviour : MonoBehaviour
        {
            #region Properties
            internal BaboonBirdAI? motherBird = null;
            private BaboonBirdAI? _tinyHawk = null;
            private const float BaseSpeed = 1.5f;
            private const float PingOnDeathRange = 20f;
            #endregion

            #region Base Methods
            void Start()
            {
                if (!gameObject.TryGetComponent(out _tinyHawk))
                {
                    Destroy(this);
                    return;
                }

                _tinyHawk!.transform.localScale /= 5f;
                _tinyHawk.creatureVoice.maxDistance = 5f;

                if(motherBird != null && motherBird.scoutingGroup != null)
                    motherBird.scoutingGroup.members.Add(_tinyHawk);
            }

            void Update()
            {
                if(_tinyHawk == null || _tinyHawk.isEnemyDead)
                {
                    if (_tinyHawk != null && _tinyHawk.isEnemyDead && Utils.IsHost)
                        PingNearBaboonHawksOnDeath();

                    Destroy(this);
                    return;
                }

                if (_tinyHawk.agent != null)
                    _tinyHawk.agent.speed = BaseSpeed;

                if (_tinyHawk.creatureVoice != null)
                {
                    _tinyHawk.creatureVoice.pitch = 2f;
                    _tinyHawk.creatureVoice.volume = 0.2f;
                }

                if(_tinyHawk.creatureAnimator != null)
                    _tinyHawk.creatureAnimator.speed = 0.5f;
            }

            void OnTriggerEnter(Collider other)
            {
                if (_tinyHawk == null || _tinyHawk.timeSinceHitting < 0.5f)
                    return;

                if (other.gameObject.TryGetComponent( out PlayerControllerB player))
                {
                    _tinyHawk.timeSinceHitting = 0f;
                    OnCollideWithPlayer(player);
                }
            }
            #endregion

            #region Methods
            internal void PingNearBaboonHawksOnDeath()
            {
                if (_tinyHawk == null) return;

                var enemiesInRange = Physics.OverlapSphere(_tinyHawk.transform.position, PingOnDeathRange, 1 << (int)LayerMasks.Mask.Enemies, QueryTriggerInteraction.Collide);
                foreach (var enemyHit in enemiesInRange)
                {
                    if (enemyHit != null && enemyHit.TryGetComponent(out BaboonBirdAI baboonBirdAI) && !baboonBirdAI.isEnemyDead)
                        baboonBirdAI.PingBaboonInterest(_tinyHawk.transform.position, 4);

                    if (motherBird != null)
                    {
                        motherBird.timeSincePingingBirdInterest = 0f;
                        if(motherBird.focusingOnThreat && motherBird.focusedThreat == null)
                            motherBird.focusingOnThreat = false;
                        motherBird.PingBaboonInterest(_tinyHawk.transform.position, 42);
                    }
                }
            }

            internal void OnCollideWithPlayer(PlayerControllerB player)
            {
                LethalMon.Log("Hitting tiny hawk");
                _tinyHawk!.HitEnemy(1, player, true); // poor tiny hawk got hurt

                var directionalVector = (_tinyHawk.transform.position - player.transform.position).normalized;
                directionalVector.y = 0f;
                StartCoroutine(boinkAnimation(directionalVector));
            }

            internal IEnumerator boinkAnimation(Vector3 direction)
            {
                var startPosition = _tinyHawk!.transform.position;
                var endPosition = _tinyHawk!.transform.position + direction * 4f;
                if (Physics.Linecast(startPosition + Vector3.up, endPosition + Vector3.up, out RaycastHit hit, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
                {
                    // Obstacle in the way
                    endPosition = hit.point - direction * 0.5f - Vector3.up;
                }

                _tinyHawk.agent.enabled = false;
                _tinyHawk.enabled = false;

                float timer = 0f, duration = 0.5f;
                while (timer < duration)
                {
                    timer += Time.deltaTime;
                    if(timer < duration / 2f)
                        endPosition.y += Time.deltaTime * 7f;
                    else
                        endPosition.y -= Time.deltaTime * 7f;

                    var newPosition = Vector3.Lerp(startPosition, endPosition, timer / duration);
                    _tinyHawk.transform.position = newPosition;
                    _tinyHawk.serverPosition = newPosition;

                    yield return null;
                }

                _tinyHawk.creatureAnimator.SetBool("sit", true); // _tinyHawk.EnemyEnterRestModeServerRpc(false, false);

                _tinyHawk.creatureSFX.PlayOneShot(_tinyHawk.enemyType.audioClips[5], 0.2f);
                yield return new WaitForSeconds(1f);

                _tinyHawk.EnemyGetUpServerRpc(); // _tinyHawk.creatureAnimator.SetBool("sit", false);

                _tinyHawk.enabled = true;
                _tinyHawk.agent.enabled = true;
            }
            #endregion
        }
        #endregion
    }
}
