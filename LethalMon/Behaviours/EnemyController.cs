using GameNetcodeStuff;
using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace LethalMon.Behaviours
{
    /// <summary>
    /// Allows to control an enemy.
    /// Don't forget to destroy this object with the enemy object as it is not automatically destroyed.
    /// </summary>
    public class EnemyController : NetworkBehaviour
    {
        #region Animations

        private static readonly float shinLRotationX = 125f;
        
        private static readonly float shinLRotationY = -0.289f;
        
        private static readonly float shinLRotationZ = -0.987f;
        
        private static readonly float shinRRotationX = 125f;
        
        private static readonly float shinRRotationY = 0.289f;
        
        private static readonly float shinRRotationZ = 0.987f;

        private static readonly float thighLRotationX = 69.399f;
        
        private static readonly float thighLRotationY = -22.43f;
        
        private static readonly float thighLRotationZ = -3f;
        
        private static readonly float thighRRotationX = 75.226f;
        
        private static readonly float thighRRotationY = 0.293f;
        
        private static readonly float thighRRotationZ = -20.61f;
        
        private Transform _shinL = null;
        
        private Transform _shinR = null;
        
        private Transform _thighL = null;
        
        private Transform _thighR = null;
        #endregion
        
        #region Properties
        private EnemyAI? _enemy = null;

        private PlayerControllerB? playerControlledBy = null;

        // Stamina
        private float _controllingPlayerStamina = 0f;
        private Color _staminaDefaultColor;
        
        private float _enemyStamina = 1f;

        private bool _inputsBinded = false;
        
        private bool _isSprinting = false;
        
        private bool _isMoving = false;

        private Vector3 _lastDirection = Vector3.zero;
        
        private float _currentSpeed = 0f;

        public bool IsPlayerControlled => playerControlledBy != null;
        
        private bool IsControlledByUs => IsPlayerControlled && playerControlledBy == Utils.CurrentPlayer || _inputsBinded;

        // Changeable variables
        
        /// <summary>
        /// Enemy speed inside the factory.
        /// </summary>
        public float enemySpeedInside = 4f;
        
        /// <summary>
        /// Enemy speed outside the factory.
        /// </summary>
        public float enemySpeedOutside = 6f;
        
        /// <summary>
        /// Jump force of the enemy. Not implemented yet.
        /// </summary>
        public float enemyJumpForce = 10f;
        
        /// <summary>
        /// General stamina duration of the enemy.
        /// </summary>
        public float enemyDuration = 5f;
        
        /// <summary>
        /// Defines how much the enemy stamina is affected by held items
        /// </summary>
        public float enemyStrength = 1f; 
        
        /// <summary>
        /// Defines if the enemy can jump.
        /// </summary>
        public bool enemyCanJump = false;
        
        /// <summary>
        /// Defines if the enemy can fly.
        /// </summary>
        public bool enemyCanFly = false;
        
        /// <summary>
        /// Defines the stamina use multiplier of the enemy.
        /// </summary>
        public float enemyStaminaUseMultiplier = 1f;
        
        /// <summary>
        /// Defines if the enemy can sprint.
        /// </summary>
        public bool enemyCanSprint = true;

        /// <summary>
        /// Defines if the enemy should move forward even if the player is not moving.
        /// </summary>
        public bool forceMoveForward = false;
        
        /// <summary>
        /// Defines if the enemy should sprint even if the player is not sprinting.
        /// </summary>
        public bool forceSprint = false;
        
        /// <summary>
        /// Vector offset of the enemy while being controlled.
        /// </summary>
        public Vector3 enemyOffsetWhileControlling = Vector3.zero; // TODO: transform parenting

        private readonly InputAction _moveAction = IngamePlayerSettings.Instance.playerInput.actions.FindAction("Move");

        // Trigger
        
        /// <summary>
        /// Control trigger hold time.
        /// </summary>
        public virtual float ControlTriggerHoldTime => 1f;

        private InteractTrigger? _controlTrigger = null;
        private GameObject? _triggerObject = null;
        #endregion

        #region Controlling methods
        /// <summary>
        /// Method called when starting to control the enemy.
        /// </summary>
        public Action? OnStartControlling = null;
        
        /// <summary>
        /// Method called when stopping to control the enemy.
        /// </summary>
        public Action? OnStopControlling = null;
        
        private readonly Func<Vector2, Vector3> _onCalculateMovementVector;
        
        /// <summary>
        /// Method called when starting to sprint.
        /// </summary>
        public Action? OnStartSprinting = null;
        
        /// <summary>
        /// Method called when stopping to sprint.
        /// </summary>
        public Action? OnStopSprinting = null;
        
        /// <summary>
        /// Method called when moving the enemy.
        /// </summary>
        public Action<Vector3> OnMove;
        
        /// <summary>
        /// Method called when jumping.
        /// </summary>
        public Action OnJump;
        
        /// <summary>
        /// Method called when crouching.
        /// </summary>
        public Action OnCrouch;
        #endregion

        /// <summary>
        /// Default constructor.
        /// </summary>
        public EnemyController()
        {
            _onCalculateMovementVector = CalculateMovementVector;
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
        /// <summary>
        /// Adds a trigger to control the enemy.
        /// This method can be called when the enemy starts to be controllable.
        /// The trigger will be added again automatically when the enemy is not controlled anymore. 
        /// </summary>
        /// <param name="hoverTip">Hover tip text when the player aims the monster</param>
        public void AddTrigger(string hoverTip = "Control")
        {
            if (_enemy?.transform == null || _controlTrigger != null) return;
            LethalMon.Log("Adding riding trigger.");
            
            Utils.CreateInteractionForEnemy(_enemy!, hoverTip, ControlTriggerHoldTime, (player) => StartControllingServerRpc(player.NetworkObject), out _controlTrigger, out _triggerObject);
        }

        /// <summary>
        /// Sets the visibility of the control trigger.
        /// </summary>
        /// <param name="visible">True to make the trigger visible, false otherwise.</param>
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

            var spineTransform = player.transform
                .Find("ScavengerModel")
                .Find("metarig")
                .Find("spine");
            _thighL = spineTransform.Find("thigh.L");
            _thighR = spineTransform.Find("thigh.R");
            _shinL = _thighL.Find("shin.L");
            _shinR = _thighR.Find("shin.R");

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
                player.sprintMeter = _enemyStamina;
                _staminaDefaultColor = player.sprintMeterUI.color;
                player.sprintMeterUI.color = Color.cyan;

                SetControlTriggerVisible(false);
                BindInputs();
                
                HUDManager.Instance.ClearControlTips();
                
                HUDManager.Instance.ChangeControlTipMultiple(
                    [
                        $"Dismount: [{IngamePlayerSettings.Instance.playerInput.actions.FindAction("Crouch").bindings[0].effectivePath}]",
                        $"Sprint: [{IngamePlayerSettings.Instance.playerInput.actions.FindAction("Sprint").bindings[0].effectivePath}]"
                    ],
                    holdingItem: Utils.CurrentPlayer.currentlyHeldObjectServer != null,
                    Utils.CurrentPlayer.currentlyHeldObjectServer?.itemProperties);
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

        /// <summary>
        /// Stops controlling the enemy.
        /// </summary>
        /// <param name="beingDestroyed">Indicates if the enemy is being destroyed. If not, OnStopControlling will be called. If yes, it does not to prevent errors.</param>
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
                HUDManager.Instance.ClearControlTips();
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

        private void BindInputs()
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

        private void UnbindInputs()
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

            if (playerControlledBy != null)
            {
                _shinL.localRotation = Quaternion.Euler(shinLRotationX, shinLRotationY, shinLRotationZ);
                _shinR.localRotation = Quaternion.Euler(shinRRotationX, shinRRotationY, shinRRotationZ);
                _thighL.localRotation = Quaternion.Euler(thighLRotationX, thighLRotationY, thighLRotationZ);
                _thighR.localRotation = Quaternion.Euler(thighRRotationX, thighRRotationY, thighRRotationZ);
            }
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

                    if (forceSprint && !_isSprinting)
                    {
                        _isSprinting = true;
                        OnStartSprinting?.Invoke();
                    }
                    
                    if (_moveAction.IsPressed() || forceMoveForward)
                    {
                        // Actively moving
                        if (!_isMoving)
                        {
                            //OnStartMoving?.Invoke();
                            _isMoving = true;
                        }
                        MoveEnemy(!forceMoveForward ? _onCalculateMovementVector(_moveAction.ReadValue<Vector2>()) : _onCalculateMovementVector(Vector2.up));
                    }
                    else
                    {
                        // Not moving anymore
                        if (_isMoving)
                        {
                            //OnStopMoving?.Invoke();
                            _isMoving = false;
                        }

                        if (_currentSpeed > 0.1f)
                            MoveEnemy(_lastDirection);
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
                if (_isMoving)
                    _enemyStamina = Mathf.Clamp(_enemyStamina - Time.deltaTime / playerControlledBy!.sprintTime * (playerControlledBy.carryWeight / enemyStrength) * (_isSprinting ? 4f : 1f) * enemyStaminaUseMultiplier / enemyDuration, 0f, 1f); // Take stamina while moving, more if sprinting
                else if (enemyCanFly && !playerControlledBy!.IsPlayerNearGround())
                    _enemyStamina = Mathf.Clamp(_enemyStamina - Time.deltaTime / playerControlledBy!.sprintTime * (playerControlledBy.carryWeight / enemyStrength) * enemyStaminaUseMultiplier / enemyDuration / 5f, 0f, 1f); // Player is standing mid-air
                else
                    _enemyStamina = Mathf.Clamp(_enemyStamina + Time.deltaTime / (playerControlledBy!.sprintTime + 1f) * enemyStaminaUseMultiplier, 0f, 1f); // Gain stamina if grounded and not moving

                _controllingPlayerStamina = Mathf.Clamp(_controllingPlayerStamina + Time.deltaTime / (playerControlledBy.sprintTime + 2f), 0f, 1f);

                if (playerControlledBy.sprintMeter < 0.2f)
                {
                    StopControllingServerRpc();
                    return;
                }

                playerControlledBy.sprintMeter = _enemyStamina;
                playerControlledBy.sprintMeterUI.fillAmount = _enemyStamina;
            }
            else
            {
                // Not controlled
                _enemyStamina = Mathf.Clamp(_enemyStamina + Time.deltaTime / 5f / enemyDuration, 0f, 1f);
            }

            //LethalMon.Log($"Stamina player {(int)(100f*controllingPlayerStamina)}%, enemy{(int)(100f * enemyStamina)}%");
        }

        // Simplify abstract method parameter
        private void Jump(InputAction.CallbackContext callbackContext) => OnJump();
        private void SprintStart(InputAction.CallbackContext callbackContext)
        {
            if (enemyCanSprint || forceSprint)
            {
                _isSprinting = true;
                OnStartSprinting?.Invoke();
            }
        }

        private void SprintStop(InputAction.CallbackContext callbackContext)
        {
            if (_isSprinting && !forceSprint)
            {
                _isSprinting = false;
                OnStopSprinting?.Invoke();
            }
        }

        private void Crouch(InputAction.CallbackContext callbackContext) => OnCrouch();

        // Virtual methods
        private Vector3 CalculateMovementVector(Vector2 moveInputVector)
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

        public void StopSprinting(bool callCallback = true)
        {
            _isSprinting = false;
            if (callCallback)
                OnStopSprinting?.Invoke();
        }

        private void MoveEnemy(Vector3 direction)
        {
            _lastDirection = direction;

            if (_isMoving)
            {
                float maxSpeed = playerControlledBy!.isInsideFactory && StartOfRound.Instance.testRoom == null ? enemySpeedInside : enemySpeedOutside;
                if (_isSprinting)
                    maxSpeed *= 2.25f;

                _currentSpeed = Mathf.Max(Mathf.Lerp(_currentSpeed, maxSpeed, 2f * Time.deltaTime), 0f);
            }
            else
                _currentSpeed = Mathf.Max( Mathf.Lerp(_currentSpeed, 0f, 5f * Time.deltaTime), 0f);

            direction *= _currentSpeed;

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

        /// <summary>
        /// Method called when the player is crouching.
        /// </summary>
        public virtual void Crouching()
        {
            StopControllingServerRpc();
        }
        
        /// <summary>
        /// Method called when the player is jumping.
        /// </summary>
        public virtual void Jumping()
        {

        }

        public virtual void Moving(Vector3 direction)
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
