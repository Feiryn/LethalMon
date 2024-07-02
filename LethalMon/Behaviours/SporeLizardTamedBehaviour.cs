using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static LethalMon.Behaviours.TamedEnemyBehaviour;

namespace LethalMon.Behaviours
{
    internal class SporeLizardTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        // Multiplier compared to default player movement
        internal readonly float RidingSpeedMultiplier = 2.2f;
        internal readonly float RidingJumpForceMultiplier = 1.7f;
        internal PufferAI sporeLizard { get; private set; }

        internal InteractTrigger? ridingTrigger = null;

        internal float previousJumpForce = Utils.DefaultJumpForce;
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
                StopRiding();
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
            ownerPlayer!.sprintMultiplier = Utils.DefaultPlayerSpeed * RidingSpeedMultiplier;
            ownerPlayer!.jumpForce = Utils.DefaultJumpForce * RidingJumpForceMultiplier;
            ownerPlayer!.takingFallDamage = false;
            sporeLizard.CalculateAnimationDirection();
        }
        #endregion

        #region Base Methods
        internal override void Start()
        {
            base.Start();

            sporeLizard = (Enemy as PufferAI)!;

#if DEBUG
            ownerPlayer = Utils.CurrentPlayer;
            AddRidingTrigger();
            SetRidingTriggerVisible();
            isOutsideOfBall = true;
#endif
        }

        internal override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            var shouldDoAIInterval = CurrentCustomBehaviour != (int)CustomBehaviour.Riding; // Don't attempt to SetDestination in Riding mode
            base.OnUpdate(update, shouldDoAIInterval);
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
            ridingTrigger.timeToHold = 1f;
            ridingTrigger.timeToHoldSpeedMultiplier = 1f;

            ridingTrigger.holdingInteractEvent = new InteractEventFloat();
            ridingTrigger.onInteract = new InteractEvent();
            ridingTrigger.onInteractEarly = new InteractEvent();
            ridingTrigger.onStopInteract = new InteractEvent();
            ridingTrigger.onCancelAnimation = new InteractEvent();

            ridingTrigger.onInteract.AddListener((player) => StartRiding());
            ridingTrigger.enabled = true;
        }

        private void SetRidingTriggerVisible(bool visible = true)
        {
            if (ridingTrigger == null) return;

            ridingTrigger.touchTrigger = visible;
            ridingTrigger.holdInteraction = visible;
            ridingTrigger.isPlayingSpecialAnimation = !visible;
        }

        private void StartRiding()
        {
            LethalMon.Log("SporeLizard.StartRiding");
            previousJumpForce = ownerPlayer!.jumpForce;
            ownerPlayer!.playerBodyAnimator.enabled = false;

            //sporeLizard.enabled = false;
            //sporeLizard.agent.enabled = false;

            sporeLizard.transform.position = ownerPlayer!.transform.position;
            sporeLizard.transform.rotation = ownerPlayer!.transform.rotation;
            sporeLizard.transform.SetParent(ownerPlayer!.transform);

            if (sporeLizard.TryGetComponent(out Collider collider))
                Physics.IgnoreCollision(collider, ownerPlayer!.playerCollider);

            SetRidingTriggerVisible(false);
            SwitchToCustomBehaviour((int)CustomBehaviour.Riding);
            EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true);
        }

        private void StopRiding()
        {
            LethalMon.Log("SporeLizard.StopRiding");
            ownerPlayer!.jumpForce = previousJumpForce;
            ownerPlayer!.playerBodyAnimator.enabled = true;

            sporeLizard.transform.SetParent(null);

            //sporeLizard.enabled = true;
            //sporeLizard.agent.enabled = true;

            if (sporeLizard.TryGetComponent(out Collider collider))
                Physics.IgnoreCollision(collider, ownerPlayer!.playerCollider, false);

            SetRidingTriggerVisible();
            EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, false);
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
        }
        #endregion
    }
}
