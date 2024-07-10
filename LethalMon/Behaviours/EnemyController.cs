using GameNetcodeStuff;
using LethalLib.Modules;
using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LethalMon.Behaviours
{
    internal class EnemyController : NetworkBehaviour
    {
        #region Properties
        internal EnemyAI? enemy;

        internal PlayerControllerB? playerControlledBy = null;

        // Stamina
        private float controllingPlayerStamina = 0f;
        private Color staminaDefaultColor;
        private float enemyStamina = 1f;

        private bool inputsBinded = false;

        internal bool isSprinting = false;
        internal bool isMoving = false;
        private Vector3 lastDirection = Vector3.zero;
        private float currentSpeed = 0f;

        internal bool IsPlayerControlled => playerControlledBy != null;
        internal bool IsControlledByUs => playerControlledBy == Utils.CurrentPlayer || inputsBinded;

        // Changeable variables
        internal float EnemySpeedInside = 4f;
        internal float EnemySpeedOutside = 6f;
        internal float EnemyJumpForce = 10f;
        internal float EnemyDuration = 5f;
        internal bool EnemyCanJump = false;
        internal bool EnemyCanFly = false;
        internal Vector3 EnemyOffsetWhileControlling = Vector3.zero;

        internal InputAction moveAction = IngamePlayerSettings.Instance.playerInput.actions.FindAction("Move");

        // Trigger
        internal virtual float ControlTriggerHoldTime => 1f;

        internal InteractTrigger? controlTrigger = null;
        internal Vector3 triggerCenterDistance = Vector3.zero;
        #endregion

        #region Controlling methods
        internal Action? OnStartControlling = null;
        internal Action? OnStopControlling = null;
        internal Func<Vector2, Vector3> OnCalculateMovementVector;
        internal Action<Vector3> OnMove;
        internal Action OnStartMoving;
        internal Action OnStopMoving;
        internal Action OnJump;
        #endregion

        public EnemyController()
        {
            OnCalculateMovementVector = CalculateMovementVector;
            OnMove = Moving;
            OnJump = Jumping;
        }

        void Awake()
        {
            if (!gameObject.TryGetComponent(out enemy))
                LethalMon.Log("EnemyController: Unable to get enemy object.", LethalMon.LogType.Error);
        }

        #region Methods
        public void AddTrigger(string hoverTip = "Control")
        {
            if (enemy?.transform == null || controlTrigger != null) return;
            LethalMon.Log("Adding riding trigger.");

            var bounds = Utils.RealEnemyBounds(enemy);
            if(bounds == null)
            {
                LethalMon.Log("Unable to get enemy bounds. No MeshRenderer found.", LethalMon.LogType.Error);
                return;
            }

            var triggerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            triggerObject.transform.position = bounds.Value.center;
            triggerCenterDistance = enemy!.transform.position - bounds.Value.center;
            triggerObject.transform.localScale = bounds.Value.size;
            Physics.IgnoreCollision(triggerObject.GetComponent<BoxCollider>(), Utils.CurrentPlayer.playerCollider);
            //triggerObject.transform.SetParent(enemy.gameObject.transform, false); // damn parenting not working...

            triggerObject.tag = "InteractTrigger";
            triggerObject.layer = LayerMask.NameToLayer("InteractableObject");

            controlTrigger = triggerObject.AddComponent<InteractTrigger>();
            controlTrigger.interactable = true;
            controlTrigger.hoverIcon = GameObject.Find("StartGameLever")?.GetComponent<InteractTrigger>()?.hoverIcon;
            controlTrigger.hoverTip = hoverTip;
            controlTrigger.oneHandedItemAllowed = true;
            controlTrigger.twoHandedItemAllowed = true;
            controlTrigger.holdInteraction = true;
            controlTrigger.touchTrigger = false;
            controlTrigger.timeToHold = ControlTriggerHoldTime;
            controlTrigger.timeToHoldSpeedMultiplier = 1f;

            controlTrigger.holdingInteractEvent = new InteractEventFloat();
            controlTrigger.onInteract = new InteractEvent();
            controlTrigger.onInteractEarly = new InteractEvent();
            controlTrigger.onStopInteract = new InteractEvent();
            controlTrigger.onCancelAnimation = new InteractEvent();

            controlTrigger.onInteract.AddListener((player) => StartControllingServerRpc(player.NetworkObject));

            controlTrigger.enabled = true;
        }

        public void SetControlTriggerVisible(bool visible = true)
        {
            if (controlTrigger == null) return;

            controlTrigger.holdInteraction = visible;
            controlTrigger.isPlayingSpecialAnimation = !visible;
        }

        [ServerRpc(RequireOwnership = false)]
        public void StartControllingServerRpc(NetworkObjectReference playerNetworkReference)
        {
            LethalMon.Log("StartControllingServerRpc");
            StartControllingClientRpc(playerNetworkReference);
        }

        [ClientRpc]
        public void StartControllingClientRpc(NetworkObjectReference playerNetworkReference)
        {
            LethalMon.Log("StartControllingClientRpc");
            if(!playerNetworkReference.TryGet(out NetworkObject networkObject) || !networkObject.TryGetComponent(out PlayerControllerB player))
            {
                LethalMon.Log("Failed to get player object (StartControllingClientRpc).", LethalMon.LogType.Error);
                return;
            }
            playerControlledBy = player;

            enemy!.moveTowardsDestination = false;
            if (EnemyCanFly)
            {
                enemy!.agent.enabled = false;
                playerControlledBy.jetpackControls = true;
                playerControlledBy.disablingJetpackControls = true;
            }

            player.disableMoveInput = true;

            enemy!.transform.localPosition += EnemyOffsetWhileControlling;
            player.transform.position = enemy!.transform.position - EnemyOffsetWhileControlling;
            player.transform.rotation = enemy!.transform.rotation;

            if (IsControlledByUs)
            {
                controllingPlayerStamina = player.sprintMeter;
                player.sprintMeter = enemyStamina;
                staminaDefaultColor = player.sprintMeterUI.color;
                player.sprintMeterUI.color = Color.cyan;

                SetControlTriggerVisible(false);
                BindInputs();
            }

            if (enemy!.TryGetComponent(out Collider collider))
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
            if (playerControlledBy != null)
            {
                playerControlledBy.disableMoveInput = false;
                playerControlledBy!.jetpackControls = false;
                playerControlledBy.disablingJetpackControls = false;

                if (enemy!.TryGetComponent(out Collider collider))
                    Physics.IgnoreCollision(collider, playerControlledBy.playerCollider, false);
            }

            enemy!.transform.localPosition -= EnemyOffsetWhileControlling;

            enemy!.agent.enabled = true;

            if (IsControlledByUs)
            {
                SetControlTriggerVisible(true);
                UnbindInputs();
                playerControlledBy!.sprintMeter = controllingPlayerStamina;
                playerControlledBy!.sprintMeterUI.fillAmount = playerControlledBy.sprintMeter;
                playerControlledBy!.sprintMeterUI.color = staminaDefaultColor;
            }

            OnStopControlling?.Invoke();

            playerControlledBy = null;
        }
        #endregion

        internal void BindInputs()
        {
            if (inputsBinded || !IsControlledByUs) return;

            LethalMon.Log("Binding inputs to control enemy " + enemy!.enemyType.name);
            IngamePlayerSettings.Instance.playerInput.actions.FindAction("Sprint").started += SprintStart;
            IngamePlayerSettings.Instance.playerInput.actions.FindAction("Sprint").canceled += SprintStop;

            if (EnemyCanJump)
                IngamePlayerSettings.Instance.playerInput.actions.FindAction("Jump").started += Jump;

            inputsBinded = true;
        }

        internal void UnbindInputs()
        {
            if (!inputsBinded || !IsControlledByUs) return;

            LethalMon.Log("Unbinding inputs -> " + enemy!.enemyType.name);
            IngamePlayerSettings.Instance.playerInput.actions.FindAction("Sprint").started -= SprintStart;
            IngamePlayerSettings.Instance.playerInput.actions.FindAction("Sprint").canceled -= SprintStop;

            if (EnemyCanJump)
                IngamePlayerSettings.Instance.playerInput.actions.FindAction("Jump").started -= Jump;

            inputsBinded = false;
        }

        void LateUpdate()
        {
            UpdateStamina();
        }

        void Update()
        {
            if (controlTrigger != null)
                controlTrigger.gameObject.transform.position = enemy!.transform.position + triggerCenterDistance;

            if (playerControlledBy != null && enemy != null)
            {
                if (inputsBinded)
                {
                    // Controlling player
                    playerControlledBy!.transform.position = enemy!.transform.position - EnemyOffsetWhileControlling;
                    playerControlledBy!.ResetFallGravity();

                    if (moveAction.IsPressed())
                    {
                        // Actively moving
                        if (!isMoving)
                        {
                            OnStartMoving?.Invoke();
                            isMoving = true;
                        }
                        Moving(OnCalculateMovementVector(moveAction.ReadValue<Vector2>()));
                    }
                    else
                    {
                        // Not moving anymore
                        if (isMoving)
                        {
                            OnStopMoving?.Invoke();
                            isMoving = false;
                        }

                        if (currentSpeed > 0.1f)
                            Moving(lastDirection);
                    }
                }
                else
                {
                    // Other clients
                    enemy!.transform.position = playerControlledBy!.transform.position + EnemyOffsetWhileControlling;
                }

                enemy!.transform.rotation = playerControlledBy!.transform.rotation;
            }

            if (inputsBinded && (playerControlledBy == null || playerControlledBy.isPlayerDead || enemy == null || enemy.isEnemyDead))
                StopControllingServerRpc();
        }

        private void UpdateStamina()
        {
            if (IsControlledByUs && inputsBinded)
            {
                // Controlled
                if (isMoving)
                    enemyStamina = Mathf.Clamp(enemyStamina - Time.deltaTime / playerControlledBy!.sprintTime * playerControlledBy.carryWeight * (isSprinting ? 4f : 1f) / EnemyDuration, 0f, 1f); // Take stamina while moving, more if sprinting
                else if (EnemyCanFly && !playerControlledBy!.IsPlayerNearGround())
                    enemyStamina = Mathf.Clamp(enemyStamina - Time.deltaTime / playerControlledBy!.sprintTime * playerControlledBy.carryWeight / EnemyDuration / 5f, 0f, 1f); // Player is standing mid-air
                else
                    enemyStamina = Mathf.Clamp(enemyStamina + Time.deltaTime / (playerControlledBy!.sprintTime + 1f), 0f, 1f); // Gain stamina if grounded and not moving

                controllingPlayerStamina = Mathf.Clamp(controllingPlayerStamina + Time.deltaTime / (playerControlledBy.sprintTime + 2f), 0f, 1f);

                if (playerControlledBy.sprintMeter < 0.2f)
                {
                    StopControllingServerRpc();
                    return;
                }

                playerControlledBy.sprintMeter = enemyStamina;
                playerControlledBy.sprintMeterUI.fillAmount = enemyStamina;
            }
            else
            {
                // Not controlled
                enemyStamina = Mathf.Clamp(enemyStamina + Time.deltaTime / 2f / EnemyDuration, 0f, 1f);
            }

            //LethalMon.Log($"Stamina player {(int)(100f*controllingPlayerStamina)}%, enemy{(int)(100f * enemyStamina)}%");
        }

        // Simplify abstract method parameter
        internal void Jump(InputAction.CallbackContext callbackContext) => OnJump();
        internal void SprintStart(InputAction.CallbackContext callbackContext) => isSprinting = true;
        internal void SprintStop(InputAction.CallbackContext callbackContext) => isSprinting = false;

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
            lastDirection = direction;

            if (isMoving)
            {
                float maxSpeed = playerControlledBy!.isInsideFactory && StartOfRound.Instance.testRoom == null ? EnemySpeedInside : EnemySpeedOutside;
                if (isSprinting)
                    maxSpeed *= 2.25f;

                currentSpeed = Mathf.Max(Mathf.Lerp(currentSpeed, maxSpeed, 2f * Time.deltaTime), 0f);
            }
            else
                currentSpeed = Mathf.Max( Mathf.Lerp(currentSpeed, 0f, 5f * Time.deltaTime), 0f);

            LethalMon.Log("Currentspeed: " + currentSpeed);
            direction *= currentSpeed;

            if (EnemyCanFly)
            {
                var raycastForward = direction;
                raycastForward.Scale(playerControlledBy!.playerCollider.bounds.size / 2f);
                bool willCollide = Physics.Raycast(new Ray(playerControlledBy!.playerCollider.bounds.center, raycastForward), out _, playerControlledBy!.transform.localScale.y / 2f + 0.03f, StartOfRound.Instance.allPlayersCollideWithMask, QueryTriggerInteraction.Ignore);
                if (willCollide) return;
                enemy!.transform.position += direction;
            }
            else
            {
                var navMeshPos = RoundManager.Instance.GetNavMeshPosition(enemy!.transform.position + direction, RoundManager.Instance.navHit, -1f);
                OnMove(direction);
                enemy!.agent.Move(navMeshPos - enemy!.transform.position);
                enemy!.agent.destination = navMeshPos;
            }
        }

        internal virtual void Jumping()
        {

        }
    }
}
