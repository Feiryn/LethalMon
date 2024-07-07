using GameNetcodeStuff;
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

        internal bool IsPlayerControlled => playerControlledBy != null;
        internal bool IsControlledByUs => playerControlledBy == Utils.CurrentPlayer || inputsBinded;
        internal virtual float EnemySpeedInside => 2f;
        internal virtual float EnemySpeedOutside => 4f;
        internal virtual float EnemyJumpForce => 10f;
        internal bool EnemyCanJump = false;
        internal bool EnemyCanFly = false;

        internal InputAction moveAction = IngamePlayerSettings.Instance.playerInput.actions.FindAction("Move");

        // Trigger
        internal virtual float ControlTriggerHoldTime => 1f;

        internal InteractTrigger? controlTrigger = null;
        #endregion

        #region Controlling methods
        internal Action? OnStartControlling = null;
        internal Action? OnStopControlling = null;
        internal Func<Vector2, Vector3> OnCalculateMovementVector;
        internal Action<Vector3> OnMove;
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
        public void AddTrigger(string hoverTip = "Control", GameObject? triggerObject = null)
        {
            if (enemy == null || controlTrigger != null) return;
            LethalMon.Log("Adding riding trigger.");

            /*var triggerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            triggerObject.transform.localScale = Vector3.one * 4f;
            triggerObject.transform.position = enemy.transform.position;

            if(triggerObject.TryGetComponent(out MeshRenderer mr))
            {
                mr.material = new Material(Shader.Find("HDRP/Lit"));
                mr.material.color = Color.red;
                mr.enabled = true;
            }

            triggerObject.transform.SetParent(enemy.gameObject.transform, true);*/

            if (triggerObject == null)
                triggerObject = enemy!.gameObject;

            if (triggerObject == null)
            {
                LethalMon.Log("Unable to get spore lizard model.", LethalMon.LogType.Error);
                return;
            }

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

            player.inSpecialInteractAnimation = true;

            player.transform.position = enemy!.transform.position;
            player.transform.rotation = enemy!.transform.rotation;

            if (IsControlledByUs)
            {
                SetControlTriggerVisible(false);
                BindInputs();
            }

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
            if(playerControlledBy != null)
                playerControlledBy.inSpecialInteractAnimation = false;

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
            if (playerControlledBy != null && enemy != null)
            {
                if (inputsBinded && moveAction.IsPressed())
                {
                    // Controlling player
                    Moving(OnCalculateMovementVector(moveAction.ReadValue<Vector2>()));
                    playerControlledBy!.transform.position = enemy!.transform.position;
                }
                else
                {
                    // Other clients
                    enemy!.transform.position = playerControlledBy!.transform.position;
                }

                enemy!.transform.rotation = playerControlledBy!.transform.rotation;
                //enemy!.transform.rotation = Quaternion.Lerp(enemy!.transform.rotation, playerControlledBy!.transform.rotation, Time.deltaTime);
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

            /*if (!EnemyCanFly)
                directionVector.y = 0f;*/

            float speed = playerControlledBy.isInsideFactory ? EnemySpeedInside : EnemySpeedOutside;
            if (isSprinting)
                speed *= 2.25f;

            return directionVector * 0.02f * speed;
        }

        internal void Moving(Vector3 direction)
        {
            OnMove(direction);
            enemy!.agent.Move(direction);
        }

        internal virtual void Jumping()
        {

        }
    }
}
