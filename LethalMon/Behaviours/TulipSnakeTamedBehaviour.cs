using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LethalMon.Behaviours
{
    internal class TulipSnakeTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        internal FlowerSnakeEnemy? _tulipSnake = null;
        internal FlowerSnakeEnemy TulipSnake
        {
            get
            {
                if (_tulipSnake == null)
                    _tulipSnake = (Enemy as FlowerSnakeEnemy)!;

                return _tulipSnake;
            }
        }

        public override bool Controllable => true;
        private InteractTrigger? _flyingTrigger = null;

        internal bool IsFlying => CurrentCustomBehaviour == (int)CustomBehaviour.Flying;

        private EnemyController? _controller = null;
        #endregion

        #region Custom behaviours
        private enum CustomBehaviour
        {
            Flying = 1,
        }
        public override List<Tuple<string, string, Action>>? CustomBehaviourHandler =>
        [
            new Tuple<string, string, Action>(CustomBehaviour.Flying.ToString(), "Is flying with you...", WhileFlying),
        ];

        void WhileFlying()
        {
            TulipSnake.CalculateAnimationSpeed();

            if (Utils.IsHost)
            {
                bool flying = !Physics.Raycast(new Ray(ownerPlayer!.transform.position, Vector3.down), out _, ownerPlayer!.transform.localScale.y / 2f + 0.03f, ownerPlayer!.walkableSurfacesNoPlayersMask, QueryTriggerInteraction.Ignore);
                if (flying)
                {
                    if (!TulipSnake.flapping)
                    {
                        TulipSnake.SetFlappingLocalClient(setFlapping: true, isMainSnake: true);
                        TulipSnake.SetFlappingClientRpc(setFlapping: true);
                    }
                }
                else
                {
                    if (TulipSnake.flapping)
                    {
                        TulipSnake.SetFlappingLocalClient(setFlapping: false, isMainSnake: true);
                        TulipSnake.SetFlappingClientRpc(setFlapping: false);
                    }
                }
            }
        }
        #endregion

        #region Controlling
        internal void OnStartFlying()
        {
            if (Utils.IsHost)
                SwitchToCustomBehaviour((int)CustomBehaviour.Flying);
        }

        internal void OnStopFlying()
        {
            if(Utils.IsHost)
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);

            TulipSnake.flapping = false;
            TulipSnake.SetFlappingLocalClient(false);
            TulipSnake.SetFlappingClientRpc(false);
        }

        internal void OnMove(Vector3 direction)
        {
        }

        internal void OnJump()
        {
        }
        #endregion

        #region Base Methods
        public override void Start()
        {
            base.Start();
            
            if (IsTamed)
            {
                TulipSnake.creatureVoice.volume = 0f;
                TulipSnake.SetFlappingLocalClient(false);
            }
        }
        
        public override void Awake()
        {
            base.Awake();

            if (TryGetComponent(out _controller) && _controller != null)
            {
                _controller.OnStartControlling = OnStartFlying;
                _controller.OnStopControlling = OnStopFlying;
                _controller.OnMove = OnMove;
                
                _controller.enemyCanFly = true;
                _controller.OnJump = OnJump;
                _controller.enemySpeedOutside = 8f;
                _controller.enemyDuration = 3f;
                _controller.enemyOffsetWhileControlling = new Vector3(0f, 2.4f, 0f);
                _controller.enemyStaminaUseMultiplier = 1.5f;
            }
        }

        public override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            base.OnUpdate(update, !IsFlying); // Don't attempt to SetDestination in Riding mode
        }

        public override void OnCallFromBall()
        {
            base.OnCallFromBall();

            if(_flyingTrigger == null && IsOwnerPlayer)
                Utils.CallNextFrame(() => _controller!.AddTrigger("Fly"));

            _controller!.SetControlTriggerVisible();
        }

        public override void OnRetrieveInBall()
        {
            base.OnRetrieveInBall();
            
            _controller!.SetControlTriggerVisible(false);
        }

        public override void OnTamedFollowing()
        {
            base.OnTamedFollowing();

            if(!TulipSnake.flapping)
            {
                TulipSnake.clingPosition = 0;
                TulipSnake.creatureAnimator.SetInteger("clingType", 0);
                TulipSnake.SetFlappingLocalClient(false);
            }
            
            TulipSnake.CalculateAnimationSpeed(0.5f);
            TulipSnake.DoChuckleOnInterval();
        }

        public override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            base.OnEscapedFromBall(playerWhoThrewBall);

            if (Utils.IsHost)
            {
                if (playerWhoThrewBall == null || !playerWhoThrewBall.isPlayerControlled ||
                    playerWhoThrewBall.isPlayerDead) return;

                var distance = Vector3.Distance(playerWhoThrewBall.transform.position, TulipSnake.transform.position);
                if (distance > 50f) return;

                var hasLineOfSight =
                    TulipSnake.CheckLineOfSightForPosition(playerWhoThrewBall.transform.position, 180f);

                LethalMon.Log("TulipSnake.OnEscapedFromBall distance: " + distance + " / LOS: " + hasLineOfSight);
                if (distance > 15f || !hasLineOfSight)
                {
                    // DOAIInterval case 0
                    TulipSnake.SetMovingTowardsTargetPlayer(playerWhoThrewBall);
                    TulipSnake.timeSinceSeeingTarget = 0f;
                    TulipSnake.SwitchToBehaviourServerRpc(1);
                    return;
                }

                // DOAIInterval case 1
                TulipSnake.targetPlayer = playerWhoThrewBall;
                Vector3 vector = TulipSnake.targetPlayer.transform.position - base.transform.position;
                vector += UnityEngine.Random.insideUnitSphere * UnityEngine.Random.Range(0.05f, 0.15f);
                vector.y = Mathf.Clamp(vector.y, -16f, 16f);
                vector = Vector3.Normalize(vector * 1000f);
                TulipSnake.StartLeapOnLocalClient(vector);
                TulipSnake.StartLeapClientRpc(vector);
            }
        }
        
        public override void OnDestroy()
        {
            _controller!.StopControlling(true);
            Destroy(_controller!);
            
            base.OnDestroy();
        }
        #endregion
    }
}
