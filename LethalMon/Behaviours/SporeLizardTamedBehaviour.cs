using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.Behaviours
{
    internal class SporeLizardTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        // Multiplier compared to default player movement
        internal readonly float RidingSpeedMultiplier = 2.2f;
        internal readonly float RidingJumpForceMultiplier = 1.7f;
        internal readonly float RidingTriggerHoldTime = 1f;

        internal PufferAI sporeLizard { get; private set; }

        internal InteractTrigger? ridingTrigger = null;

        internal float previousJumpForce = Utils.DefaultJumpForce;
        internal bool usingModifiedValues = false;

        internal bool IsRiding => CurrentCustomBehaviour == (int)CustomBehaviour.Riding;
        internal bool IsOwnerPlayer => ownerPlayer == Utils.CurrentPlayer;
        #endregion

        #region Action Keys
        private List<ActionKey> _actionKeys = new List<ActionKey>()
        {
            new ActionKey() { actionKey = ModConfig.Instance.ActionKey1, description = "Stop riding" }
        };
        internal override List<ActionKey> ActionKeys => _actionKeys;

        internal override void ActionKey1Pressed()
        {
            LethalMon.Log("ActionKey1Pressed TamedEnemyBehaviour");
            base.ActionKey1Pressed();

            if (CurrentCustomBehaviour == (int)CustomBehaviour.Riding)
                StopRidingServerRpc();
        }
        #endregion

        #region Custom behaviours
        private enum CustomBehaviour
        {
            Riding = 1,
        }
        internal override List<Tuple<string, Action>>? CustomBehaviourHandler => new List<Tuple<string, Action>>()
        {
            { new Tuple<string, Action>(CustomBehaviour.Riding.ToString(), WhileRiding) },
        };

        void WhileRiding()
        {
            if (IsOwnerPlayer)
            {
                usingModifiedValues = true;
                ownerPlayer!.sprintMultiplier = Utils.DefaultPlayerSpeed * RidingSpeedMultiplier;
                ownerPlayer!.jumpForce = Utils.DefaultJumpForce * RidingJumpForceMultiplier;
                ownerPlayer!.takingFallDamage = false;
            }
            else if(!Utils.IsHost && ownerPlayer != null)
            {
                sporeLizard.transform.rotation = ownerPlayer.transform.rotation;
                sporeLizard.transform.position = ownerPlayer.transform.position;
            }

            sporeLizard.CalculateAnimationDirection();
        }
        #endregion

        #region Base Methods
        internal override void Start()
        {
            base.Start();

            sporeLizard = (Enemy as PufferAI)!;

#if DEBUG
            ownerPlayer = StartOfRound.Instance.allPlayerScripts.Where((p) => p.playerClientId == 0ul).First();
            AddRidingTrigger();
            SetRidingTriggerVisible();
            isOutsideOfBall = true;
#endif
        }

        internal override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            if(!IsRiding && usingModifiedValues)
            {
                // Reset to default
                ownerPlayer!.jumpForce = previousJumpForce;
                ownerPlayer!.sprintMultiplier = Utils.DefaultPlayerSpeed;
                usingModifiedValues = false;
            }

            base.OnUpdate(update, !IsRiding); // Don't attempt to SetDestination in Riding mode
        }

        internal override void OnTamedFollowing()
        {
            base.OnTamedFollowing();

            if (ridingTrigger == null && ownerPlayer == Utils.CurrentPlayer)
                AddRidingTrigger();

            SetRidingTriggerVisible();
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            base.OnEscapedFromBall(playerWhoThrewBall);

            sporeLizard.StartCoroutine(PuffAndWait(sporeLizard));
        }
        #endregion

        #region Methods

        private IEnumerator PuffAndWait(PufferAI sporeLizard)
        {
            sporeLizard.ShakeTailServerRpc();
            sporeLizard.enabled = false;
            yield return new WaitForSeconds(1f);
            sporeLizard.enabled = true;
        }

        private void AddRidingTrigger()
        {
            LethalMon.Log("Adding riding trigger.");
            var triggerObject = sporeLizard.transform.Find("PufferModel").gameObject;
            if(triggerObject == null)
            {
                LethalMon.Log("Unable to get spore lizard model.", LethalMon.LogType.Error);
                return;
            }

            triggerObject.tag = "InteractTrigger";
            triggerObject.layer = LayerMask.NameToLayer("InteractableObject");

            ridingTrigger = triggerObject.AddComponent<InteractTrigger>();
            ridingTrigger.interactable = true;
            ridingTrigger.hoverIcon = GameObject.Find("StartGameLever")?.GetComponent<InteractTrigger>()?.hoverIcon;
            ridingTrigger.hoverTip = "Ride";
            ridingTrigger.oneHandedItemAllowed = true;
            ridingTrigger.twoHandedItemAllowed = true;
            ridingTrigger.holdInteraction = true;
            ridingTrigger.touchTrigger = false;
            ridingTrigger.timeToHold = RidingTriggerHoldTime;
            ridingTrigger.timeToHoldSpeedMultiplier = 1f;

            ridingTrigger.holdingInteractEvent = new InteractEventFloat();
            ridingTrigger.onInteract = new InteractEvent();
            ridingTrigger.onInteractEarly = new InteractEvent();
            ridingTrigger.onStopInteract = new InteractEvent();
            ridingTrigger.onCancelAnimation = new InteractEvent();

            ridingTrigger.onInteract.AddListener((player) => StartRidingServerRpc());

            ridingTrigger.enabled = true;
        }

        private void SetRidingTriggerVisible(bool visible = true)
        {
            if (ridingTrigger == null) return;

            ridingTrigger.holdInteraction = visible;
            ridingTrigger.isPlayingSpecialAnimation = !visible;
        }

        [ServerRpc(RequireOwnership = false)]
        public void StartRidingServerRpc()
        {
            LethalMon.Log("StartRidingServerRpc");
            StartRidingClientRpc();
            SwitchToCustomBehaviour((int)CustomBehaviour.Riding);
        }

        [ClientRpc]
        public void StartRidingClientRpc()
        {
            LethalMon.Log("SporeLizard.StartRiding");

            sporeLizard.agent.enabled = false;

            sporeLizard.transform.position = ownerPlayer!.transform.position;
            sporeLizard.transform.rotation = ownerPlayer!.transform.rotation;

            if(Utils.IsHost)
                sporeLizard.transform.SetParent(ownerPlayer!.transform);

            previousJumpForce = ownerPlayer!.jumpForce;
            ownerPlayer!.playerBodyAnimator.enabled = false;

            if (IsOwnerPlayer)
            {
                SetRidingTriggerVisible(false);
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void StopRidingServerRpc()
        {
            LethalMon.Log("StopRidingServerRpc");
            StopRidingClientRpc();
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
        }

        [ClientRpc]
        public void StopRidingClientRpc()
        {
            LethalMon.Log("SporeLizard.StopRiding");
            ownerPlayer!.playerBodyAnimator.enabled = true;

            if (Utils.IsHost)
                sporeLizard.transform.SetParent(null);

            sporeLizard.agent.enabled = true;

            sporeLizard.agentLocalVelocity = Vector3.zero;
            sporeLizard.CalculateAnimationDirection(0f);

            if (IsOwnerPlayer)
            {
                SetRidingTriggerVisible();
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, false);
            }
        }
        #endregion
    }
}
