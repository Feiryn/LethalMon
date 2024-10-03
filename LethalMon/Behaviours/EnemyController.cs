using GameNetcodeStuff;
using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace LethalMon.Behaviours
{
    public class EnemyController : NetworkBehaviour
    {
        #region Properties
        private EnemyAI? _enemy = null;

        private PlayerControllerB? playerControlledBy = null;

        // Stamina
        private float _controllingPlayerStamina = 0f;
        private Color _staminaDefaultColor;
        public float EnemyStamina { get; private set; } = 1f;

        private bool _inputsBinded = false;

        public bool IsSprinting { get; private set; } = false;
        public bool IsMoving { get; private set; } = false;

        private Vector3 _lastDirection = Vector3.zero;
        public float CurrentSpeed { get; private set; } = 0f;

        public bool IsPlayerControlled => playerControlledBy != null;
        public bool IsControlledByUs => IsPlayerControlled && playerControlledBy == Utils.CurrentPlayer || _inputsBinded;

        // Changeable variables
        public float enemySpeedInside = 4f;
        public float enemySpeedOutside = 6f;
        public float enemyJumpForce = 10f;
        public float enemyDuration = 5f; // General stamina duration
        public float enemyStrength = 1f; // Defines how much the enemy stamina is affected by held items
        public bool enemyCanJump = false;
        public bool enemyCanFly = false;
        public float enemyStaminaUseMultiplier = 1f;
        public Vector3 enemyOffsetWhileControlling = Vector3.zero; // TODO: transform parenting

        private readonly InputAction _moveAction = IngamePlayerSettings.Instance.playerInput.actions.FindAction("Move");

        // Trigger
        public virtual float ControlTriggerHoldTime => 1f;

        private InteractTrigger? _controlTrigger = null;
        private GameObject? _triggerObject = null;
        #endregion

        #region Controlling methods
        public Action? OnStartControlling = null;
        public Action? OnStopControlling = null;
        public Func<Vector2, Vector3> OnCalculateMovementVector;
        public Action<Vector3> OnMove;
        //internal Action? OnStartMoving;
        //internal Action? OnStopMoving;
        public Action OnJump;
        public Action OnCrouch;
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
            
            Utils.CreateInteractionForEnemy(_enemy!, hoverTip, ControlTriggerHoldTime, (player) => StartControllingServerRpc(player.NetworkObject), out _controlTrigger, out _triggerObject);
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
            if (enemyCanFly)
                _enemy!.agent.enabled = false;

            player.disableMoveInput = true;

            _enemy!.transform.localPosition += enemyOffsetWhileControlling;
            player.transform.position = _enemy!.transform.position - enemyOffsetWhileControlling;
            player.transform.rotation = _enemy!.transform.rotation;

            if (IsControlledByUs)
            {
                // Reposition on navMesh
                if (!enemyCanFly)
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

        public void StopControlling(bool beingDestroyed = false)
        {
            if (playerControlledBy != null)
            {
                playerControlledBy.disableMoveInput = false;

                if (_enemy!.TryGetComponent(out Collider collider))
                    Physics.IgnoreCollision(collider, playerControlledBy.playerCollider, false);
            }

            _enemy!.transform.localPosition -= enemyOffsetWhileControlling;

            if (enemyCanFly)
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

            if (enemyCanJump)
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

            if (enemyCanJump)
                IngamePlayerSettings.Instance.playerInput.actions.FindAction("Jump").started -= Jump;

            _inputsBinded = false;
        }

        void LateUpdate()
        {
            UpdateStamina();
        }

        void Update()
        {
            if (playerControlledBy != null && _enemy != null)
            {
                if (_inputsBinded)
                {
                    // Controlling player
                    playerControlledBy!.transform.position = _enemy!.transform.position - enemyOffsetWhileControlling;
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
                    _enemy!.transform.position = playerControlledBy!.transform.position + enemyOffsetWhileControlling;
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
                    EnemyStamina = Mathf.Clamp(EnemyStamina - Time.deltaTime / playerControlledBy!.sprintTime * (playerControlledBy.carryWeight / enemyStrength) * (IsSprinting ? 4f : 1f) * enemyStaminaUseMultiplier / enemyDuration, 0f, 1f); // Take stamina while moving, more if sprinting
                else if (enemyCanFly && !playerControlledBy!.IsPlayerNearGround())
                    EnemyStamina = Mathf.Clamp(EnemyStamina - Time.deltaTime / playerControlledBy!.sprintTime * (playerControlledBy.carryWeight / enemyStrength) * enemyStaminaUseMultiplier / enemyDuration / 5f, 0f, 1f); // Player is standing mid-air
                else
                    EnemyStamina = Mathf.Clamp(EnemyStamina + Time.deltaTime / (playerControlledBy!.sprintTime + 1f) * enemyStaminaUseMultiplier, 0f, 1f); // Gain stamina if grounded and not moving

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
                EnemyStamina = Mathf.Clamp(EnemyStamina + Time.deltaTime / 5f / enemyDuration, 0f, 1f);
            }

            //LethalMon.Log($"Stamina player {(int)(100f*controllingPlayerStamina)}%, enemy{(int)(100f * enemyStamina)}%");
        }

        // Simplify abstract method parameter
        public void Jump(InputAction.CallbackContext callbackContext) => OnJump();
        public void SprintStart(InputAction.CallbackContext callbackContext) => IsSprinting = true;
        public void SprintStop(InputAction.CallbackContext callbackContext) => IsSprinting = false;
        
        public void Crouch(InputAction.CallbackContext callbackContext) => OnCrouch();

        // Virtual methods
        internal Vector3 CalculateMovementVector(Vector2 moveInputVector)
        {
            var angleStrength = moveInputVector.x > 0 ? moveInputVector.x : -moveInputVector.x;
            var baseVector = playerControlledBy!.gameplayCamera.transform.forward;
            var leftrightVector = Quaternion.Euler(0, 90f * moveInputVector.x, 0) * baseVector * angleStrength;
            var forwardVector = baseVector * moveInputVector.y;
            var directionVector = leftrightVector + forwardVector;

            if (!enemyCanFly)
                directionVector.y = 0f;

            return directionVector * Time.deltaTime;
        }

        internal void Moving(Vector3 direction)
        {
            _lastDirection = direction;

            if (IsMoving)
            {
                float maxSpeed = playerControlledBy!.isInsideFactory && StartOfRound.Instance.testRoom == null ? enemySpeedInside : enemySpeedOutside;
                if (IsSprinting)
                    maxSpeed *= 2.25f;

                CurrentSpeed = Mathf.Max(Mathf.Lerp(CurrentSpeed, maxSpeed, 2f * Time.deltaTime), 0f);
            }
            else
                CurrentSpeed = Mathf.Max( Mathf.Lerp(CurrentSpeed, 0f, 5f * Time.deltaTime), 0f);

            direction *= CurrentSpeed;

            if (enemyCanFly)
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

        public virtual void Crouching()
        {
            StopControllingServerRpc();
        }
        
        public virtual void Jumping()
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
