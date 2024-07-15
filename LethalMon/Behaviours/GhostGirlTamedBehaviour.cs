using GameNetcodeStuff;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Collections;
using Unity.Netcode;
using static LethalMon.Utils.LayerMasks;

namespace LethalMon.Behaviours
{
    internal class GhostGirlTamedBehaviour : TamedEnemyBehaviour
    {
        internal DressGirlAI? _ghostGirl = null;
        internal DressGirlAI GhostGirl
        {
            get
            {
                if (_ghostGirl == null)
                    _ghostGirl = (Enemy as DressGirlAI)!;

                return _ghostGirl;
            }
        }

        internal bool isWalking = false;
        internal Vector3 previousPosition = Vector3.zero;

        internal bool ownerInsideFactory = false;

        #region Custom behaviours
        private enum CustomBehaviour
        {
            RunningBackToOwner = 1
        }
        internal override List<Tuple<string, Action>>? CustomBehaviourHandler => new()
        {
            { new (CustomBehaviour.RunningBackToOwner.ToString(), OnRunningBackToOwner) }
        };

        internal override void InitCustomBehaviour(int behaviour)
        {
            base.InitCustomBehaviour(behaviour);

            switch((CustomBehaviour)behaviour)
            {
                case CustomBehaviour.RunningBackToOwner:
                    ownerInsideFactory = ownerPlayer!.isInsideFactory;
                    break;

                default:
                    break;
            }
        }

        public void OnRunningBackToOwner()
        {
            LethalMon.Log("OnRunningBackToOwner: " + GhostGirl.transform.position);

            AnimateWalking();

            if (ownerPlayer == null ||
                Vector3.Distance(GhostGirl.transform.position, ownerPlayer.transform.position) < 8f // Reached owner
                || ownerInsideFactory != ownerPlayer.isInsideFactory) // Owner left/inserted factory
            {
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, false);
                return;
            }

            GhostGirl.agent.speed = 8f;

            GhostGirl.SetDestinationToPosition(ownerPlayer.transform.position);
        }
        #endregion

        #region Action Keys
#if DEBUG
        private List<ActionKey> _actionKeys = new List<ActionKey>()
        {
            new ActionKey() { actionKey = ModConfig.Instance.ActionKey1, description = "Teleport to Ghost Girl" }
        };
        internal override List<ActionKey> ActionKeys => _actionKeys;

        internal override void ActionKey1Pressed()
        {
            base.ActionKey1Pressed();

            if (ownerPlayer != null && CurrentCustomBehaviour == (int)CustomBehaviour.RunningBackToOwner)
            {
                ownerPlayer.TeleportPlayer(GhostGirl.transform.position);
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
            }
        }
#endif
        #endregion

        #region Base Methods
        internal override void Start()
        {
            base.Start();

            /*#if DEBUG
                        ownerPlayer = Utils.AllPlayers.Where((p) => p.playerClientId == 0ul).First();
                        ownClientId = 0ul;
                        isOutsideOfBall = true;
                        SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
            #endif*/
            GhostGirl.EnableEnemyMesh(true, true);
        }

        internal override void LateUpdate()
        {
            base.LateUpdate();

            AnimateWalking();
            GhostGirl.EnableEnemyMesh(true, true);
        }

        internal override void InitTamingBehaviour(TamingBehaviour behaviour)
        {
            base.InitTamingBehaviour(behaviour);

            switch(behaviour)
            {
                case TamingBehaviour.TamedDefending:
                    LethalMon.Log("GhostGirl: Play breathingSFX");
                    GhostGirl.creatureVoice.clip = GhostGirl.breathingSFX;
                    GhostGirl.creatureVoice.Play();
                    break;

                default: break;
            }
        }

        internal override void OnTamedFollowing()
        {
            base.OnTamedFollowing();

            GhostGirl.agent.speed = ownerPlayer!.isSprinting ? 6f : 3f;

            if (!GhostGirl.enemyMeshEnabled)
            {
                GhostGirl.EnableEnemyMesh(enable: true, overrideDoNotSet: true);
                GhostGirl.enemyMeshEnabled = true;
            }

            TargetNearestEnemy();
        }

