using GameNetcodeStuff;
using System.Collections.Generic;
using System;
using Unity.Netcode;
using UnityEngine;

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

        internal readonly int TeleportingDamage = 20;

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
            LethalMon.Log("OnRunningBackToOwner");

            if (ownerPlayer == null ||
                Vector3.Distance(GhostGirl.transform.position, ownerPlayer.transform.position) < 6f // Reached owner
                || Mathf.Abs(GhostGirl.transform.position.y - ownerPlayer.transform.position.y) > 100f) // Owner left/inserted factory
            {
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                return;
            }

            GhostGirl.SetDestinationToPosition(ownerPlayer.transform.position);
        }
        #endregion

        #region Base Methods
        internal override void LateUpdate()
        {
            base.LateUpdate();
#if DEBUG
            if(GhostGirl.enemyMeshEnabled)
                GhostGirl.EnableEnemyMesh(enable: true, overrideDoNotSet: true);
#endif
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
                GhostGirl.creatureVoice.clip = GhostGirl.skipWalkSFX;
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
                // teleport
                var position = GhostGirl.ChooseFarthestNodeFromPosition(GhostGirl.transform.position, true)?.position;
                if (!position.HasValue)
                {
                    // todo: search other point
                    LethalMon.Log("No teleport point found for ghost girl");
                    return;
                }

                DropBlood();

                GhostGirl.creatureVoice.Stop();

                GhostGirl.agent.Warp(position.Value);
                GhostGirl.UpdateEnemyPositionServerRpc(position.Value);

                targetEnemy.agent.Warp(position.Value);
                targetEnemy.UpdateEnemyPositionServerRpc(position.Value);

                if(TeleportingDamage > 0)
                    targetEnemy.HitEnemyOnLocalClient(TeleportingDamage);

                RoundManager.Instance.FlickerLights(flickerFlashlights: true, disableFlashlights: true);

                SwitchToCustomBehaviour((int)CustomBehaviour.RunningBackToOwner);
            }
            else
            {
                LethalMon.Log("GhostGirlTamedBehaviour: Moving to target");
                GhostGirl.agent.speed = 5.25f;
                GhostGirl.creatureAnimator.SetBool("Walk", value: true);
                GhostGirl.SetDestinationToPosition(targetEnemy.transform.position);
                GhostGirl.creatureVoice.volume = Mathf.Max((10f - distanceTowardsTarget) / 5f, 0f);
            }

        }

        internal void DropBlood()
        {
            if (ownerPlayer == null) return;

            var bloodObject = Instantiate(ownerPlayer.playerBloodPooledObjects[ownerPlayer.currentBloodIndex]);

            var direction = GhostGirl.transform.position - ownerPlayer.transform.position;
            bloodObject.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            bloodObject.transform.SetParent(ownerPlayer.isInElevator ? StartOfRound.Instance.elevatorTransform : StartOfRound.Instance.bloodObjectsContainer);

            var interactRay = new Ray(GhostGirl.transform.position + GhostGirl.transform.up * 2f, direction);
            if (Physics.Raycast(interactRay, out RaycastHit hit, 6f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
            {
                bloodObject.transform.position = hit.point - direction.normalized * 0.45f;
                ownerPlayer.RandomizeBloodRotationAndScale(bloodObject.transform);
                bloodObject.transform.gameObject.SetActive(value: true);
            }
            ownerPlayer.currentBloodIndex = (ownerPlayer.currentBloodIndex + 1) % ownerPlayer.playerBloodPooledObjects.Count;
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            base.OnEscapedFromBall(playerWhoThrewBall);
        }

        internal void DefendOwnerFromClosestEnemy()
        {
            if (ownerPlayer == null) return;

            // Check if enemy in sight
            var enemiesInRange = Physics.SphereCastAll(ownerPlayer.transform.position, 10f, Vector3.zero, 0f, ToInt([Mask.Enemies]), QueryTriggerInteraction.Ignore);
            foreach (var enemy in enemiesInRange)
            {
                if (enemy.collider == null || !GhostGirl.CheckLineOfSightForPosition(enemy.collider.transform.position, 180f, 10)) continue;

                LethalMon.Log("Found enemy");
                if (enemy.collider.TryGetComponent(out targetEnemy))
                    SwitchToTamingBehaviour(TamingBehaviour.TamedDefending);
            }
        }
        #endregion
    }
}
