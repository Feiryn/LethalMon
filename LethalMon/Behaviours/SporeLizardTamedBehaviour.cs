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
        internal PufferAI? _sporeLizard = null;
        internal PufferAI SporeLizard
        {
            get
            {
                if (_sporeLizard == null)
                    _sporeLizard = (Enemy as PufferAI)!;

                return _sporeLizard;
            }
        }

        internal override bool Controllable => true;
        internal const float RidingTriggerHoldTime = 1f;

        private readonly InteractTrigger? _ridingTrigger = null;

        private bool _nightVisionPreviouslyEnabled = false;

        internal bool IsRiding => CurrentCustomBehaviour == (int)CustomBehaviour.Riding;

        private EnemyController? _controller = null;
        #endregion

        #region Custom behaviours
        private enum CustomBehaviour
        {
            Riding = 1,
        }
        internal override List<Tuple<string, string, Action>>? CustomBehaviourHandler =>
        [
            new Tuple<string, string, Action>(CustomBehaviour.Riding.ToString(), "Is being rode...", WhileRiding),
        ];

        void WhileRiding()
        {
            SporeLizard.CalculateAnimationDirection();
        }
        #endregion

        #region Controlling
        internal void OnStartRiding()
        {
            if (Utils.IsHost)
                SwitchToCustomBehaviour((int)CustomBehaviour.Riding);

            if (IsOwnerPlayer)
            {
                _nightVisionPreviouslyEnabled = Utils.CurrentPlayer.nightVision.enabled;
                ownerPlayer!.nightVision.enabled = ownerPlayer.isInsideFactory; // todo: retrieve in ball when going outside, so that it gets disabled again
            }
        }

        internal void OnStopRiding()
        {
            if(Utils.IsHost)
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);

            if (IsOwnerPlayer)
            {
                ownerPlayer!.nightVision.enabled = _nightVisionPreviouslyEnabled;
            }

            SporeLizard.agentLocalVelocity = Vector3.zero;
            SporeLizard.CalculateAnimationDirection(0f);
        }

        internal void OnMove(Vector3 direction)
        {
            SporeLizard.CalculateAnimationDirection();
        }

        internal void OnJump()
        {
            LethalMon.Log("Spore lizard is jumping");
            _controller!.Jumping();
        }
        #endregion

        #region Base Methods
        internal override void Awake()
        {
            base.Awake();

            if (TryGetComponent(out _controller) && _controller != null)
            {
                _controller.OnStartControlling = OnStartRiding;
                _controller.OnStopControlling = OnStopRiding;
                _controller.OnMove = OnMove;
                _controller.OnJump = OnJump;
                
                _controller.EnemyCanJump = true;
                _controller.EnemyStrength = 3f;
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

            if(_ridingTrigger == null && IsOwnerPlayer)
                Utils.CallNextFrame(() => _controller!.AddTrigger("Ride"));

            _controller!.SetControlTriggerVisible();
        }

        internal override void OnRetrieveInBall()
        {
            base.OnRetrieveInBall();
            
            _controller!.SetControlTriggerVisible(false);
        }

        internal override void OnTamedFollowing()
        {
            base.OnTamedFollowing();

            SporeLizard.CalculateAnimationDirection();
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            base.OnEscapedFromBall(playerWhoThrewBall);

            if (Utils.IsHost)
                SporeLizard.StartCoroutine(PuffAndWait(SporeLizard));
        }

        public override void OnDestroy()
        {
            if (IsOwnerPlayer && ownerPlayer?.nightVision != null)
                ownerPlayer.nightVision.enabled = _nightVisionPreviouslyEnabled;

            SporeLizard.agentLocalVelocity = Vector3.zero;
            SporeLizard.CalculateAnimationDirection(0f);
            
            _controller!.StopControlling(true);
            Destroy(_controller!);
            
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
