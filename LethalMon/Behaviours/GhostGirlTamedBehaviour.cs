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

        private int _fakeHunts = 0;
        private bool _playerHasSeenGhostGirlInFakeHunt = false;
        private float _fakeHuntStartTime = 0f;

        private bool TargetingUs => targetPlayer == Utils.CurrentPlayer;

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
            EscapePhaseStare,
            EscapePhaseFakeHunt,
            EscapePhaseHunt
        }
        internal override List<Tuple<string, string, Action>>? CustomBehaviourHandler =>
        [
            new (CustomBehaviour.RunningBackToOwner.ToString(), "Runs back to you...", OnRunningBackToOwner),
            new (CustomBehaviour.EscapePhaseStare.ToString(), "Is watching you!", OnEscapePhaseStare),
            new (CustomBehaviour.EscapePhaseFakeHunt.ToString(), "Is hunting you?", OnEscapePhaseFakeHunt),
            new (CustomBehaviour.EscapePhaseHunt.ToString(), "Is hunting you!", OnEscapePhaseHunt)
        ];

        internal override void InitCustomBehaviour(int behaviour)
        {
            base.InitCustomBehaviour(behaviour);

            //LethalMon.Log("InitCustomBehaviour: " + ((CustomBehaviour)behaviour).ToString());

            switch ((CustomBehaviour)behaviour)
            {
                case CustomBehaviour.RunningBackToOwner:
                    _ownerInsideFactory = ownerPlayer!.isInsideFactory;
                    break;

                case CustomBehaviour.EscapePhaseStare:      // Phase 1
                    if (targetPlayer == null) return;

                    //LethalMon.Log("Target: " +  targetPlayer.name + " / Is us? " + (TargetingUs ? "yes" : "no"));
                    GhostGirl.EnableEnemyMesh(TargetingUs, true);
                    GhostGirl.enemyMeshEnabled = TargetingUs;
                    GhostGirl.creatureSFX.Stop();

                    if (GhostGirl.agent != null)
                        GhostGirl.agent.speed = 0f;

                    if (TargetingUs)
                    {
                        GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.2f);
                        PlayRandomSoundForTarget();
                    }

                    LethalMon.Log("Current owner: " + Enemy.currentOwnershipOnThisClient + " / " + Enemy.IsOwner);

                    if (Enemy.IsOwner)
                    {
                        PlaceOnNavMesh();
                        Invoke(nameof(StartFakeHunt), 3f);
                    }
                    break;

                case CustomBehaviour.EscapePhaseFakeHunt:   // Phase 2
                    if (targetPlayer == null) return;

                    if(TargetingUs)
                    {
                        if (_playerHasSeenGhostGirlInFakeHunt || _fakeHunts == 0)
                        {
                            GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(0.2f + (_fakeHunts + 1f) * 0.1f);
                            PlayRandomSoundForTarget();
                        }

                        if (_fakeHunts == 0)
                            RoundManager.Instance.FlickerLights(true, true);
                    }

                    if (Enemy.IsOwner)
                    {
                        if (!WarpToHauntPosition())
                        {
                            LethalMon.Log("GhostGirl.ScareThrowerAndHunt: Unable to find next haunt position.", LethalMon.LogType.Warning);
                            SwitchToCustomBehaviour((int)CustomBehaviour.EscapePhaseHunt);
                            return;
                        }
                    }

                    _playerHasSeenGhostGirlInFakeHunt = false;
                    _fakeHuntStartTime = Time.realtimeSinceStartup;
                    _fakeHunts++;
                    break;

                case CustomBehaviour.EscapePhaseHunt:       // Phase 3
                    _playerHasSeenGhostGirlInFakeHunt = false;
                    _fakeHunts = 0;

                    if (targetPlayer == null) return;

                    if(TargetingUs)
                    {
                        PlayRandomSoundForTarget();
                        GameNetworkManager.Instance.localPlayerController.JumpToFearLevel(1.9f);
                        RoundManager.Instance.FlickerLights(true, true);
                    }

                    if (Enemy.IsOwner)
                    {
                        if (GhostGirl.agent != null)
                            GhostGirl.agent.speed = 0f;

                        var location = RoundManager.Instance.GetNavMeshPosition(targetPlayer.transform.position + targetPlayer.transform.forward * 5f, GhostGirl.navHit);
                        if (GhostGirl.navHit.hit)
                            Teleport(location, true, true);
                        else
                            WarpToHauntPosition();

                        Invoke(nameof(StartHuntServerRpc), 3f);
                    }

                    break;

                default:
                    break;
            }
        }

        internal void PlayRandomSoundForTarget()
        {
            if (!TargetingUs) return;

            int num = UnityEngine.Random.Range(0, GhostGirl.appearStaringSFX.Length);
            Utils.PlaySoundAtPosition(Utils.CurrentPlayer.transform.position, GhostGirl.appearStaringSFX[num]);
        }

        public void OnRunningBackToOwner()
        {
            //LethalMon.Log("OnRunningBackToOwner: " + GhostGirl.transform.position);

            if(ownerPlayer == Utils.CurrentPlayer)
                AnimateWalking();

            if (ownerPlayer == null || ownerPlayer.isPlayerDead
                || DistanceToOwner < 8f // Reached owner
                || _ownerInsideFactory != ownerPlayer.isInsideFactory) // Owner left/inserted factory
            {
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, false);
                return;
            }

            if (GhostGirl.agent != null)
            {
                GhostGirl.agent.speed = 8f;
                GhostGirl.SetDestinationToPosition(ownerPlayer.transform.position);
            }
        }

        public void StartEscapeStare() => SwitchToCustomBehaviour((int)CustomBehaviour.EscapePhaseStare);
        public void OnEscapePhaseStare()
        {
            if(!EscapePhaseCondition)
            {
                ExitEscapePhase();
                return;
            }

            TurnTowardsPosition(targetPlayer!.transform.position);
        }

        internal void StartFakeHunt() => SwitchToCustomBehaviour((int)CustomBehaviour.EscapePhaseFakeHunt);

        public void OnEscapePhaseFakeHunt()
        {
            if (!EscapePhaseCondition)
            {
                ExitEscapePhase();
                return;
            }

            if (TargetingUs)
                TurnTowardsPosition(targetPlayer!.transform.position);

            if (!Enemy.IsOwner) return;

            PlaceOnNavMesh();

            var distanceTowardsPlayer = DistanceToTargetPlayer;
            if (distanceTowardsPlayer > 2f)
            {
                GhostGirl.SetDestinationToPosition(targetPlayer!.transform.position);
            }
            else
            {
                var angleTowardsTarget = (GhostGirl.transform.position - targetPlayer!.transform.position).normalized;
                GhostGirl.SetDestinationToPosition(targetPlayer.transform.position + angleTowardsTarget);
            }

            if (!_playerHasSeenGhostGirlInFakeHunt)
                _playerHasSeenGhostGirlInFakeHunt = targetPlayer.HasLineOfSightToPosition(GhostGirl.transform.position, 60f);

            var timeSinceFakeHunt = Time.realtimeSinceStartup - _fakeHuntStartTime;

            // base speed + speed over time + jumpscare (faster the long it took)
            GhostGirl.agent.speed = 3f + timeSinceFakeHunt / 5f + Mathf.Max(12f - distanceTowardsPlayer, 0.5f) * Mathf.Max(timeSinceFakeHunt / 10f, 1f);

            //LethalMon.Log("Girl speed: " + GhostGirl.agent.speed + " (Girl distance to player: " + distanceTowardsPlayer + " / timeSinceHunt: " + timeSinceFakeHunt + ")");

            if ((_playerHasSeenGhostGirlInFakeHunt && distanceTowardsPlayer < 2f) || timeSinceFakeHunt > 10f)
                HitTargetPlayerOnFakeHunt();
        }

        public void HitTargetPlayerOnFakeHunt()
        {
            //LethalMon.Log($"Fake hunt {_fakeHunts} ended.");
            if (_fakeHunts >= 3)
                SwitchToCustomBehaviour((int)CustomBehaviour.EscapePhaseHunt);
            else
                StartNextFakeHuntServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        internal void StartNextFakeHuntServerRpc() => StartNextFakeHuntClientRpc();

        [ClientRpc]
        internal void StartNextFakeHuntClientRpc() => InitCustomBehaviour((int)CustomBehaviour.EscapePhaseFakeHunt);

        public void OnEscapePhaseHunt()
        {
            if (!EscapePhaseCondition)
            {
                ExitEscapePhase();
                return;
            }

            TurnTowardsPosition(targetPlayer!.transform.position);
        }

        public bool EscapePhaseCondition => targetPlayer != null && !targetPlayer.isPlayerDead;

        public void ExitEscapePhase()
        {
            LethalMon.Log("GhostGirl.ExitEscapePhase");
            _playerHasSeenGhostGirlInFakeHunt = false;
            _fakeHunts = 0;
            targetPlayer = null;
            GhostGirl.enabled = true;
            SwitchToDefaultBehaviour(0);
        }

        [ServerRpc(RequireOwnership = false)]
        internal void StartHuntServerRpc()
        {
            //LethalMon.Log("StartHuntServerRpc");
            if (targetPlayer == null)
            {
                LethalMon.Log("targetPlayer is null at start of ghost girl hunt.", LethalMon.LogType.Warning);
                SwitchToDefaultBehaviour(0);
                return;
            }

            StartHuntClientRpc();
        }

        [ClientRpc]
        internal void StartHuntClientRpc()
        {
            //LethalMon.Log("StartHuntClientRpc");
            GhostGirl.hauntingPlayer = targetPlayer;
            GhostGirl.hauntingLocalPlayer = targetPlayer == Utils.CurrentPlayer;

            GhostGirl.enabled = true;
            if(TargetingUs)
                GhostGirl.BeginChasing();
            else
                GhostGirl.SwitchToBehaviourStateOnLocalClient(1);
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

            if (IsTamed)
            {
                if (!IsOwnerPlayer)
                    MuteGhostGirl();

                GhostGirl.EnableEnemyMesh(IsOwnerPlayer, true);
                GhostGirl.enemyMeshEnabled = IsOwnerPlayer;
            }
#if DEBUG
            else
                Utils.CallNextFrame(ShowForHauntingPlayer);
#endif

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

            if (DistanceToTargetEnemy < 2f)
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

            LethalMon.Log("GhostGirl.OnEscapedFromBall");
            targetPlayer = playerWhoThrewBall;

            if (playerWhoThrewBall != Utils.CurrentPlayer)
                MuteGhostGirl();

            GhostGirl.enabled = false;

            if (Enemy.IsOwner)
            {
                GhostGirl.ChangeOwnershipOfEnemy(playerWhoThrewBall.actualClientId); // Change it so that the host can control it during the event
                Utils.CallNextFrame(StartEscapeStare);
            }
        }
#endregion

        #region Methods
        internal void MuteGhostGirl()
        {
            GhostGirl.creatureVoice.Stop();
            GhostGirl.creatureSFX.volume = 0;
            GhostGirl.heartbeatMusic.volume = 0;
        }

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
            if (GhostGirl.hauntingPlayer == null) return false;

            GhostGirl.hauntingPlayer.isInsideFactory = GhostGirl.hauntingPlayer.transform.position.y < -50f;
            LethalMon.Log("WarpToHauntPosition");
            var newPosition = GhostGirl.TryFindingHauntPosition(staringMode: false, mustBeInLOS: true);
            if (newPosition == Vector3.zero)
                newPosition = GhostGirl.TryFindingHauntPosition(staringMode: false, mustBeInLOS: false);

            if (newPosition != Vector3.zero)
            {
                Teleport(newPosition, true, true);
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
            Teleport(newPosition);
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
            var isEnabled = enable && targetPlayerID == Utils.CurrentPlayerID;
            GhostGirl.EnableEnemyMesh(isEnabled, true);
            GhostGirl.enemyMeshEnabled = isEnabled;
        }
        #endregion

#if DEBUG
        #region DEBUG
        internal void HauntPlayer(PlayerControllerB player) // DEBUG
        {
            GhostGirl.hauntingPlayer = player;
            if (GhostGirl.hauntingPlayer.isPlayerDead)
            {
                for (int k = 0; k < StartOfRound.Instance.allPlayerScripts.Length; k++)
                {
                    if (!StartOfRound.Instance.allPlayerScripts[k].isPlayerDead)
                    {
                        GhostGirl.hauntingPlayer = StartOfRound.Instance.allPlayerScripts[k];
                        break;
                    }
                }
            }
            Utils.CallNextFrame(HauntTargetPlayer);
        }

        internal void HauntTargetPlayer() // DEBUG
        {
            GhostGirl.ChangeOwnershipOfEnemy(GhostGirl.hauntingPlayer.actualClientId);
            GhostGirl.hauntingLocalPlayer = GameNetworkManager.Instance.localPlayerController == GhostGirl.hauntingPlayer;
            if (GhostGirl.switchHauntedPlayerCoroutine != null)
            {
                StopCoroutine(GhostGirl.switchHauntedPlayerCoroutine);
            }
            GhostGirl.switchHauntedPlayerCoroutine = StartCoroutine(GhostGirl.setSwitchingHauntingPlayer());
        }

        internal void ShowForHost() // DEBUG
        {
            GhostGirl.EnableEnemyMesh(Utils.IsHost, true);
            GhostGirl.enemyMeshEnabled = Utils.IsHost;
            GhostGirl.agent.speed = 0f;
        }

        internal void ShowForHauntingPlayer() // DEBUG
        {
            var isEnabled = Utils.CurrentPlayer == GhostGirl.hauntingPlayer;
            GhostGirl.EnableEnemyMesh(isEnabled, true);
            GhostGirl.enemyMeshEnabled = isEnabled;
            GhostGirl.agent.speed = 0f;
        }
        #endregion
#endif
    }
}
