using GameNetcodeStuff;
using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace LethalMon.Behaviours
{
    internal class EnemyController : NetworkBehaviour
    {
        #region Properties
        private EnemyAI? _enemy = null;

        private PlayerControllerB? playerControlledBy = null;

        // Stamina
        private float _controllingPlayerStamina = 0f;
        private Color _staminaDefaultColor;
        internal float EnemyStamina { get; private set; } = 1f;

        private bool _inputsBinded = false;

        internal bool IsSprinting { get; private set; } = false;
        internal bool IsMoving { get; private set; } = false;

        private Vector3 _lastDirection = Vector3.zero;
        internal float CurrentSpeed { get; private set; } = 0f;

        internal bool IsPlayerControlled => playerControlledBy != null;
        internal bool IsControlledByUs => IsPlayerControlled && playerControlledBy == Utils.CurrentPlayer || _inputsBinded;

        // Changeable variables
        internal float EnemySpeedInside = 4f;
        internal float EnemySpeedOutside = 6f;
        internal float EnemyJumpForce = 10f;
        internal float EnemyDuration = 5f; // General stamina duration
        internal float EnemyStrength = 1f; // Defines how much the enemy stamina is affected by held items
        internal bool EnemyCanJump = false;
        internal bool EnemyCanFly = false;
        internal float EnemyStaminaUseMultiplier = 1f;
        internal Vector3 EnemyOffsetWhileControlling = Vector3.zero; // TODO: transform parenting

        private readonly InputAction _moveAction = IngamePlayerSettings.Instance.playerInput.actions.FindAction("Move");

        // Trigger
        internal virtual float ControlTriggerHoldTime => 1f;

        private InteractTrigger? _controlTrigger = null;
        private Vector3 _triggerCenterDistance = Vector3.zero; // TODO: transform parenting
        private GameObject? _triggerObject = null;
        #endregion

        #region Controlling methods
        internal Action? OnStartControlling = null;
        internal Action? OnStopControlling = null;
        internal Func<Vector2, Vector3> OnCalculateMovementVector;
        internal Action<Vector3> OnMove;
        //internal Action? OnStartMoving;
        //internal Action? OnStopMoving;
        internal Action OnJump;
        internal Action OnCrouch;
        #endregion

        public EnemyController()
        {
            OnCalculateMovementVector = CalculateMovementVector;
            OnMove = Moving;
            OnJump = Jumping;
            OnCrouch = Crouching;
        }

        void Awake()
        {
            if (!gameObject.TryGetComponent(out _enemy))
                LethalMon.Log("EnemyController: Unable to get enemy object.", LethalMon.LogType.Error);
        }

        #region Methods
        public void AddTrigger(string hoverTip = "Control")
        {
            if (_enemy?.transform == null || _controlTrigger != null) return;
            LethalMon.Log("Adding riding trigger.");

            if(!Utils.TryGetRealEnemyBounds(_enemy, out Bounds bounds))
            {
                LethalMon.Log("Unable to get enemy bounds. No MeshRenderer found.", LethalMon.LogType.Error);
                return;
            }

            _triggerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _triggerObject.transform.position = bounds.center;
            _triggerCenterDistance = _enemy!.transform.position - bounds.center;
            _triggerObject.transform.localScale = bounds.size;
            Physics.IgnoreCollision(_triggerObject.GetComponent<BoxCollider>(), Utils.CurrentPlayer.playerCollider);
            //triggerObject.transform.SetParent(enemy.gameObject.transform, false); // damn parenting not working...

            _triggerObject.tag = "InteractTrigger";
            _triggerObject.layer = LayerMask.NameToLayer("InteractableObject");

            _controlTrigger = _triggerObject.AddComponent<InteractTrigger>();
            _controlTrigger.interactable = true;
            _controlTrigger.hoverIcon = GameObject.Find("StartGameLever")?.GetComponent<InteractTrigger>()?.hoverIcon;
            _controlTrigger.hoverTip = hoverTip;
            _controlTrigger.oneHandedItemAllowed = true;
            _controlTrigger.twoHandedItemAllowed = true;
            _controlTrigger.holdInteraction = true;
            _controlTrigger.touchTrigger = false;
            _controlTrigger.timeToHold = ControlTriggerHoldTime;
            _controlTrigger.timeToHoldSpeedMultiplier = 1f;

            _controlTrigger.holdingInteractEvent = new InteractEventFloat();
            _controlTrigger.onInteract = new InteractEvent();
            _controlTrigger.onInteractEarly = new InteractEvent();
            _controlTrigger.onStopInteract = new InteractEvent();
            _controlTrigger.onCancelAnimation = new InteractEvent();

            _controlTrigger.onInteract.AddListener((player) => StartControllingServerRpc(player.NetworkObject));

            _controlTrigger.enabled = true;
        }

        public void SetControlTriggerVisible(bool visible = true)
        {
            if (_controlTrigger == null) return;

            _controlTrigger.holdInteraction = visible;
            _controlTrigger.isPlayingSpecialAnimation = !visible;
        }

        [ServerRpc(RequireOwnership = false)]
        public void StartControllingServerRpc(NetworkObjectReference playerNetworkReference)
        {
            LethalMon.Log("StartControllingServerRpc");
            StartControllingClientRpc(playerNetworkReference, _enemy!.transform.position);
        }

        [ClientRpc]
        public void StartControllingClientRpc(NetworkObjectReference playerNetworkReference, Vector3 enemyPos)
        {
            LethalMon.Log("StartControllingClientRpc");
            if(!playerNetworkReference.TryGet(out NetworkObject networkObject) || !networkObject.TryGetComponent(out PlayerControllerB player))
            {
                LethalMon.Log("Failed to get player object (StartControllingClientRpc).", LethalMon.LogType.Error);
                return;
            }
            playerControlledBy = player;

            _enemy!.moveTowardsDestination = false;
            if (EnemyCanFly)
                _enemy!.agent.enabled = false;

            player.disableMoveInput = true;

            _enemy!.transform.localPosition += EnemyOffsetWhileControlling;
            player.transform.position = _enemy!.transform.position - EnemyOffsetWhileControlling;
            player.transform.rotation = _enemy!.transform.rotation;

            if (IsControlledByUs)
            {
                // Reposition on navMesh
                if (!EnemyCanFly)
                {
                    _enemy.agent.Warp(enemyPos);
                    _enemy.agent.enabled = false;
                    _enemy.agent.enabled = true;
                }

                _controllingPlayerStamina = player.sprintMeter;
                player.sprintMeter = EnemyStamina;
                _staminaDefaultColor = player.sprintMeterUI.color;
                player.sprintMeterUI.color = Color.cyan;

                SetControlTriggerVisible(false);
                BindInputs();
            }

            if (_enemy!.TryGetComponent(out Collider collider))
                Physics.IgnoreCollision(collider, player.playerCollider);

            OnStartControlling?.Invoke();
        }

        [ServerRpc(RequireOwnership = false)]
        public void StopControllingServerRpc()
        {
            LethalMon.Log("StopControllingServerRpc");
            StopControllingClientRpc();
        }

        [ClientRpc]
        public void StopControllingClientRpc()
        {
            StopControlling();
        }
        #endregion

        internal void StopControlling(bool beingDestroyed = false)
        {
            if (playerControlledBy != null)
            {
                playerControlledBy.disableMoveInput = false;

                if (_enemy!.TryGetComponent(out Collider collider))
                    Physics.IgnoreCollision(collider, playerControlledBy.playerCollider, false);
            }

            _enemy!.transform.localPosition -= EnemyOffsetWhileControlling;

            if (EnemyCanFly)
            {
                if (Physics.Raycast(_enemy.transform.position + Vector3.up, -Vector3.up, out var hitInfo, 50f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
                {
                    Vector3 navMeshPosition = RoundManager.Instance.GetNavMeshPosition(hitInfo.point, default(NavMeshHit), 10f);
                    _enemy.transform.position = navMeshPosition;
                }
                
                _enemy!.agent.enabled = true;
            }

            if (IsControlledByUs)
            {
                SetControlTriggerVisible(true);
                UnbindInputs();
                playerControlledBy!.sprintMeter = _controllingPlayerStamina;
                playerControlledBy!.sprintMeterUI.fillAmount = playerControlledBy.sprintMeter;
                playerControlledBy!.sprintMeterUI.color = _staminaDefaultColor;
            }

            if (!beingDestroyed)
            {
                OnStopControlling?.Invoke();
            }

            playerControlledBy = null;
        }
        
        internal void BindInputs()
        {
            if (_inputsBinded || !IsControlledByUs) return;

            LethalMon.Log("Binding inputs to control enemy " + _enemy!.enemyType.name);
            IngamePlayerSettings.Instance.playerInput.actions.FindAction("Sprint").started += SprintStart;
            IngamePlayerSettings.Instance.playerInput.actions.FindAction("Sprint").canceled += SprintStop;
            IngamePlayerSettings.Instance.playerInput.actions.FindAction("Crouch").started += Crouch;

            if (EnemyCanJump)
                IngamePlayerSettings.Instance.playerInput.actions.FindAction("Jump").started += Jump;

            _inputsBinded = true;
        }

        internal void UnbindInputs()
        {
            if (!_inputsBinded || !IsControlledByUs) return;

            LethalMon.Log("Unbinding inputs -> " + _enemy!.enemyType.name);
            IngamePlayerSettings.Instance.playerInput.actions.FindAction("Sprint").started -= SprintStart;
            IngamePlayerSettings.Instance.playerInput.actions.FindAction("Sprint").canceled -= SprintStop;
            IngamePlayerSettings.Instance.playerInput.actions.FindAction("Crouch").started -= Crouch;

            if (EnemyCanJump)
                IngamePlayerSettings.Instance.playerInput.actions.FindAction("Jump").started -= Jump;

            _inputsBinded = false;
        }

        void LateUpdate()
        {
            UpdateStamina();
        }

        void Update()
        {
            if (_controlTrigger != null)
                _controlTrigger.gameObject.transform.position = _enemy!.transform.position + _triggerCenterDistance;

            if (playerControlledBy != null && _enemy != null)
            {
                if (_inputsBinded)
                {
                    // Controlling player
                    playerControlledBy!.transform.position = _enemy!.transform.position - EnemyOffsetWhileControlling;
                    playerControlledBy!.ResetFallGravity();

                    if (_moveAction.IsPressed())
                    {
                        // Actively moving
                        if (!IsMoving)
                        {
                            //OnStartMoving?.Invoke();
                            IsMoving = true;
                        }
                        Moving(OnCalculateMovementVector(_moveAction.ReadValue<Vector2>()));
                    }
                    else
                    {
                        // Not moving anymore
                        if (IsMoving)
                        {
                            //OnStopMoving?.Invoke();
                            IsMoving = false;
                        }

                        if (CurrentSpeed > 0.1f)
                            Moving(_lastDirection);
                    }
                }
                else
                {
                    // Other clients
                    _enemy!.transform.position = playerControlledBy!.transform.position + EnemyOffsetWhileControlling;
                }

                _enemy!.transform.rotation = playerControlledBy!.transform.rotation;
            }

            if (_inputsBinded && (playerControlledBy == null || playerControlledBy.isPlayerDead || _enemy == null || _enemy.isEnemyDead))
                StopControllingServerRpc();
        }

        private void UpdateStamina()
        {
            if (IsControlledByUs && _inputsBinded)
            {
                // Controlled
                if (IsMoving)
                    EnemyStamina = Mathf.Clamp(EnemyStamina - Time.deltaTime / playerControlledBy!.sprintTime * (playerControlledBy.carryWeight / EnemyStrength) * (IsSprinting ? 4f : 1f) * EnemyStaminaUseMultiplier / EnemyDuration, 0f, 1f); // Take stamina while moving, more if sprinting
                else if (EnemyCanFly && !playerControlledBy!.IsPlayerNearGround())
                    EnemyStamina = Mathf.Clamp(EnemyStamina - Time.deltaTime / playerControlledBy!.sprintTime * (playerControlledBy.carryWeight / EnemyStrength) * EnemyStaminaUseMultiplier / EnemyDuration / 5f, 0f, 1f); // Player is standing mid-air
                else
                    EnemyStamina = Mathf.Clamp(EnemyStamina + Time.deltaTime / (playerControlledBy!.sprintTime + 1f) * EnemyStaminaUseMultiplier, 0f, 1f); // Gain stamina if grounded and not moving

                _controllingPlayerStamina = Mathf.Clamp(_controllingPlayerStamina + Time.deltaTime / (playerControlledBy.sprintTime + 2f), 0f, 1f);

                if (playerControlledBy.sprintMeter < 0.2f)
                {
                    StopControllingServerRpc();
                    return;
                }

                playerControlledBy.sprintMeter = EnemyStamina;
                playerControlledBy.sprintMeterUI.fillAmount = EnemyStamina;
            }
            else
            {
                // Not controlled
                EnemyStamina = Mathf.Clamp(EnemyStamina + Time.deltaTime / 5f / EnemyDuration, 0f, 1f);
            }

            //LethalMon.Log($"Stamina player {(int)(100f*controllingPlayerStamina)}%, enemy{(int)(100f * enemyStamina)}%");
        }

        // Simplify abstract method parameter
        internal void Jump(InputAction.CallbackContext callbackContext) => OnJump();
        internal void SprintStart(InputAction.CallbackContext callbackContext) => IsSprinting = true;
        internal void SprintStop(InputAction.CallbackContext callbackContext) => IsSprinting = false;
        
        internal void Crouch(InputAction.CallbackContext callbackContext) => OnCrouch();

        // Virtual methods
        internal Vector3 CalculateMovementVector(Vector2 moveInputVector)
        {
            var angleStrength = moveInputVector.x > 0 ? moveInputVector.x : -moveInputVector.x;
            var baseVector = playerControlledBy!.gameplayCamera.transform.forward;
            var leftrightVector = Quaternion.Euler(0, 90f * moveInputVector.x, 0) * baseVector * angleStrength;
            var forwardVector = baseVector * moveInputVector.y;
            var directionVector = leftrightVector + forwardVector;

            if (!EnemyCanFly)
                directionVector.y = 0f;

            return directionVector * Time.deltaTime;
        }

        internal void Moving(Vector3 direction)
        {
            _lastDirection = direction;

            if (IsMoving)
            {
                float maxSpeed = playerControlledBy!.isInsideFactory && StartOfRound.Instance.testRoom == null ? EnemySpeedInside : EnemySpeedOutside;
                if (IsSprinting)
                    maxSpeed *= 2.25f;

                CurrentSpeed = Mathf.Max(Mathf.Lerp(CurrentSpeed, maxSpeed, 2f * Time.deltaTime), 0f);
            }
            else
                CurrentSpeed = Mathf.Max( Mathf.Lerp(CurrentSpeed, 0f, 5f * Time.deltaTime), 0f);

            direction *= CurrentSpeed;

            if (EnemyCanFly)
            {
                // Collision checking
                var raycastForward = direction;
                raycastForward.Scale(playerControlledBy!.playerCollider.bounds.size / 2f);
                bool willCollideForward = Physics.Raycast(new Ray(playerControlledBy!.playerCollider.bounds.center, raycastForward), out _, 0.5f, StartOfRound.Instance.allPlayersCollideWithMask, QueryTriggerInteraction.Ignore);
                if (willCollideForward) return;

                if (direction.y < 0f)
                {
                    bool willCollideDownwards = Physics.Raycast(new Ray(playerControlledBy!.playerCollider.bounds.center, Vector3.down), out _, playerControlledBy!.transform.localScale.y / 2f, StartOfRound.Instance.allPlayersCollideWithMask, QueryTriggerInteraction.Ignore);
                    if (willCollideDownwards) return;
                }
                
                _enemy!.transform.position += direction;
            }
            else
            {
                var navMeshPos = RoundManager.Instance.GetNavMeshPosition(_enemy!.transform.position + direction, RoundManager.Instance.navHit, -1f);
                OnMove(direction);
                _enemy!.agent.Move(navMeshPos - _enemy!.transform.position);
                _enemy!.agent.destination = navMeshPos;
            }
        }

        internal virtual void Crouching()
        {
            StopControllingServerRpc();
        }
        
        internal virtual void Jumping()
        {

        }

        public override void OnDestroy()
        {
            Destroy(_triggerObject);
            Destroy(_controlTrigger);
            
            base.OnDestroy();
        }
    }
}
