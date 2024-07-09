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

        internal bool inputsBinded = false;

        internal bool isSprinting = false;
        internal bool isMoving = false;

        internal bool IsPlayerControlled => playerControlledBy != null;
        internal bool IsControlledByUs => playerControlledBy == Utils.CurrentPlayer || inputsBinded;
        internal float EnemySpeedInside = 4f;
        internal float EnemySpeedOutside = 6f;
        internal float EnemyJumpForce = 10f;
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

        void Update()
        {
            if(controlTrigger != null)
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
                        Moving(OnCalculateMovementVector(moveAction.ReadValue<Vector2>()));

                        if(!isMoving)
                        {
                            OnStartMoving?.Invoke();
                            isMoving = true;
                        }
                    }
                    else if (isMoving)
                    {
                        OnStopMoving?.Invoke();
                        isMoving = false;
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

            float speed = playerControlledBy.isInsideFactory ? EnemySpeedInside : EnemySpeedOutside;
            if (isSprinting)
                speed *= 2.25f;

            return directionVector * speed * Time.deltaTime;
        }

        internal void Moving(Vector3 direction)
        {
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
