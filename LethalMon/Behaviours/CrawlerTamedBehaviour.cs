using GameNetcodeStuff;
using System.Collections.Generic;
using System;
using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.Behaviours
{
    internal class CrawlerTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        private CrawlerAI? _crawler = null; // Replace with enemy class
        
        internal CrawlerAI Crawler
        {
            get
            {
                if (_crawler == null)
                    _crawler = (Enemy as CrawlerAI)!;

                return _crawler;
            }
        }
        
        private float _iaTimer = 0f;

        private Vector3 _targetPos = Vector3.zero;
        
        private DoorLock? _targetSmallDoor = null;
        
        private TerminalAccessibleObject? _targetBigDoor = null;
        
        private Turret? _targetTurret = null;

        private Coroutine? _smashCoroutine = null;
        
        private int _stuckTries = 0;
        
        private float _afraidTimer = 0f;
        
        private float _followEnemyTimer = 0f;
        
        private EnemyAI? _targetEnemy = null;

        private const float CheckDoorsAndTurretsInterval = 1.5f;

        private const float CheckReachDestinationInterval = 0.25f;

        private const float MaxDoorAndTurretDistance = 30f;

        private const int MaxStuckTries = 30;

        private const int SmashNumberOfTimesSmallDoorLocked = 5;
        
        private const int SmashNumberOfTimesSmallDoorUnlocked = 2;

        private const int SmashNumberOfTimesBigDoor = 12;

        private const int SmashNumberOfTimesTurret = 4;
        
        private const int CloseBigDoorAfterSeconds = 5;
        
        private const float AfraidTime = 5f;
        
        private const float FollowEnemyTime = 10f;

        private static readonly int SpeedMultiplier = Animator.StringToHash("speedMultiplier");

        public override float MaxFollowDistance => 150f;

        public override bool CanDefend => false;
        
        private DoorLock[]? _smallDoors = null;
        
        private TerminalAccessibleObject[]? _bigDoors = null;
        
        private Turret[]? _turrets = null;

        #endregion
        
        #region Custom behaviours

        private enum CustomBehaviour
        {
            OpenSmallDoor = 1,
            OpenBigDoor = 2,
            DisableTurret = 3,
            GoToSmallDoor = 4,
            GoToBigDoor = 5,
            GoToTurret = 6,
            AfraidOfTurret = 7,
            FollowEnemy = 8
        }
        public override List<Tuple<string, string, Action>> CustomBehaviourHandler =>
        [
            new (CustomBehaviour.OpenSmallDoor.ToString(), "Smashes a closed door!", OpenSmallDoor),
            new (CustomBehaviour.OpenBigDoor.ToString(), "Smashes a secured door!", OnOpenBigDoor),
            new (CustomBehaviour.DisableTurret.ToString(), "Smashes a turret!", OnDisableTurret),
            new (CustomBehaviour.GoToSmallDoor.ToString(), "Saw a closed door!", OnGoToSmallDoor),
            new (CustomBehaviour.GoToBigDoor.ToString(), "Saw a secured door!", OnGoToBigDoor),
            new (CustomBehaviour.GoToTurret.ToString(), "Saw a turret!", OnGoToTurretDoor),
            new (CustomBehaviour.AfraidOfTurret.ToString(), "Got scared by a turret!", OnAfraidOfTurret),
            new (CustomBehaviour.FollowEnemy.ToString(), "Follows an enemy...", OnFollowEnemy)
        ];
        
        private void OpenSmallDoor()
        {
            if (SwitchToFollowingModeIfSmallDoorInvalid() || _smashCoroutine != null)
                return;

            AttackAndSync(_targetSmallDoor!.isLocked ? SmashNumberOfTimesSmallDoorLocked : SmashNumberOfTimesSmallDoorUnlocked, () =>
            {
                if (_targetSmallDoor.isLocked)
                    _targetSmallDoor.UnlockDoorSyncWithServer();
                
                if (_targetSmallDoor.gameObject.TryGetComponent(out AnimatedObjectTrigger trigger))
                {
                    trigger.TriggerAnimationNonPlayer(false, true);
                }
                _targetSmallDoor.OpenDoorAsEnemyServerRpc();
                BreakDoorServerRpc(_targetSmallDoor.NetworkObject);
                
                _targetSmallDoor = null;
                
                LethalMon.Log("Door opened!");
            }, () =>
            {
                ChooseCloseEnemy(false);
            }, AttackType.SmallDoor);
        }
        
        private void OnOpenBigDoor()
        {
            if (SwitchToFollowingModeIfBigDoorInvalid() || _smashCoroutine != null)
                return;
            
            AttackAndSync(SmashNumberOfTimesBigDoor, () =>
            {
                // targetBigDoor.SetDoorOpenServerRpc(true);
                _targetBigDoor!.GetComponentInParent<TerminalAccessibleObject>().CallFunctionFromTerminal();

                StartCoroutine(CloseBigDoorAfterTime(_targetBigDoor));
                
                _targetBigDoor = null;
                
                LethalMon.Log("Big door opened!");
            }, () =>
            {
                ChooseCloseEnemy(false);
            }, AttackType.BigDoor);
        }
        
        private void OnDisableTurret()
        {
            if (SwitchToFollowingModeIfTurretInvalid() || _smashCoroutine != null)
                return;
            
            AttackAndSync(SmashNumberOfTimesTurret, () =>
            {
                _targetTurret!.SwitchTurretMode((int) TurretMode.Detection);
                _targetTurret.Update(); // Make it stop firing
                _targetTurret.GetComponentInParent<TerminalAccessibleObject>().CallFunctionFromTerminal();
            
                _targetTurret = null;
                
                LethalMon.Log("Turret disabled!");
            }, () =>
            {
                if (_targetTurret!.turretMode != TurretMode.Berserk)
                {
                    _targetTurret.SwitchTurretMode((int) TurretMode.Berserk);

                    if (UnityEngine.Random.Range(0f, 1f) < 0.5f)
                    {
                        _targetPos = Crawler.ChooseFarthestNodeFromPosition(_targetTurret.transform.position).position;
                        Crawler.SetDestinationToPosition(_targetPos);
                        _targetTurret = null;
                        _afraidTimer = 0f;
                        SwitchToCustomBehaviour((int) CustomBehaviour.AfraidOfTurret);
                        return;
                    }
                }
                
                ChooseCloseEnemy(false);
            }, AttackType.Turret);
        }

        private void OnGoToSmallDoor()
        {
            if (SwitchToFollowingModeIfSmallDoorInvalid())
                return;
            
            OnGoToTarget(2.5f, CustomBehaviour.OpenSmallDoor);  // Minimum: 2.07
        }
        
        private void OnGoToBigDoor()
        {
            if (SwitchToFollowingModeIfBigDoorInvalid())
                return;
            
            OnGoToTarget(2f, CustomBehaviour.OpenBigDoor);
        }
        
        private void OnGoToTurretDoor()
        {
            if (SwitchToFollowingModeIfTurretInvalid())
                return;
            
            OnGoToTarget(2f, CustomBehaviour.DisableTurret); // Minimum: 0.7321472
        }

        private void OnAfraidOfTurret()
        {
            _afraidTimer += Time.deltaTime;
            
            if (_afraidTimer >= AfraidTime)
            {
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
            }
        }

        private void OnFollowEnemy()
        {
            if (_targetEnemy == null || _followEnemyTimer >= FollowEnemyTime)
            {
                _targetEnemy = null;
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                return;
            }
            
            _followEnemyTimer += Time.deltaTime;
            
            FollowPosition(_targetEnemy.transform.position);
        }
        #endregion

        #region Base Methods
        public override void Start()
        {
            base.Start();

            if (ownerPlayer == null) return;
            
            Crawler.transform.localScale *= 0.8f;
            Crawler.openDoorSpeedMultiplier = 0f;

            Crawler.creatureVoice.pitch = 1.5f;
            // Crawler.GetComponentInChildren<PlayAudioAnimationEvent>().randomClips = new AudioClip[] { null! };
            Crawler.GetComponentInChildren<PlayAudioAnimationEvent>().audioToPlay.volume = 0.5f;
            
            Crawler.creatureAnimator.Play("Base Layer.CrawlSlow");
        }

        public override void InitTamingBehaviour(TamingBehaviour behaviour)
        {
            if (Crawler.agent != null)
            {
                Crawler.agent.speed = 8f;
            }
            
            if (behaviour == TamingBehaviour.TamedFollowing && _smashCoroutine != null)
            {
                StopCoroutine(_smashCoroutine);
                _smashCoroutine = null;
            }
        }

        public override void InitCustomBehaviour(int behaviour)
        {
            base.InitCustomBehaviour(behaviour);

            Crawler.agent.speed = 8f;
            
            if (behaviour is >= (int)CustomBehaviour.GoToSmallDoor and <= (int)CustomBehaviour.GoToTurret)
            {
                _stuckTries = 0;
            }
            else if (_smashCoroutine != null)
            {
                StopCoroutine(_smashCoroutine);
                _smashCoroutine = null;
            }
        }

        public override void OnTamedFollowing()
        {
            // OWNER ONLY
            base.OnTamedFollowing();
            
            if (_iaTimer >= CheckDoorsAndTurretsInterval)
            {
                _iaTimer = 0f;

                ChooseClosestTarget();
            }
        }

        public override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            // ANY CLIENT
            base.OnEscapedFromBall(playerWhoThrewBall);

            Crawler.SetDestinationToPosition(playerWhoThrewBall.transform.position);
        }

        public override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            // ANY CLIENT
            base.OnUpdate(update, doAIInterval);

            _iaTimer += Time.deltaTime;
            
            var position = Crawler.transform.position;
            Crawler.creatureAnimator.SetFloat(SpeedMultiplier, Vector3.ClampMagnitude(position - Crawler.previousPosition, 1f).sqrMagnitude / (Time.deltaTime / 4f));
            var previousPosition = Crawler.previousPosition;
            Crawler.previousPosition = position;
            int? currentCustomBehaviour = CurrentCustomBehaviour;

            if (_smashCoroutine != null)
            {
                TurnTowardsPosition(_targetPos);
            }
            else if (Utils.IsHost && currentCustomBehaviour is >= (int) CustomBehaviour.GoToSmallDoor and <= (int) CustomBehaviour.GoToTurret && Vector3.Distance(position, previousPosition) < 0.0005f)
            {
                _stuckTries++;
                LethalMon.Log("Incrementing stuck tries: " + _stuckTries);
                if (_stuckTries > MaxStuckTries)
                {
                    _stuckTries = 0;
                    _targetSmallDoor = null;
                    _targetBigDoor = null;
                    _targetTurret = null;
                    LethalMon.Log("Stuck, switching to following mode!");
                    SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                }
            }
        }

        public override bool CanBeTeleported()
        {
            // HOST ONLY
            return CurrentTamingBehaviour == TamingBehaviour.TamedFollowing;
        }
        #endregion

        #region Methods

        private bool ChooseCloseEnemy(bool checkChance)
        {
            if (!checkChance || UnityEngine.Random.Range(0f, 1f) < 0.15f)
            {
                EnemyAI? nearestEnemy = NearestEnemy(true, false);
                if (nearestEnemy != null)
                {
                    _targetEnemy = nearestEnemy;
                    _targetSmallDoor = null;
                    _targetBigDoor = null;
                    _targetTurret = null;
                    _followEnemyTimer = 0f;
                    SwitchToCustomBehaviour((int)CustomBehaviour.FollowEnemy);
                    return true;
                }
            }

            return false;
        }
        
        private void ChooseClosestTarget()
        {
            if (ChooseCloseEnemy(true))
                return;
            
            float closestDistance = float.MaxValue;
            DoorLock? closestSmallDoor = null;
            TerminalAccessibleObject? closestBigDoor = null;
            Turret? closestTurret = null;
            
            if (_smallDoors == null)
                _smallDoors = FindObjectsOfType<DoorLock>();
            foreach (var door in _smallDoors)
            {
                var doorComponent = Cache.GetDoorAnimatedObjectTrigger(door);
                if (doorComponent == null)
                    continue;
                
                var doorPosition = doorComponent.transform.position;
                if (!door.isDoorOpened)
                {
                    var distance = Vector3.Distance(doorPosition, Crawler.transform.position);
                    if (distance <= 0.5f ||
                        (distance <= MaxDoorAndTurretDistance &&
                        distance <= closestDistance &&
                        !Physics.Raycast(Crawler.transform.position, (doorPosition - Crawler.transform.position).normalized, out _, distance - 0.5f, StartOfRound.Instance.collidersAndRoomMaskAndDefault)))
                    {
                        closestDistance = distance;
                        closestSmallDoor = door;
                    }
                }
            }
            
            if (_bigDoors == null)
                _bigDoors = FindObjectsOfType<TerminalAccessibleObject>();
            foreach (var door in _bigDoors)
            {
                var doorCollider = Cache.GetTerminalAccessibleObjectCollider(door);
                if (doorCollider == null)
                    continue;

                var doorPosition = doorCollider.transform.position;
                if (door is { isBigDoor: true, isDoorOpen: false })
                {
                    var distance = Vector3.Distance(doorPosition, Crawler.transform.position);
                    if (distance <= MaxDoorAndTurretDistance &&
                        distance <= closestDistance)
                    {
                        Physics.Linecast(Crawler.transform.position, doorPosition, out RaycastHit hit,
                            StartOfRound.Instance.collidersAndRoomMaskAndDefault);
                        if (hit.collider == doorCollider)
                        {
                            closestDistance = distance;
                            closestBigDoor = door;
                            closestSmallDoor = null;
                        }
                    }
                }
            }
            
            if (_turrets == null)
                _turrets = FindObjectsOfType<Turret>();
            foreach (var turret in _turrets)
            {
                var turretPosition = turret.transform.position;
                if (turret.turretActive)
                {
                    var distance = Vector3.Distance(turretPosition, Crawler.transform.position);
                    if (turret.turretActive &&
                        distance <= MaxDoorAndTurretDistance &&
                        distance <= closestDistance &&
                        !Physics.Linecast(Crawler.transform.position, turretPosition, out _,
                            StartOfRound.Instance.collidersAndRoomMaskAndDefault))
                    {
                        closestDistance = distance;
                        closestTurret = turret;
                        closestBigDoor = null;
                        closestSmallDoor = null;
                    }
                }
            }
            
            if (closestSmallDoor != null)
            {
                _targetSmallDoor = closestSmallDoor;
                _targetPos = Cache.GetDoorAnimatedObjectTrigger(closestSmallDoor)!.transform.position;
                SwitchToCustomBehaviour((int)CustomBehaviour.GoToSmallDoor);
            }
            else if (closestBigDoor != null)
            {
                _targetBigDoor = closestBigDoor;
                _targetPos = Cache.GetTerminalAccessibleObjectCollider(closestBigDoor)!.transform.position;
                SwitchToCustomBehaviour((int)CustomBehaviour.GoToBigDoor);
            }
            else if (closestTurret != null)
            {
                _targetTurret = closestTurret;
                _targetPos = closestTurret.transform.position;
                SwitchToCustomBehaviour((int)CustomBehaviour.GoToTurret);
            }
        }
        
        private IEnumerator SmashAnimationCoroutine(int smashTimes, Action callback, Action smashAction)
        {
            Crawler.agent.speed = 0f;

            for (int i = 0; i < smashTimes; ++i)
            {
                yield return new WaitForSeconds(1f);
                Crawler.creatureAnimator.Play("Base Layer.Attack");
                RoundManager.PlayRandomClip(Crawler.creatureSFX, Crawler.hitWallSFX);
                smashAction.Invoke();
            }
            
            _smashCoroutine = null;
            callback.Invoke();
        }
        
                
        private void OnGoToTarget(float reachDistance, CustomBehaviour reachCustomBehaviour)
        {
            if (_iaTimer >= CheckReachDestinationInterval)
            {
                _iaTimer = 0f;
                if (Vector3.Distance(_targetPos, Crawler.transform.position) < reachDistance)
                {
                    SwitchToCustomBehaviour((int)reachCustomBehaviour);
                    LethalMon.Log("Target reached");
                }
            }

            Crawler.SetDestinationToPosition(_targetPos);
        }
        
        
        private void ClearTargetsAndSwitchToFollowing()
        {
            LethalMon.Log("Target invalid, switching to following mode!");
            _targetSmallDoor = null;
            _targetBigDoor = null;
            _targetTurret = null;
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
        }

        private bool SwitchToFollowingModeIfSmallDoorInvalid()
        {
            if (_targetSmallDoor != null && !_targetSmallDoor.isDoorOpened)
                return false;

            ClearTargetsAndSwitchToFollowing();
            return true;
        }
        
        private bool SwitchToFollowingModeIfBigDoorInvalid()
        {
            if (_targetBigDoor != null && !_targetBigDoor.isDoorOpen)
                return false;

            ClearTargetsAndSwitchToFollowing();
            return true;
        }
        
        private bool SwitchToFollowingModeIfTurretInvalid()
        {
            if (_targetTurret != null && _targetTurret.turretActive)
                return false;

            ClearTargetsAndSwitchToFollowing();
            return true;
        }

        private IEnumerator CloseBigDoorAfterTime(TerminalAccessibleObject bigDoor)
        {
            yield return new WaitForSeconds(CloseBigDoorAfterSeconds);
            if (bigDoor.isDoorOpen)
                bigDoor.CallFunctionFromTerminal();
        }
        #endregion
        
        #region RPCs

        public void AttackAndSync(int numberOfTimes, Action callback, Action smashAction, AttackType attackType)
        {
            _smashCoroutine = StartCoroutine(SmashAnimationCoroutine(numberOfTimes, callback, smashAction));
            
            AttackTargetServerRpc(numberOfTimes, attackType);
        }

        public enum AttackType
        {
            SmallDoor,
            BigDoor,
            Turret
        }
        
        [ServerRpc(RequireOwnership = false)]
        public void AttackTargetServerRpc(int numberOfTimes, AttackType attackType)
        {
            // HOST ONLY
            AttackTargetClientRpc(_targetPos, numberOfTimes, (int) attackType, attackType switch
            {
                AttackType.Turret => _targetTurret!.NetworkObject,
                AttackType.BigDoor => _targetBigDoor!.NetworkObject,
                _ => _targetSmallDoor!.NetworkObject
            });
        }

        [ClientRpc]
        public void AttackTargetClientRpc(Vector3 targetPosition, int numberOfTimes, int attackType, NetworkObjectReference target)
        {
            // ANY CLIENT (HOST INCLUDED)
            if (Utils.IsHost) return;
            
            _targetPos = targetPosition;
            
            target.TryGet(out NetworkObject networkObject);
            switch (attackType)
            {
                case (int) AttackType.Turret:
                    _targetTurret = FindObjectsOfType<Turret>().FirstOrDefault(t => t.NetworkObject == networkObject);
                    break;
                case (int) AttackType.BigDoor:
                    _targetBigDoor = FindObjectsOfType<TerminalAccessibleObject>().FirstOrDefault(d => d.NetworkObject == networkObject);
                    break;
                default:
                    _targetSmallDoor = FindObjectsOfType<DoorLock>().FirstOrDefault(d => d.NetworkObject == networkObject);
                    break;
            }
            _smashCoroutine = StartCoroutine(SmashAnimationCoroutine(numberOfTimes, () =>
            {
                if (attackType == (int) AttackType.Turret)
                {
                    _targetTurret!.turretModeLastFrame = TurretMode.Detection;
                    _targetTurret!.rotatingClockwise = false;
                    _targetTurret!.mainAudio.Stop();
                    _targetTurret!.farAudio.Stop();
                    _targetTurret!.berserkAudio.Stop();
                    if (_targetTurret!.fadeBulletAudioCoroutine != null)
                    {
                        StopCoroutine(_targetTurret!.fadeBulletAudioCoroutine);
                    }
                    _targetTurret!.fadeBulletAudioCoroutine = StartCoroutine(_targetTurret!.FadeBulletAudio());
                    _targetTurret!.bulletParticles.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
                    _targetTurret!.rotationSpeed = 28f;
                    _targetTurret!.rotatingSmoothly = true;
                    _targetTurret!.turretAnimator.SetInteger("TurretMode", 0);
                    _targetTurret!.turretAnimator.SetBool("turretActive", _targetTurret!.turretActive);
                    _targetTurret!.turretInterval = UnityEngine.Random.Range(0f, 0.15f);
                }
            }, () =>
            {
                if (_targetTurret!.turretMode != TurretMode.Berserk)
                {
                    _targetTurret.SwitchTurretMode((int)TurretMode.Berserk);
                }
            }));
        }

        [ServerRpc(RequireOwnership = false)]
        public void BreakDoorServerRpc(NetworkObjectReference door)
        {
            BreakDoorClientRpc(door);
        }
        
        [ClientRpc]
        public void BreakDoorClientRpc(NetworkObjectReference door)
        {
            if (door.TryGet(out NetworkObject networkObject))
            {
                DoorLock? doorLock = FindObjectsOfType<DoorLock>().FirstOrDefault(dl => dl.NetworkObject == networkObject);
                if (doorLock != null)
                {
                    doorLock.doorTrigger.interactable = false;
                    doorLock.doorTrigger.timeToHold = float.MaxValue;
                    doorLock.doorTrigger.disabledHoverTip = "Broken"; 
                    doorLock.navMeshObstacle.carving = true;
                    doorLock.navMeshObstacle.carveOnlyStationary = true;
                }
                else
                {
                    LethalMon.Log("Door not found", LethalMon.LogType.Warning);
                }
            }
            else
            {
                LethalMon.Log("Door NetworkObject not found", LethalMon.LogType.Warning);
            }
        }
        #endregion
    }
}
