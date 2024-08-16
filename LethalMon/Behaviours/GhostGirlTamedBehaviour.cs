using GameNetcodeStuff;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Collections;
using Unity.Netcode;
namespace LethalMon.Behaviours
{
    internal class GhostGirlTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        private DressGirlAI? _ghostGirl = null;
        internal DressGirlAI GhostGirl
        {
            get
            {
                if (_ghostGirl == null)
                    _ghostGirl = (Enemy as DressGirlAI)!;

                return _ghostGirl;
            }
        }

        private bool _isWalking = false;
        private Vector3 _previousPosition = Vector3.zero;

        private bool _ownerInsideFactory = false;

        private Coroutine? _scareAndHuntCoroutine = null;

        internal override string DefendingBehaviourDescription => "Saw an enemy to hunt!";

        #endregion

        #region Cooldowns

        private const string TeleportCooldownId = "dressgirl_tp";
    
        internal override Cooldown[] Cooldowns => [new Cooldown(TeleportCooldownId, "Attack enemy", ModConfig.Instance.values.DressGirlTeleportCooldown)];

        private CooldownNetworkBehaviour? teleportCooldown;

        internal override bool CanDefend => teleportCooldown != null && teleportCooldown.IsFinished();
        #endregion
        
        #region Custom behaviours
        internal enum CustomBehaviour
        {
            RunningBackToOwner = 1,
            ScareThrowerAndHunt
        }
        internal override List<Tuple<string, string, Action>>? CustomBehaviourHandler =>
        [
            new (CustomBehaviour.RunningBackToOwner.ToString(), "Runs back to you...", OnRunningBackToOwner),
            new (CustomBehaviour.ScareThrowerAndHunt.ToString(), "Is hunting you!", WhileScaringThrower)
        ];

        internal override void InitCustomBehaviour(int behaviour)
        {
            base.InitCustomBehaviour(behaviour);

            switch((CustomBehaviour)behaviour)
            {
                case CustomBehaviour.RunningBackToOwner:
                    _ownerInsideFactory = ownerPlayer!.isInsideFactory;
                    break;

                case CustomBehaviour.ScareThrowerAndHunt:
                    LethalMon.Log("InitCustomBehaviour ScareThrowerAndHunt", LethalMon.LogType.Warning);
                    if (Utils.IsHost && targetPlayer != null)
                    {
                        EnableEnemyMeshForTargetClientRpc(targetPlayer.playerClientId, true);
                        _scareAndHuntCoroutine = GhostGirl.StartCoroutine(ScareThrowerAndHunt());
                    }
                    break;

                default:
                    break;
            }
        }

        public void OnRunningBackToOwner()
        {
            LethalMon.Log("OnRunningBackToOwner: " + GhostGirl.transform.position);

            AnimateWalking();

            if (ownerPlayer == null || ownerPlayer.isPlayerDead
                || DistanceToOwner < 8f // Reached owner
                || _ownerInsideFactory != ownerPlayer.isInsideFactory) // Owner left/inserted factory
            {
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, false);
                return;
            }

            GhostGirl.agent.speed = 8f;

