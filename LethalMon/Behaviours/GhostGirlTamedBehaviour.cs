using GameNetcodeStuff;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;
using System.Collections;
using Unity.Netcode;

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

        internal readonly int TeleportingDamage = 20;

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

#if DEBUG
            ownerPlayer = Utils.AllPlayers.Where((p) => p.playerClientId == 0ul).First();
            ownClientId = 0ul;
            isOutsideOfBall = true;
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
#endif
        }

        internal override void LateUpdate()
        {
            base.LateUpdate();

            AnimateWalking();
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

            DefendOwnerFromClosestEnemy();
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

        internal void DropBlood()
        {
            if (ownerPlayer == null) return;

            var bloodObject = ownerPlayer.playerBloodPooledObjects[ownerPlayer.currentBloodIndex];

            bloodObject.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.up);
            bloodObject.transform.SetParent(ownerPlayer.isInElevator ? StartOfRound.Instance.elevatorTransform : StartOfRound.Instance.bloodObjectsContainer);

            var interactRay = new Ray(GhostGirl.transform.position + GhostGirl.transform.up * 2f, Vector3.down);
            if (Physics.Raycast(interactRay, out RaycastHit hit, 6f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
            {
                bloodObject.transform.position = hit.point - Vector3.down * 0.45f;
                ownerPlayer.RandomizeBloodRotationAndScale(bloodObject.transform);
                bloodObject.transform.localScale *= 2f;
                bloodObject.transform.gameObject.SetActive(value: true);
            }
            ownerPlayer.currentBloodIndex = (ownerPlayer.currentBloodIndex + 1) % ownerPlayer.playerBloodPooledObjects.Count;
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

        internal void DefendOwnerFromClosestEnemy()
        {
            if (ownerPlayer == null) return;

            // Check if enemy in sight
            /*var enemiesInRange = Physics.SphereCastAll(ownerPlayer.transform.position, 10f, Vector3.zero, 0f, ToInt([Mask.Enemies]), QueryTriggerInteraction.Ignore);
            foreach (var enemy in enemiesInRange)
            {
                LethalMon.Log("Found enemy");
                if (enemy.collider == null || !GhostGirl.CheckLineOfSightForPosition(enemy.collider.transform.position, 180f, 10)) continue;

                LethalMon.Log("Enemy in line of sight");
                if (enemy.collider.TryGetComponent(out targetEnemy))
                    SwitchToTamingBehaviour(TamingBehaviour.TamedDefending);
            }*/

            // Check if enemy in sight
            foreach (EnemyAI spawnedEnemy in RoundManager.Instance.SpawnedEnemies) // todo: maybe SphereCast with fixed radius instead of checking LoS for any enemy for performance?
            {
                if (spawnedEnemy?.transform != null && spawnedEnemy != GhostGirl && spawnedEnemy.gameObject.layer == (int)Utils.LayerMasks.Mask.Enemies && !spawnedEnemy.isEnemyDead && GhostGirl.CheckLineOfSightForPosition(spawnedEnemy.transform.position, 180f, 10))
                {
                    targetEnemy = spawnedEnemy;
                    //Physics.IgnoreCollision(GhostGirl.GetComponent<Collider>(), targetEnemy.GetComponent<Collider>());
                    SwitchToTamingBehaviour(TamingBehaviour.TamedDefending);
                    LethalMon.Log("Targeting " + spawnedEnemy.enemyType.name);
                    return;
                }
            }
        }
        #endregion

        #region RPCs
        [ServerRpc(RequireOwnership = false)]
        public void OnHitTargetEnemyServerRpc(NetworkObjectReference enemyRef)
        {
            var randomPosition = Utils.GetRandomNavMeshPositionOnRadius(GhostGirl.transform.position, 70f);
            var randomPositionDistance = Vector3.Distance(randomPosition, GhostGirl.transform.position);
            var farthestNode = GhostGirl.ChooseFarthestNodeFromPosition(GhostGirl.transform.position).position;
            var farthestNodeDistance = Vector3.Distance(farthestNode, GhostGirl.transform.position);

            OnHitTargetEnemyClientRpc(enemyRef, randomPositionDistance > farthestNodeDistance ? randomPosition : farthestNode);
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

            if (TeleportingDamage > 0 && targetEnemy.enemyType.canDie)
            {
                LethalMon.Log("Damaging enemy before teleporting.");
                targetEnemy.HitEnemyOnLocalClient(TeleportingDamage);
                DropBlood();
                if(targetEnemy.isEnemyDead && targetEnemy.dieSFX != null)
                    ownerPlayer!.movementAudio.PlayOneShot(targetEnemy.dieSFX, 0.4f);
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
