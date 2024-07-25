using GameNetcodeStuff;
using System.Collections.Generic;
using System;
using Unity.Netcode;
using UnityEngine;
using static LethalMon.Utils.LayerMasks;
using HarmonyLib;
using System.Collections;

namespace LethalMon.Behaviours
{
    internal class ButlerTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        private ButlerEnemyAI? _butler = null;
        internal ButlerEnemyAI Butler
        {
            get
            {
                if (_butler == null)
                    _butler = (Enemy as ButlerEnemyAI)!;

                return _butler;
            }
        }

        internal readonly float CleanUpTime = 5f;
        internal float timeCleaning = 0f;
        #endregion

        #region Custom behaviours
        internal enum CustomBehaviour
        {
            RunTowardsDeadEnemy = 1,
            CleanUpEnemy
        }
        internal override List<Tuple<string, string, Action>>? CustomBehaviourHandler => new()
        {
            { new (CustomBehaviour.RunTowardsDeadEnemy.ToString(), "Runs towards a dead enemy...", OnRunTowardsDeadEnemy) },
            { new (CustomBehaviour.CleanUpEnemy.ToString(), "Is cleaning up an enemy...", OnCleanUpEnemy) }
        };

        internal override void InitCustomBehaviour(int behaviour)
        {
            base.InitCustomBehaviour(behaviour);

            switch ((CustomBehaviour)behaviour)
            {
                case CustomBehaviour.RunTowardsDeadEnemy:
                    if (targetEnemy == null) return;

                    Butler.agent.speed = 9f;
                    if (IsOwner)
                        Butler.SetButlerRunningServerRpc(true);

                    Butler.SetDestinationToPosition(targetEnemy!.transform.position);
                    break;
                case CustomBehaviour.CleanUpEnemy:
                    timeCleaning = Butler.creatureAnimator.GetInteger("HeldItem") == 1 ? 0f : -2f; // More if not sweeping previously
                    Butler.creatureAnimator.SetInteger("HeldItem", 1);
                    Butler.agent.speed = 0f;
                    if (IsOwner)
                    {
                        Butler.SetButlerRunningServerRpc(false);
                        Butler.SetSweepingAnimServerRpc(true);
                    }
                    break;

                default:
                    break;
            }
        }

        internal void OnRunTowardsDeadEnemy()
        {
            if (targetEnemy == null)
            {
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                return;
            }

            TurnTowardsPosition(targetEnemy.transform.position);

            if (Vector3.Distance(Butler.transform.position, targetEnemy.transform.position) < 1.5f)
                SwitchToCustomBehaviour((int)CustomBehaviour.CleanUpEnemy);
        }

        internal void OnCleanUpEnemy()
        {
            if (targetEnemy == null)
            {
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                return;
            }

            timeCleaning += Time.deltaTime;
            if (timeCleaning > CleanUpTime)
            {
                timeCleaning = 0f;
                EnemyCleanedUpServerRpc(targetEnemy.NetworkObject);
                Butler.SetSweepingAnimServerRpc(false);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        internal void EnemyCleanedUpServerRpc(NetworkObjectReference enemyRef)
        {
            if (enemyRef.TryGet(out NetworkObject networkObject) && networkObject.TryGetComponent(out targetEnemy) && targetEnemy != null)
            {
                if (!Utils.TrySpawnRandomItemAtPosition(targetEnemy.transform.position, out GrabbableObject? item))
                    LethalMon.Log("Unable to spawn an item after cleaning up the enemy.", LethalMon.LogType.Error);
                else
                    LethalMon.Log("Spawned " + item!.itemProperties.itemName + " from cleaning up enemy " + targetEnemy.enemyType.enemyName);
            }

            EnemyCleanedUpClientRpc(enemyRef);
        }

        [ClientRpc]
        internal void EnemyCleanedUpClientRpc(NetworkObjectReference enemyRef)
        {
            Butler.creatureAnimator.SetInteger("HeldItem", 0);

            Vector3 enemyPos, enemySize = Vector3.one;
            if (enemyRef.TryGet(out NetworkObject networkObject) && networkObject.TryGetComponent(out targetEnemy) && targetEnemy != null)
            {
                enemyPos = targetEnemy.transform.position;
                var bounds = Utils.RealEnemyBounds(targetEnemy);
                if(bounds.HasValue)
                    enemySize = bounds.Value.size;
            }
            else
            {
                LethalMon.Log("EnemyCleanedUpServerRpc: Unable to get enemy object.", LethalMon.LogType.Error);
                enemyPos = Enemy.transform.position;
            }

            var giftBox = Utils.itemByType(Utils.VanillaItem.Gift)?.spawnPrefab?.GetComponent<GiftBoxItem>();
            if (giftBox != null)
            {
                var presentAudio = Instantiate(giftBox.openGiftAudio);
                var presentParticles = Instantiate(giftBox.PoofParticle);

                presentParticles.transform.position = enemyPos;
                presentParticles.transform.localScale = enemySize;
                presentParticles.Play();

                Utils.PlaySoundAtPosition(Butler.transform.position, presentAudio);

                Destroy(presentAudio, 1f);
                Destroy(presentParticles, 1f);
            }

            if(Utils.IsHost)
                RoundManager.Instance.DespawnEnemyGameObject(enemyRef);
            else if(targetEnemy != null)
                RoundManager.Instance.SpawnedEnemies.Remove(targetEnemy);
            targetEnemy = null;

            if(IsOwner)
            {
                var target = NearestEnemy();
                if (target != null)
                {
                    targetEnemy = target;
                    SwitchToCustomBehaviour((int)CustomBehaviour.RunTowardsDeadEnemy);
                }
                else
                    SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
            }
        }
        #endregion

        #region Base Methods
        internal override void InitTamingBehaviour(TamingBehaviour behaviour)
        {
            // OWNER ONLY
            base.InitTamingBehaviour(behaviour);

            switch (behaviour)
            {
                case TamingBehaviour.TamedFollowing:
                    if (ownerPlayer == null) return;

                    Butler.agent.speed = 6f;
                    if (IsOwner)
                        Butler.SetButlerRunningServerRpc(false);
                    break;

                case TamingBehaviour.TamedDefending:
                    if (targetEnemy == null) return;

                    Butler.agent.speed = 9f;
                    if (IsOwner)
                        Butler.SetButlerRunningServerRpc(true);
                    break;

                default: break;
            }
        }

        internal override void OnTamedFollowing()
        {
            base.OnTamedFollowing();

            TargetNearestEnemy();
        }

        internal override bool EnemyMeetsTargetingConditions(EnemyAI enemyAI)
        {
            return enemyAI.gameObject.layer != (int)Mask.EnemiesNotRendered && enemyAI.isEnemyDead;
        }

        internal override void OnFoundTarget()
        {
            SwitchToCustomBehaviour((int)CustomBehaviour.RunTowardsDeadEnemy);
        }

        internal override void OnTamedDefending()
        {
            // OWNER ONLY
            base.OnTamedDefending();
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            // ANY CLIENT
            base.OnEscapedFromBall(playerWhoThrewBall);

            if (IsOwner)
            {
                Butler.targetPlayer = playerWhoThrewBall;
                Butler.berserkModeTimer = 8f;
                Butler.SwitchOwnershipAndSetToStateServerRpc(2, playerWhoThrewBall.actualClientId, 0f);
            }
        }

        internal override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            base.OnUpdate(update, doAIInterval);

            Butler.CalculateAnimationDirection();
        }
        #endregion
    }
}