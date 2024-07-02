using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LethalMon.Behaviours
{
    internal class SporeLizardTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        // Multiplier compared to default player movement
        internal readonly float RidingSpeedMultiplier = 2.5f;
        internal readonly float RidingJumpForceMultiplier = 2f;
        internal PufferAI sporeLizard { get; private set; }

        internal InteractTrigger? ridingTrigger = null;

        internal float previousJumpForce = Utils.DefaultJumpForce;
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
#endif


            if (ownerPlayer == Utils.CurrentPlayer)
                AddRidingTrigger();


#if DEBUG
            SetRidingTriggerVisible();
#endif
        }

        internal override void OnTamedFollowing()
        {
            base.OnTamedFollowing();

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
            LethalMon.Log("Adding riding trigger", LethalMon.LogType.Warning);

            /*var triggerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            
            if (triggerObject.TryGetComponent(out MeshRenderer meshRenderer))
            {
                meshRenderer.material = new Material(Shader.Find("HDRP/Lit"));
                meshRenderer.material.color = Color.yellow;
                meshRenderer.enabled = true;
            }

            triggerObject.transform.localScale = sporeLizard.transform.localScale * 5f;
            LethalMon.Log(triggerObject.transform.localScale.ToString(), LethalMon.LogType.Warning);
            triggerObject.transform.position = sporeLizard.transform.position;
            LethalMon.Log(triggerObject.transform.position.ToString(), LethalMon.LogType.Warning);
            triggerObject.transform.rotation = sporeLizard.transform.rotation;
            //triggerObject.transform.SetParent(sporeLizard.transform.parent);*/

            var triggerObject = sporeLizard.transform.Find("PufferModel").gameObject;
            if(triggerObject == null)
            {
                LethalMon.Log("eeeee");
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
            ridingTrigger.timeToHold = 1.5f;
            ridingTrigger.timeToHoldSpeedMultiplier = 1f;

            ridingTrigger.holdingInteractEvent = new InteractEventFloat();
            ridingTrigger.onInteract = new InteractEvent();
            ridingTrigger.onInteractEarly = new InteractEvent();
            ridingTrigger.onStopInteract = new InteractEvent();
            ridingTrigger.onCancelAnimation = new InteractEvent();

            ridingTrigger.onInteract.AddListener((player) => StartRiding());
            ridingTrigger.enabled = true;

            SetRidingTriggerVisible(false);
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
            previousJumpForce = ownerPlayer!.jumpForce;
            ownerPlayer!.playerBodyAnimator.enabled = false;

            sporeLizard.enabled = false;
            sporeLizard.agent.enabled = false;

            sporeLizard.transform.position = ownerPlayer!.transform.position;
            sporeLizard.transform.rotation = ownerPlayer!.transform.rotation;
            sporeLizard.transform.SetParent(ownerPlayer!.transform);

            if (sporeLizard.TryGetComponent(out Collider collider))
                Physics.IgnoreCollision(collider, ownerPlayer!.playerCollider);

            SetRidingTriggerVisible(false);
            SwitchToCustomBehaviour((int)CustomBehaviour.Riding);
        }

        private void StopRiding()
        {
            ownerPlayer!.jumpForce = previousJumpForce;
            ownerPlayer!.playerBodyAnimator.enabled = true;

            sporeLizard.transform.SetParent(null);

            sporeLizard.enabled = true;
            sporeLizard.agent.enabled = true;

            if (sporeLizard.TryGetComponent(out Collider collider))
                Physics.IgnoreCollision(collider, ownerPlayer!.playerCollider, false);

            SetRidingTriggerVisible();
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
        }
        #endregion
    }
}