            GhostGirl.SetDestinationToPosition(ownerPlayer.transform.position);
        }

        public void WhileScaringThrower()
        {
            if (!Utils.IsHost) return;

            if (targetPlayer == null || targetPlayer.isPlayerDead)
            {
                LethalMon.Log("Stopping ScareThrowerAndHunt", LethalMon.LogType.Warning);
                if (_scareAndHuntCoroutine != null)
                    GhostGirl.StopCoroutine(_scareAndHuntCoroutine);

                GhostGirl.hauntingPlayer = null;
                targetPlayer = null;
                SwitchToDefaultBehaviour(0);
            }
        }

        internal IEnumerator ScareThrowerAndHunt()
        {
            if (targetPlayer == null) yield break;

            EnableEnemyMeshForTargetClientRpc(targetPlayer.playerClientId, true);
            GhostGirl.creatureSFX.Stop();
            GhostGirl.enabled = false;

            var targetingUs = targetPlayer == Utils.CurrentPlayer;

            // Phase 1: Stare and turn towards player
            #region Phase 1
            GhostGirl.agent.speed = 0f;
            if (targetingUs)
                GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.2f);

            RoundManager.PlayRandomClip(GhostGirl.creatureVoice, GhostGirl.appearStaringSFX);

            float timeTillStart = 3f;
            while (timeTillStart > 0f)
            {
                TurnTowardsPosition(targetPlayer.transform.position);
                timeTillStart -= Time.deltaTime;
                yield return null;
            }
            RoundManager.Instance.FlickerLights(true, true);
            #endregion

            // Phase 2: Fake attempts to scare the player
            #region Phase 2
            GhostGirl.hauntingPlayer = targetPlayer;
            GhostGirl.hauntingLocalPlayer = targetPlayer == Utils.CurrentPlayer;

            int fakeAttempts = 1;
            while (fakeAttempts <= 3)
            {
                if (targetingUs)
                    GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.2f + fakeAttempts * 0.1f);

                fakeAttempts++;

                if (!WarpToHauntPosition())
                {
                    LethalMon.Log("GhostGirl.ScareThrowerAndHunt: Unable to find next haunt position.", LethalMon.LogType.Warning);
                    continue;
                }

                bool playerHasSeenGhostGirl = false;
                float timeSinceAttempt = 0f;
                yield return new WaitUntil(() =>
                {
                    var distanceTowardsPlayer = DistanceToTargetPlayer;
                    if (distanceTowardsPlayer > 2f)
                    {
                        GhostGirl.SetDestinationToPosition(targetPlayer.transform.position);
                    }
                    else
                    {
                        var angleTowardsOwner = (GhostGirl.transform.position - targetPlayer.transform.position).normalized;
                        GhostGirl.SetDestinationToPosition(targetPlayer.transform.position + angleTowardsOwner);
                    }
                    TurnTowardsPosition(targetPlayer.transform.position);

                    if(!playerHasSeenGhostGirl)
                        playerHasSeenGhostGirl = targetPlayer.HasLineOfSightToPosition(GhostGirl.transform.position, 60f);

                    timeSinceAttempt += Time.deltaTime;
                    GhostGirl.agent.speed = Mathf.Max(10f - distanceTowardsPlayer, 1f) + timeSinceAttempt / 3f; // Faster the longer it takes

                    return playerHasSeenGhostGirl && distanceTowardsPlayer < 2f;
                });

                int num = UnityEngine.Random.Range(0, GhostGirl.appearStaringSFX.Length);
                Utils.PlaySoundAtPosition(GhostGirl.transform.position, GhostGirl.appearStaringSFX[num]);
            }
            #endregion

            // Phase 3: Hunt
            #region Phase 3
            GhostGirl.agent.speed = 0f;
            GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(1.9f);
            RoundManager.Instance.FlickerLights(true, true);
            var location = RoundManager.Instance.GetNavMeshPosition(targetPlayer.transform.position + targetPlayer.transform.forward * 5f, GhostGirl.navHit);
            if (GhostGirl.navHit.hit)
                GhostGirl.agent.Warp(location);
            else
                WarpToHauntPosition();

            timeTillStart = 3f;
            while (timeTillStart > 0f)
            {
                TurnTowardsPosition(targetPlayer.transform.position);
                timeTillStart -= Time.deltaTime;
                yield return null;
            }

            GhostGirl.enabled = true;
            GhostGirl.BeginChasing();
            #endregion
        }
        #endregion

        #region Action Keys
#if DEBUG
        private readonly List<ActionKey> _actionKeys =
        [
            new ActionKey() { Key = ModConfig.Instance.ActionKey1, Description = "Teleport to Ghost Girl" }
        ];
        internal override List<ActionKey> ActionKeys => _actionKeys;

        internal override void ActionKey1Pressed()
        {
            base.ActionKey1Pressed();

            if (IsTamed && CurrentCustomBehaviour == (int)CustomBehaviour.RunningBackToOwner)
            {
                ownerPlayer!.TeleportPlayer(GhostGirl.transform.position);
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
            }
        }