        internal override void OnTamedDefending()
        {
            base.OnTamedDefending();

            if (ownerPlayer == null) return;

            var distanceTowardsOwner = Vector3.Distance(GhostGirl.transform.position, ownerPlayer.transform.position);
            if (targetEnemy == null || targetEnemy.isEnemyDead || distanceTowardsOwner > 30f)
            {
                targetEnemy = null;
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                return;
            }

            var distanceTowardsTarget = Vector3.Distance(GhostGirl.transform.position, targetEnemy.transform.position);
            if (distanceTowardsTarget < 2f)
            {
                LethalMon.Log("GhostGirlTamedBehaviour: Teleporting enemy.");

                OnHitTargetEnemyServerRpc(targetEnemy.NetworkObject);
                SwitchToCustomBehaviour((int)CustomBehaviour.RunningBackToOwner);
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true);
            }
            else
            {
                LethalMon.Log("GhostGirlTamedBehaviour: Moving to target");
                GhostGirl.agent.speed = 5.25f;
                GhostGirl.SetDestinationToPosition(targetEnemy.transform.position);
                GhostGirl.creatureVoice.volume = Mathf.Max((20f - distanceTowardsTarget) / 15f, 0f);
            }
        }

        public void OnTriggerEnter(Collider other)
        {
            LethalMon.Log("OnTriggerEnter: " + other.name, LethalMon.LogType.Warning);
        }

        internal void AnimateWalking()
        {
            var currentlyWalking = Vector3.Distance(previousPosition, GhostGirl.transform.position) > 0.01f;
            if (currentlyWalking != isWalking)
            {
                isWalking = currentlyWalking;
                GhostGirl.creatureAnimator.SetBool("Walk", value: isWalking);
            }

            previousPosition = GhostGirl.transform.position;
        }

        internal void DropBlood(int minAmount = 3, int maxAmount = 7)
        {
            if (ownerPlayer == null) return;

            var amount = UnityEngine.Random.Range(minAmount, maxAmount);
            while(amount > 0)
            {
                amount--;
                ownerPlayer.currentBloodIndex = (ownerPlayer.currentBloodIndex + 1) % ownerPlayer.playerBloodPooledObjects.Count;
                var bloodObject = ownerPlayer.playerBloodPooledObjects[ownerPlayer.currentBloodIndex];
                if (bloodObject == null) continue;

                bloodObject.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.up);
                bloodObject.transform.SetParent(ownerPlayer.isInElevator ? StartOfRound.Instance.elevatorTransform : StartOfRound.Instance.bloodObjectsContainer);

                var randomDirection = new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), UnityEngine.Random.Range(-1f, -0.5f), UnityEngine.Random.Range(-0.5f, 0.5f));
                var interactRay = new Ray(GhostGirl.transform.position + Vector3.up * 2f, randomDirection);
                if (Physics.Raycast(interactRay, out RaycastHit hit, 6f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
                {
                    bloodObject.transform.position = hit.point - Vector3.down * 0.45f;
                    ownerPlayer.RandomizeBloodRotationAndScale(bloodObject.transform);
                    bloodObject.transform.gameObject.SetActive(value: true);
                }
            }
        }

        internal IEnumerator FlickerLightsAndTurnDownBreaker()
        {
            RoundManager.Instance.FlickerLights(flickerFlashlights: true, disableFlashlights: true);
            yield return new WaitForSeconds(1f);
            TurnOffBreakerNearbyServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        public void TurnOffBreakerNearbyServerRpc() // Original FlipLightsBreakerServerRpc isn't calling the correct ClientRpc.. zeekerss pls :p
        {
            TurnOffBreakerNearbyClientRpc();
        }

        [ClientRpc]
        public void TurnOffBreakerNearbyClientRpc()
        {
            var breakerBoxList = FindObjectsOfType<BreakerBox>();
            foreach(var breakerBox in breakerBoxList)
            {
                if (breakerBox != null && Vector3.Distance(breakerBox.transform.position, GhostGirl.transform.position) < 35f)
                {
                    breakerBox.SetSwitchesOff();
                    breakerBox.thisAudioSource.PlayOneShot(breakerBox.switchPowerSFX);
                }
            }
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            base.OnEscapedFromBall(playerWhoThrewBall);

            GhostGirl.hauntingLocalPlayer = playerWhoThrewBall == Utils.CurrentPlayer;
            GhostGirl.hauntingPlayer = playerWhoThrewBall;
            GhostGirl.BeginChasing();
        }
        #endregion

        #region RPCs
        [ServerRpc(RequireOwnership = false)]
        public void OnHitTargetEnemyServerRpc(NetworkObjectReference enemyRef)
        {
            var farthestNode = GhostGirl.ChooseFarthestNodeFromPosition(GhostGirl.transform.position).position;
            OnHitTargetEnemyClientRpc(enemyRef, farthestNode);
        }

        [ClientRpc]
        public void OnHitTargetEnemyClientRpc(NetworkObjectReference enemyRef, Vector3 newEnemyPosition)
        {
            if (!enemyRef.TryGet(out NetworkObject networkObject) || !networkObject.TryGetComponent(out targetEnemy) || targetEnemy == null)
            {
                LethalMon.Log("OnHitTargetEnemyClientRpc: Unable to get enemy object.", LethalMon.LogType.Error);
                return;
            }

            GhostGirl.creatureVoice.Stop();

            if (targetEnemy.enemyType.canDie)
            {
                LethalMon.Log("Damaging enemy before teleporting.");
                targetEnemy.HitEnemyOnLocalClient(force: 2);
                DropBlood();
                if(targetEnemy.isEnemyDead && targetEnemy.dieSFX != null)
                    Utils.PlaySoundAtPosition(GhostGirl.transform.position, targetEnemy.dieSFX, 0.5f);
                Utils.PlaySoundAtPosition(GhostGirl.transform.position, StartOfRound.Instance.bloodGoreSFX);
            }

            GhostGirl.agent.Warp(newEnemyPosition);
            targetEnemy.agent.Warp(newEnemyPosition);

            //Physics.IgnoreCollision(GhostGirl.GetComponent<Collider>(), targetEnemy.GetComponent<Collider>(), false);

            targetEnemy = null;

            RoundManager.Instance.StartCoroutine(FlickerLightsAndTurnDownBreaker());
        }
        #endregion
    }
}
