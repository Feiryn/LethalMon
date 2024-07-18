using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LethalMon.Behaviours
{
    internal class SporeLizardTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        internal override bool Controllable => true;
        internal readonly float RidingTriggerHoldTime = 1f;

        internal PufferAI sporeLizard { get; private set; }

        internal InteractTrigger? ridingTrigger = null;

        internal bool nightVisionPreviouslyEnabled = false;

        internal bool IsRiding => CurrentCustomBehaviour == (int)CustomBehaviour.Riding;
        internal bool IsOwnerPlayer => ownerPlayer == Utils.CurrentPlayer;

        internal EnemyController? controller = null;
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

            if (IsOwnerPlayer)
            {
                nightVisionPreviouslyEnabled = Utils.CurrentPlayer.nightVision.enabled;
                ownerPlayer!.nightVision.enabled = ownerPlayer.isInsideFactory; // todo: retreive in ball when going outside, so that it gets disabled again
            }
        }

        internal void OnStopRiding()
        {
            if(Utils.IsHost)
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);

            if (IsOwnerPlayer)
            {
                ownerPlayer!.nightVision.enabled = nightVisionPreviouslyEnabled;
            }

            sporeLizard.agentLocalVelocity = Vector3.zero;
            sporeLizard.CalculateAnimationDirection(0f);
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
                controller.OnStartControlling = OnStartRiding;
                controller.OnStopControlling = OnStopRiding;
                controller.OnMove = OnMove;
                controller.OnJump = OnJump;

                controller.EnemyCanJump = true;
                controller.EnemyStrength = 3f;

                // Debug
                /*ownerPlayer = Utils.AllPlayers.Where((p) => p.playerClientId == 0ul).First();
                isOutsideOfBall = true;
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                controller!.enemy = GetComponent<EnemyAI>();
                controller!.AddTrigger("Ride");
                controller!.SetControlTriggerVisible(true);*/
            }
        }

        internal override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            if(!IsRiding)
                base.OnUpdate(); // Don't attempt to SetDestination in Riding mode
        }

        internal override void OnCallFromBall()
        {
            base.OnCallFromBall();

            if(ridingTrigger == null && ownerPlayer == Utils.CurrentPlayer)
                controller!.AddTrigger("Ride");

            controller!.SetControlTriggerVisible();
        }

        internal override void OnRetrieveInBall()
        {
            base.OnRetrieveInBall();
            
            controller!.SetControlTriggerVisible(false);
        }

        internal override void OnTamedFollowing()
        {
            base.OnTamedFollowing();

            sporeLizard.CalculateAnimationDirection();
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            base.OnEscapedFromBall(playerWhoThrewBall);

            if (Utils.IsHost)
                sporeLizard.StartCoroutine(PuffAndWait(sporeLizard));
        }

        public override void OnDestroy()
        {
            if (IsOwnerPlayer)
            {
                ownerPlayer!.nightVision.enabled = nightVisionPreviouslyEnabled;
            }

            sporeLizard.agentLocalVelocity = Vector3.zero;
            sporeLizard.CalculateAnimationDirection(0f);
            
            controller!.StopControlling(true);
            Destroy(controller!);
            
            base.OnDestroy();
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
