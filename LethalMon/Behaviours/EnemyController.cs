using GameNetcodeStuff;
using LethalLib.Modules;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
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
        internal bool IsControlledByUs => playerControlledBy == Utils.CurrentPlayer;
        internal virtual float EnemySpeed => 4f;
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
        internal Action<Vector2> OnMove;
        internal Action OnJump;
        #endregion

        public EnemyController()
        {
            OnMove = Moving;
            OnJump = Jumping;
        }

        void Awake()
        {
            if (!gameObject.TryGetComponent(out enemy))
                LethalMon.Log("EnemyController: Unable to get enemy object.", LethalMon.LogType.Error);
        }

        #region Methods
        public void AddTrigger(string hoverTip = "Ride")
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

            var triggerObject = enemy.transform.Find("PufferModel").gameObject;
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
            /*player.transform.localPosition = Vector3.zero;*/

            //player.transform.SetParent(enemy!.transform, true);
            /*enemy.agent.enabled = false;

            enemy.transform.position = player.transform.position;
            enemy.transform.rotation = player.transform.rotation;

            if (Utils.IsHost)
                enemy.transform.SetParent(player.transform);

            var previousJumpForce = player.jumpForce;
            player.playerBodyAnimator.enabled = false;
            player.disableInteract = true;*/


            if (IsControlledByUs)
            {
                SetControlTriggerVisible(false);
                BindInputs();
            }

            OnStartControlling?.Invoke();
        }

        [ServerRpc(RequireOwnership = false)]
        public void StopControllingServerRpc(NetworkObjectReference playerNetworkReference)
        {
            LethalMon.Log("StopControllingServerRpc");
            StopControllingClientRpc(playerNetworkReference);
        }

        [ClientRpc]
        public void StopControllingClientRpc(NetworkObjectReference playerNetworkReference)
        {
            LethalMon.Log("StopControllingClientRpc");
            if (!playerNetworkReference.TryGet(out NetworkObject networkObject) || !networkObject.TryGetComponent(out PlayerControllerB player))
            {
                LethalMon.Log("Failed to get player object (StopControllingClientRpc).", LethalMon.LogType.Error);
                return;
            }

            player.inSpecialInteractAnimation = false;
            //player.transform.SetParent(null);
            /*enemy.agent.enabled = true;

            if (Utils.IsHost)
                enemy.transform.SetParent(null);

            player.playerBodyAnimator.enabled = true;
            player.disableInteract = false;*/

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
            //if (inputsBinded || !IsControlledByUs) return;

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
            if (inputsBinded)
            {
                if (moveAction.IsPressed())
                {
                    OnMove(moveAction.ReadValue<Vector2>());
                }

                enemy!.transform.rotation = playerControlledBy!.transform.rotation;
                playerControlledBy!.transform.position = enemy!.transform.position;
                //enemy!.transform.rotation = Quaternion.Lerp(enemy!.transform.rotation, playerControlledBy!.transform.rotation, Time.deltaTime);
            }
        }

        // Simplify abstract method parameter
        internal void Jump(InputAction.CallbackContext callbackContext) => OnJump();
        internal void SprintStart(InputAction.CallbackContext callbackContext) => isSprinting = true;
        internal void SprintStop(InputAction.CallbackContext callbackContext) => isSprinting = false;

        // Virtual methods
        internal void Moving(Vector2 moveInputVector)
        {
            float speed = EnemySpeed;
            if (isSprinting)
                speed *= 2.25f;

            var direction = Quaternion.Euler(0, 90f * moveInputVector.x, 0) * playerControlledBy!.gameplayCamera.transform.forward;
            if (!EnemyCanFly)
                direction.y = 0f;
            direction.z *= moveInputVector.y;

            enemy!.agent.Move(direction * 0.02f * speed);
            LethalMon.Log(playerControlledBy!.gameplayCamera.transform.forward + " / " + direction);
        }

        internal virtual void Jumping()
        {

        }
    }
}
