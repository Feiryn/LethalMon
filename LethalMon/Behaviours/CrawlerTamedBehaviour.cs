using GameNetcodeStuff;
using System.Collections.Generic;
using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.Behaviours
{
    internal class CrawlerTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        private CrawlerAI? _crawler = null; // Replace with enemy class
        
        private CrawlerAI Crawler
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

        private const float CheckDoorsAndTurretsInterval = 1.5f;

        private const float CheckReachDestinationInterval = 0.25f;

        private const float MaxDoorAndTurretDistance = 30f;

        private const int MaxStuckTries = 30;

        private const int SmashNumberOfTimesSmallDoorLocked = 4;
        
        private const int SmashNumberOfTimesSmallDoorUnlocked = 2;

        private const int SmashNumberOfTimesBigDoor = 6;

        private const int SmashNumberOfTimesTurret = 4;

        private static readonly int SpeedMultiplier = Animator.StringToHash("speedMultiplier");
        #endregion
        
        #region Custom behaviours

        private enum CustomBehaviour
        {
            OpenSmallDoor = 1,
            OpenBigDoor = 2,
            DisableTurret = 3,
            GoToSmallDoor = 4,
            GoToBigDoor = 5,
            GoToTurret = 6
        }
        internal override List<Tuple<string, string, Action>> CustomBehaviourHandler => new()
        {
            new (CustomBehaviour.OpenSmallDoor.ToString(), "Smashes a closed door!", OpenSmallDoor),
            new (CustomBehaviour.OpenBigDoor.ToString(), "Smashes a secured door!", OnOpenBigDoor),
            new (CustomBehaviour.DisableTurret.ToString(), "Smashes a turret!", OnDisableTurret),
            new (CustomBehaviour.GoToSmallDoor.ToString(), "Saw a closed door!", OnGoToSmallDoor),
            new (CustomBehaviour.GoToBigDoor.ToString(), "Saw a secured door!", OnGoToBigDoor),
            new (CustomBehaviour.GoToTurret.ToString(), "Saw a turret!", OnGoToTurretDoor)
        };
        
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
                
                _targetSmallDoor = null;
                
                LethalMon.Log("Door opened!");
            });
        }
        
        private void OnOpenBigDoor()
        {
            if (SwitchToFollowingModeIfBigDoorInvalid() || _smashCoroutine != null)
                return;
            
            AttackAndSync(SmashNumberOfTimesBigDoor, () =>
            {
                // targetBigDoor.SetDoorOpenServerRpc(true);
                _targetBigDoor!.GetComponentInParent<TerminalAccessibleObject>().CallFunctionFromTerminal();
                
                _targetBigDoor = null;
                
                LethalMon.Log("Big door opened!");
            });
        }
        
        private void OnDisableTurret()
        {
            if (SwitchToFollowingModeIfTurretInvalid() || _smashCoroutine != null)
                return;
            
            AttackAndSync(SmashNumberOfTimesTurret, () =>
            {
                // targetTurret.ToggleTurretEnabled(false);
                _targetTurret!.GetComponentInParent<TerminalAccessibleObject>().CallFunctionFromTerminal();
            
                _targetTurret = null;
                
                LethalMon.Log("Turret disabled!");
            });
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
        #endregion

        #region Base Methods
        internal override void Start()
        {
            base.Start();

            if (ownerPlayer == null) return;
            
            Crawler.transform.localScale *= 0.5f;
            Crawler.openDoorSpeedMultiplier = 0f;

            Crawler.GetComponent<AudioSource>().pitch = 2f;
            // Crawler.GetComponentInChildren<PlayAudioAnimationEvent>().randomClips = new AudioClip[] { null! };
            Crawler.GetComponentInChildren<PlayAudioAnimationEvent>().audioToPlay.volume = 0.5f;
        }

        internal override void InitTamingBehaviour(TamingBehaviour behaviour)
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

        internal override void InitCustomBehaviour(int behaviour)
        {
            base.InitCustomBehaviour(behaviour);

            if (behaviour is >= (int)CustomBehaviour.GoToSmallDoor and <= (int)CustomBehaviour.GoToTurret)
            {
                _stuckTries = 0;
            }
        }

        internal override void OnTamedFollowing()
        {
            // OWNER ONLY
            base.OnTamedFollowing();
            
            if (_iaTimer >= CheckDoorsAndTurretsInterval)
            {
                _iaTimer = 0f;

                if (CheckForSmallDoor())
                    return;

                if (CheckForBigDoor())
                    return;

                CheckForTurret();
            }
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            // ANY CLIENT
            base.OnEscapedFromBall(playerWhoThrewBall);

            Crawler.SetDestinationToPosition(playerWhoThrewBall.transform.position);
        }

        internal override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            // ANY CLIENT
            base.OnUpdate(update, doAIInterval);

            _iaTimer += Time.deltaTime;
            
            var position = Crawler.transform.position;
            Crawler.creatureAnimator.SetFloat(SpeedMultiplier, Vector3.ClampMagnitude(position - Crawler.previousPosition, 1f).sqrMagnitude / (Time.deltaTime / 4f));
            var previousPosition = Crawler.previousPosition;
            Crawler.previousPosition = position;

            if (_smashCoroutine != null)
            {
                TurnTowardsPosition(_targetPos);
            }
            else if (Utils.IsHost && !IsCurrentBehaviourTaming(TamingBehaviour.TamedFollowing) && Vector3.Distance(position, previousPosition) < 0.0005f)
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

        #region CheckForTargetsMethods
        
        private void CheckForTurret()
        {
            var turrets = FindObjectsOfType<Turret>();
            foreach (var turret in turrets)
            {
                var turretPosition = turret.transform.position;
                if (turret.turretActive &&
                    Vector3.Distance(turretPosition, Crawler.transform.position) <= MaxDoorAndTurretDistance &&
                    !Physics.Linecast(Crawler.transform.position, turretPosition, out _,
                        StartOfRound.Instance.collidersAndRoomMaskAndDefault))
                {
                    _targetTurret = turret;
                    _targetPos = turretPosition;
                    SwitchToCustomBehaviour((int)CustomBehaviour.GoToTurret);
                    return;
                }
            }
        }

        private bool CheckForBigDoor()
        {
            var bigDoors = FindObjectsOfType<TerminalAccessibleObject>();
            foreach (var door in bigDoors)
            {
                var doorCollider = door.GetComponentInParent<Collider>();
                if (doorCollider == null)
                    continue;

                var doorPosition = doorCollider.transform.position;
                if (door is { isBigDoor: true, isDoorOpen: false } &&
                    Vector3.Distance(doorPosition, Crawler.transform.position) <= MaxDoorAndTurretDistance)
                {
                    Physics.Linecast(Crawler.transform.position, doorPosition, out RaycastHit hit,
                        StartOfRound.Instance.collidersAndRoomMaskAndDefault);
                    if (hit.collider == doorCollider)
                    {
                        _targetBigDoor = door;
                        _targetPos = doorPosition;
                        SwitchToCustomBehaviour((int)CustomBehaviour.GoToBigDoor);
                        return true;
                    }
                }
            }

            return false;
        }

        private bool CheckForSmallDoor()
        {
            var smallDoors = FindObjectsOfType<DoorLock>();
            foreach (var door in smallDoors)
            {
                var doorComponent = door.GetComponent<AnimatedObjectTrigger>();
                var doorPosition = doorComponent.transform.position;
                if (!door.isDoorOpened &&
                    Vector3.Distance(doorPosition, Crawler.transform.position) <= MaxDoorAndTurretDistance && !Physics.Linecast(
                        Crawler.transform.position, doorPosition, out _, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
                {
                    _targetSmallDoor = door;
                    _targetPos = doorPosition;
                    SwitchToCustomBehaviour((int)CustomBehaviour.GoToSmallDoor);
                    return true;
                }
            }

            return false;
        }
        #endregion

        #region Methods
        
        private IEnumerator SmashAnimationCoroutine(int smashTimes, Action callback)
        {
            Crawler.agent.speed = 0f;

            for (int i = 0; i < smashTimes; ++i)
            {
                yield return new WaitForSeconds(1f);
                Crawler.creatureAnimator.Play("Base Layer.Attack");
                RoundManager.PlayRandomClip(Crawler.creatureSFX, Crawler.hitWallSFX);
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
        #endregion
        
        #region RPCs

        public void AttackAndSync(int numberOfTimes, Action callback)
        {
            _smashCoroutine = StartCoroutine(SmashAnimationCoroutine(numberOfTimes, callback));
            
            AttackTargetServerRpc(numberOfTimes);
        }
        
        [ServerRpc(RequireOwnership = false)]
        public void AttackTargetServerRpc(int numberOfTimes)
        {
            // HOST ONLY
            AttackTargetClientRpc(_targetPos, numberOfTimes);
        }

        [ClientRpc]
        public void AttackTargetClientRpc(Vector3 targetPosition, int numberOfTimes)
        {
            // ANY CLIENT (HOST INCLUDED)
            if (Utils.IsHost) return;
            
            _targetPos = targetPosition;
            _smashCoroutine = StartCoroutine(SmashAnimationCoroutine(numberOfTimes, () => {}));
        }
        #endregion
    }
}