#endif
        #endregion

        #region Base Methods     
        internal override void Start()
        {
            base.Start();

            teleportCooldown = GetCooldownWithId(TeleportCooldownId);
        }

        internal override void LateUpdate()
        {
            base.LateUpdate();

            AnimateWalking();
        }

        internal override void InitTamingBehaviour(TamingBehaviour behaviour)
        {
            base.InitTamingBehaviour(behaviour);

            if(behaviour == TamingBehaviour.TamedDefending)
            {
                LethalMon.Log("GhostGirl: Play breathingSFX");
                GhostGirl.creatureVoice.clip = GhostGirl.breathingSFX;
                GhostGirl.creatureVoice.Play();
            }
        }

        internal override void OnTamedFollowing()
        {
            base.OnTamedFollowing();

            if (!IsTamed) return;

            GhostGirl.agent.speed = (ownerPlayer!.isSprinting || DistanceToOwner > 5f) ? 6f : 3f;

            if (!GhostGirl.enemyMeshEnabled)
            {
                EnableEnemyMeshForTargetServerRpc(OwnerID, true);
                GhostGirl.enemyMeshEnabled = true;
            }

            if (teleportCooldown != null && teleportCooldown.IsFinished())
            {
                TargetNearestEnemy();
            }
        }

        internal override void OnTamedDefending()
        {
            base.OnTamedDefending();

            if (!IsTamed) return;

            if (!HasTargetEnemy || targetEnemy!.isEnemyDead || DistanceToOwner > 30f)
            {
                targetEnemy = null;
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                return;
            }

            if (IsCollidingWithTargetEnemy)
            {
                LethalMon.Log("GhostGirlTamedBehaviour: Teleporting enemy.");

                OnHitTargetEnemyServerRpc(targetEnemy.NetworkObject);
                SwitchToCustomBehaviour((int)CustomBehaviour.RunningBackToOwner);
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true);
            }
            else
            {
                LethalMon.Log("GhostGirlTamedBehaviour: Moving to target");
                GhostGirl.agent.speed = 5.25f;
                GhostGirl.SetDestinationToPosition(targetEnemy.transform.position);
                GhostGirl.creatureVoice.volume = Mathf.Max((20f - DistanceToTargetEnemy) / 15f, 0f);
            }
        }
        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            base.OnEscapedFromBall(playerWhoThrewBall);

            if (playerWhoThrewBall == null) return;

            targetPlayer = playerWhoThrewBall;
            if(Utils.IsHost)
                SwitchToCustomBehaviour((int)CustomBehaviour.ScareThrowerAndHunt);
        }
        #endregion

        #region Methods
        internal void AnimateWalking()
        {
            var currentlyWalking = Vector3.Distance(_previousPosition, GhostGirl.transform.position) > Mathf.Epsilon;
            if (currentlyWalking != _isWalking)
            {
                _isWalking = currentlyWalking;
                GhostGirl.creatureAnimator.SetBool("Walk", value: _isWalking);
            }

            _previousPosition = GhostGirl.transform.position;
        }

        internal IEnumerator FlickerLightsAndTurnDownBreaker()
        {
            RoundManager.Instance.FlickerLights(flickerFlashlights: true, disableFlashlights: true);
            yield return new WaitForSeconds(1f);
            TurnOffBreakerNearbyServerRpc();
        }
        internal Vector3 ChooseFarthestPosition()
        {
            if (GhostGirl.allAINodes == null || GhostGirl.allAINodes.Length == 0)
                return RoundManager.Instance.GetRandomNavMeshPositionInRadius(GhostGirl.transform.position, 80f);

            return GhostGirl.ChooseFarthestNodeFromPosition(GhostGirl.transform.position).position;
        }

        internal bool WarpToHauntPosition()
        {
            var newPosition = GhostGirl.TryFindingHauntPosition(staringMode: false, mustBeInLOS: true);
            if (newPosition == Vector3.zero)
                newPosition = GhostGirl.TryFindingHauntPosition(staringMode: false, mustBeInLOS: false);

            if (newPosition != Vector3.zero)
            {
                GhostGirl.agent.Warp(newPosition);
                return true;
            }

            return false;
        }
        #endregion

        #region RPCs
        [ServerRpc(RequireOwnership = false)]
        public void TurnOffBreakerNearbyServerRpc() // Original FlipLightsBreakerServerRpc isn't calling the correct ClientRpc.. zeekerss pls :p
        {
            TurnOffBreakerNearbyClientRpc();
        }

        [ClientRpc]
        public void TurnOffBreakerNearbyClientRpc()
        {
            var breakerBoxList = FindObjectsOfType<BreakerBox>();
            foreach(var breakerBox in breakerBoxList)
            {
                if (breakerBox != null && Vector3.Distance(breakerBox.transform.position, GhostGirl.transform.position) < 35f)
                {
                    breakerBox.SetSwitchesOff();
                    breakerBox.thisAudioSource.PlayOneShot(breakerBox.switchPowerSFX);
                }
            }
        }
        
        [ServerRpc(RequireOwnership = false)]
        public void OnHitTargetEnemyServerRpc(NetworkObjectReference enemyRef)
        {
            var farthestNode = ChooseFarthestPosition();
            OnHitTargetEnemyClientRpc(enemyRef, farthestNode);
        }

        [ClientRpc]
        public void OnHitTargetEnemyClientRpc(NetworkObjectReference enemyRef, Vector3 newEnemyPosition)
        {
            if (!enemyRef.TryGet(out NetworkObject networkObject) || !networkObject.TryGetComponent(out targetEnemy) || targetEnemy == null)
            {
                LethalMon.Log("OnHitTargetEnemyClientRpc: Unable to get enemy object.", LethalMon.LogType.Error);
                return;
            }

            GhostGirl.creatureVoice.Stop();

            GhostGirl.StartCoroutine(TeleportAndDamage(targetEnemy, newEnemyPosition, () => targetEnemy = null));

            RoundManager.Instance.StartCoroutine(FlickerLightsAndTurnDownBreaker());
        }

        private IEnumerator TeleportAndDamage(EnemyAI enemyAI, Vector3 newPosition, Action? onComplete = null)
        {
            teleportCooldown?.Reset();
                
            // Effects
            if(enemyAI.enemyType.canDie)
            {
                DropBlood(GhostGirl.transform.position);
                if (enemyAI.isEnemyDead && enemyAI.dieSFX != null)
                    Utils.PlaySoundAtPosition(GhostGirl.transform.position, enemyAI.dieSFX, 0.5f);
                Utils.PlaySoundAtPosition(GhostGirl.transform.position, StartOfRound.Instance.bloodGoreSFX);
            }

            // Teleport
            TeleportEnemy(GhostGirl, newPosition);
            TeleportEnemy(enemyAI, newPosition);

            // Damage
            if (enemyAI.enemyType.canDie)
            {
                yield return new WaitForSeconds(0.1f);
                LethalMon.Log("Damaging enemy after teleporting.");
                enemyAI.HitEnemyOnLocalClient(force: 1);
            }

            onComplete?.Invoke();
        }

        [ServerRpc(RequireOwnership = false)]
        public void EnableEnemyMeshForTargetServerRpc(ulong targetPlayerID, bool enable = true)
        {
            EnableEnemyMeshForTargetClientRpc(targetPlayerID, enable);
        }

        [ClientRpc]
        public void EnableEnemyMeshForTargetClientRpc(ulong targetPlayerID, bool enable = true)
        {
            GhostGirl.EnableEnemyMesh(enable && targetPlayerID == Utils.CurrentPlayerID, true);
        }
        #endregion
    }
}
