using GameNetcodeStuff;
using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;
using Steamworks;
using System.Collections;
using Unity.Netcode;
using UnityEngine.UIElements;

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

        #region Custom behaviours
        private enum CustomBehaviour
        {
            RunningBackToOwner = 1
        }
        internal override List<Tuple<string, Action>>? CustomBehaviourHandler => new()
        {
            { new (CustomBehaviour.RunningBackToOwner.ToString(), OnRunningBackToOwner) }
        };

        public void OnRunningBackToOwner()
        {
            LethalMon.Log("OnRunningBackToOwner: " + GhostGirl.transform.position);
            AnimateWalking();

            if (ownerPlayer == null ||
                Vector3.Distance(GhostGirl.transform.position, ownerPlayer.transform.position) < 6f // Reached owner
                || Mathf.Abs(GhostGirl.transform.position.y - ownerPlayer.transform.position.y) > 100f) // Owner left/inserted factory
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

        internal override void OnTamedFollowing()
        {
            base.OnTamedFollowing();

            if (!GhostGirl.enemyMeshEnabled)
                GhostGirl.EnableEnemyMesh(enable: true, overrideDoNotSet: true);

            DefendOwnerFromClosestEnemy();
        }

        internal override void OnTamedDefending()
        {
            base.OnTamedDefending();

            if (ownerPlayer == null) return;

            if( !GhostGirl.creatureVoice.isPlaying)
            {
                GhostGirl.creatureVoice.clip = GhostGirl.breathingSFX;
                GhostGirl.creatureVoice.Play();
            }

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
                if (breakerBox != null && Vector3.Distance(breakerBox.transform.position, GhostGirl.transform.position) < 15f)
                {
                    breakerBox.SetSwitchesOff();
                    breakerBox.thisAudioSource.PlayOneShot(breakerBox.switchPowerSFX);
                    RoundManager.Instance.TurnOnAllLights(on: false);
                }
            }
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            base.OnEscapedFromBall(playerWhoThrewBall);
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
                if (spawnedEnemy?.transform != null && spawnedEnemy != GhostGirl && !spawnedEnemy.isEnemyDead && GhostGirl.CheckLineOfSightForPosition(spawnedEnemy.transform.position, 180f, 10))
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
            var position = RoundManager.Instance.GetRandomNavMeshPositionInRadius(GhostGirl.transform.position, 70f);
            var distance = Vector3.Distance(position, GhostGirl.transform.position);
            if (distance < 35f)
                position = GhostGirl.ChooseFarthestNodeFromPosition(GhostGirl.transform.position).position;

            OnHitTargetEnemyClientRpc(enemyRef, position);
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

            if (TeleportingDamage > 0 && Enemy.enemyType.canDie)
            {
                targetEnemy.HitEnemyOnLocalClient(TeleportingDamage);
                DropBlood();
                Utils.PlaySoundAtPosition(GhostGirl.transform.position, StartOfRound.Instance.bloodGoreSFX);
            }

            GhostGirl.agent.enabled = false;
            GhostGirl.transform.position = newEnemyPosition;
            GhostGirl.agent.enabled = true;

            targetEnemy.agent.enabled = false;
            targetEnemy.transform.position = newEnemyPosition;
            /*if(Enemy is SandSpiderAI)
            {
                var spider = (Enemy as SandSpiderAI)!;
                spider.meshContainer.position = newEnemyPosition;
                spider.meshContainerPosition = newEnemyPosition;
                spider.meshContainerServerPosition = newEnemyPosition;
                spider.floorPosition = newEnemyPosition;
            }*/
            targetEnemy.agent.enabled = true;
            // Doesn't work for spiders or blobs yet, due to meshContainer and other things

            //Physics.IgnoreCollision(GhostGirl.GetComponent<Collider>(), targetEnemy.GetComponent<Collider>(), false);

            targetEnemy = null;

            RoundManager.Instance.StartCoroutine(FlickerLightsAndTurnDownBreaker());
        }
        #endregion
    }
}
