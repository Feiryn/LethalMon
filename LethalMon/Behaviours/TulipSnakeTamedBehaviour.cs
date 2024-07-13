using GameNetcodeStuff;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LethalMon.Behaviours
{
    internal class TulipSnakeTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        internal override bool Controllable => true;

        internal FlowerSnakeEnemy tulipSnake { get; private set; }

        internal InteractTrigger? flyingTrigger = null;

        internal bool IsFlying => CurrentCustomBehaviour == (int)CustomBehaviour.Flying;
        internal bool IsOwnerPlayer => ownerPlayer == Utils.CurrentPlayer;

        internal EnemyController? controller = null;
        #endregion

        #region Custom behaviours
        private enum CustomBehaviour
        {
            Flying = 1,
        }
        internal override List<Tuple<string, Action>>? CustomBehaviourHandler => new List<Tuple<string, Action>>()
        {
            { new Tuple<string, Action>(CustomBehaviour.Flying.ToString(), WhileFlying) },
        };

        void WhileFlying()
        {
            tulipSnake.CalculateAnimationSpeed();
            //tulipSnake.SetClingingAnimationPosition();

            if (Utils.IsHost)
            {
                bool flying = !Physics.Raycast(new Ray(ownerPlayer!.transform.position, Vector3.down), out _, ownerPlayer!.transform.localScale.y / 2f + 0.03f, ownerPlayer!.walkableSurfacesNoPlayersMask /*StartOfRound.Instance.allPlayersCollideWithMask*/, QueryTriggerInteraction.Ignore);
                if (flying)
                {
                    if (!tulipSnake.flapping)
                    {
                        tulipSnake.SetFlappingLocalClient(setFlapping: true, isMainSnake: true);
                        tulipSnake.SetFlappingClientRpc(setFlapping: true);
                    }
                }
                else
                {
                    if (tulipSnake.flapping)
                    {
                        tulipSnake.SetFlappingLocalClient(setFlapping: false, isMainSnake: true);
                        tulipSnake.SetFlappingClientRpc(setFlapping: false);
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

            if (IsOwnerPlayer)
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true);
        }

        internal void OnStopFlying()
        {
            if(Utils.IsHost)
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);

            if (IsOwnerPlayer)
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, false);

            tulipSnake.flapping = false;
            tulipSnake.SetFlappingLocalClient(false);
            tulipSnake.SetFlappingClientRpc(false);
        }

        internal void OnMove(Vector3 direction)
        {
        }

        internal void OnJump()
        {
        }
        #endregion

        #region Base Methods

        void Start()
        {
            base.Start();
            
            tulipSnake = (Enemy as FlowerSnakeEnemy)!;
            
            if (ownerPlayer != null)
            {
                tulipSnake.creatureVoice.volume = 0f;
                tulipSnake.SetFlappingLocalClient(false);
            }
        }
        
        void Awake()
        {
            tulipSnake = (Enemy as FlowerSnakeEnemy)!;

            if (TryGetComponent(out controller) && controller != null)
            {
                controller.OnStartControlling = OnStartFlying;
                controller.OnStopControlling = OnStopFlying;
                controller.OnMove = OnMove;

                controller.EnemyCanFly = true;
                controller.OnJump = OnJump;
                controller.EnemySpeedOutside = 8f;
                controller.EnemyDuration = 3f;
                controller.EnemyOffsetWhileControlling = new Vector3(0f, 2.4f, 0f);
                controller.EnemyStaminaUseMultiplier = 1.5f;

                // Debug
                /*ownerPlayer = Utils.AllPlayers.Where((p) => p.playerClientId == 0ul).First();
                isOutsideOfBall = true;
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                controller!.enemy = GetComponent<EnemyAI>();
                controller!.AddTrigger("Fly");
                controller!.SetControlTriggerVisible(true);*/
            }
        }

        internal override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            base.OnUpdate(update, !IsFlying); // Don't attempt to SetDestination in Riding mode
        }

        internal override void OnCallFromBall()
        {
            base.OnCallFromBall();

            if(flyingTrigger == null && ownerPlayer == Utils.CurrentPlayer)
                controller!.AddTrigger("Fly");

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

            if(!tulipSnake.flapping)
            {
                tulipSnake.clingPosition = 0;
                tulipSnake.creatureAnimator.SetInteger("clingType", 0);
                tulipSnake.SetFlappingLocalClient(false);
            }
            
            //Enemy!.transform.position = new Vector3(Enemy!.transform.position.x, ownerPlayer!.gameplayCamera.transform.position.y, Enemy!.transform.position.z);

            tulipSnake.CalculateAnimationSpeed(0.5f);
            tulipSnake.DoChuckleOnInterval();
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            base.OnEscapedFromBall(playerWhoThrewBall);
        }
        #endregion
    }
}
