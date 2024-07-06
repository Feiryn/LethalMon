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
        internal override bool Controllable => true;

        // Multiplier compared to default player movement
        internal readonly float RidingSpeedMultiplier = 1.5f;
        internal readonly float RidingJumpForceMultiplier = 1.3f;
        internal readonly float RidingTriggerHoldTime = 1f;

        internal PufferAI sporeLizard { get; private set; }

        internal InteractTrigger? ridingTrigger = null;

        internal float previousJumpForce = Utils.DefaultJumpForce;
        internal bool usingModifiedValues = false;

        internal bool IsRiding => CurrentCustomBehaviour == (int)CustomBehaviour.Riding;
        internal bool IsOwnerPlayer => ownerPlayer == Utils.CurrentPlayer;

        internal EnemyController? controller = null;
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

            if (CurrentCustomBehaviour == (int)CustomBehaviour.Riding && IsOwnerPlayer)
                controller!.StopControllingServerRpc();
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
            sporeLizard.CalculateAnimationDirection();
        }
        #endregion

        #region Controlling
        internal void OnStartRiding()
        {
            if (Utils.IsHost)
                SwitchToCustomBehaviour((int)CustomBehaviour.Riding);

            if (controller!.IsControlledByUs)
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true);

            if(controller!.playerControlledBy != null)
                controller!.playerControlledBy.playerBodyAnimator.SetLayerWeight(controller!.playerControlledBy.playerBodyAnimator.GetLayerIndex("HoldingItemsBothHands"), 1f);
        }

        internal void OnStopRiding()
        {
            if(Utils.IsHost)
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);

            if (controller!.IsControlledByUs)
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, false);

            sporeLizard.agentLocalVelocity = Vector3.zero;
            sporeLizard.CalculateAnimationDirection(0f);

            if (controller!.playerControlledBy != null)
                controller!.playerControlledBy.playerBodyAnimator.SetLayerWeight(controller!.playerControlledBy.playerBodyAnimator.GetLayerIndex("HoldingItemsBothHands"), 0f);
        }

        internal void OnMove(Vector3 direction)
        {
            sporeLizard.CalculateAnimationDirection();
        }

        internal void OnJump()
        {
            LethalMon.Log("Spore lizard is jumping");
            controller!.Jumping();
        }
        #endregion

        #region Base Methods
        void Awake()
        {
            sporeLizard = (Enemy as PufferAI)!;

            if (TryGetComponent(out controller) && controller != null)
            {
                controller.TriggerObject = (gameObject) => gameObject.transform.Find("PufferModel").gameObject;
                controller.OnStartControlling = OnStartRiding;
                controller.OnStopControlling = OnStopRiding;
                controller.OnMove = OnMove;

                controller.EnemyCanJump = true;
                controller.OnJump = OnJump;

#if DEBUG
                ownerPlayer = Utils.CurrentPlayer;
                isOutsideOfBall = true;
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                controller!.enemy = GetComponent<EnemyAI>();
                controller!.AddTrigger("Ride");
                controller!.SetControlTriggerVisible(true);
#endif
            }
        }

        internal override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            base.OnUpdate(update, !IsRiding); // Don't attempt to SetDestination in Riding mode
        }

        internal override void OnCallFromBall()
        {
            base.OnCallFromBall();

            if(ridingTrigger == null && ownerPlayer == Utils.CurrentPlayer)
                controller!.AddTrigger();

            controller!.SetControlTriggerVisible();
        }

        internal override void OnRetreiveInBall()
        {
            base.OnRetreiveInBall();

            controller!.SetControlTriggerVisible(false);
        }

        internal override void OnTamedFollowing()
        {
            base.OnTamedFollowing();
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
        #endregion
    }
}
