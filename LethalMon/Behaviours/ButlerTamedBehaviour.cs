using GameNetcodeStuff;
using System.Collections.Generic;
using System;
using Unity.Netcode;
using UnityEngine;
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

        const float CleanUpTime = 5f;
        float _timeCleaning = 0f;

        internal override bool CanDefend => false;
        internal override TargetType Targets => TargetType.Dead;

        #endregion

        #region Custom behaviours
        internal enum CustomBehaviour
        {
            RunTowardsDeadEnemy = 1,
            CleanUpEnemy
        }
        internal override List<Tuple<string, string, Action>>? CustomBehaviourHandler =>
        [
            new (CustomBehaviour.RunTowardsDeadEnemy.ToString(), "Runs towards a dead enemy...", OnRunTowardsDeadEnemy),
            new (CustomBehaviour.CleanUpEnemy.ToString(), "Is cleaning up an enemy...", OnCleanUpEnemy)
        ];

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
                    _timeCleaning = Butler.creatureAnimator.GetInteger("HeldItem") == 1 ? 0f : -2f; // More if not sweeping previously
                    Butler.creatureAnimator.SetInteger("HeldItem", 1);
                    Butler.agent.speed = 0f;

                    Butler.creatureAnimator.SetBool("Running", false);
                    Butler.creatureAnimator.SetBool("Sweeping", true);
                    Butler.sweepingAudio.Play();
                    break;

                default:
                    break;
            }
        }

        internal void OnRunTowardsDeadEnemy()
        {
            if (!HasTargetEnemy)
            {
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                return;
            }

            TurnTowardsPosition(targetEnemy!.transform.position);

            if (Vector3.Distance(Butler.transform.position, targetEnemy.transform.position) < 1.5f)
                SwitchToCustomBehaviour((int)CustomBehaviour.CleanUpEnemy);
        }

        internal void OnCleanUpEnemy()
        {
            if (!HasTargetEnemy)
            {
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                return;
            }

            _timeCleaning += Time.deltaTime;
            if (_timeCleaning > CleanUpTime)
            {
                _timeCleaning = 0f;
                EnemyCleanedUpServerRpc(targetEnemy!.NetworkObject);
                Butler.SetSweepingAnimServerRpc(false);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        internal void EnemyCleanedUpServerRpc(NetworkObjectReference enemyRef)
        {
            SpawnableItemWithRarity? spawnableItemWithRarity = null;
            GameObject? item = null;
            if (enemyRef.TryGet(out NetworkObject networkObject) && networkObject.TryGetComponent(out targetEnemy) && HasTargetEnemy)
            {
                item = Utils.TrySpawnRandomItemAtPosition(targetEnemy!.transform.position, out spawnableItemWithRarity);
                if (item == null)
                    LethalMon.Log("Unable to spawn an item after cleaning up the enemy.", LethalMon.LogType.Error);
                else
                    LethalMon.Log("Spawned " + item.GetComponent<GrabbableObject>().itemProperties.itemName + " from cleaning up enemy " + targetEnemy.enemyType.enemyName);
            }

            int scrapValue = spawnableItemWithRarity != null ? (int)(UnityEngine.Random.RandomRangeInt(spawnableItemWithRarity.spawnableItem.minValue + 25, spawnableItemWithRarity.spawnableItem.maxValue + 35) * RoundManager.Instance.scrapValueMultiplier) : 0;
            if (item != null)
            {
                RoundManager.Instance.totalScrapValueInLevel += scrapValue;
                item.GetComponent<GrabbableObject>().SetScrapValue(scrapValue);
            }
            EnemyCleanedUpClientRpc(enemyRef, item != null, item != null ? item.GetComponent<NetworkObject>() : new NetworkObjectReference(), scrapValue);
        }

        [ClientRpc]
        internal void EnemyCleanedUpClientRpc(NetworkObjectReference enemyRef, bool itemSpawned, NetworkObjectReference itemRef, int scrapValue)
        {
            Butler.creatureAnimator.SetInteger("HeldItem", 0);

            Vector3 enemyPos, enemySize = Vector3.one;
            if (enemyRef.TryGet(out NetworkObject networkObject) && networkObject.TryGetComponent(out targetEnemy) && HasTargetEnemy)
            {
                enemyPos = targetEnemy!.transform.position;

                if (Utils.TryGetRealEnemyBounds(targetEnemy, out var bounds))
                    enemySize = bounds.size;
            }
            else
            {
                LethalMon.Log("EnemyCleanedUpServerRpc: Unable to get enemy object.", LethalMon.LogType.Error);
                enemyPos = Enemy.transform.position;
            }

            var giftBox = Utils.GiftBoxItem;
            if (giftBox?.spawnPrefab != null && giftBox.spawnPrefab.TryGetComponent(out GiftBoxItem giftBoxItem))
            {
                var presentAudio = Instantiate(giftBoxItem.openGiftAudio);
                var presentParticles = Instantiate(giftBoxItem.PoofParticle);

                var presentParticlesTransform = presentParticles.transform;
                presentParticlesTransform.position = enemyPos;
                presentParticlesTransform.localScale = enemySize;
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
            
            if (itemSpawned && !Utils.IsHost)
                StartCoroutine(waitForGiftPresentToSpawnOnClient(itemRef, scrapValue));
        }
        
        private IEnumerator waitForGiftPresentToSpawnOnClient(NetworkObjectReference netItemRef, int scrapValue)
        {
            NetworkObject? netObject = null;
            float startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < 8f && !netItemRef.TryGet(out netObject))
            {
                yield return new WaitForSeconds(0.03f);
            }
            if (netObject == null)
            {
                Debug.Log("No network object found");
                yield break;
            }
            yield return new WaitForEndOfFrame();
            GrabbableObject component = netObject.GetComponent<GrabbableObject>();
            RoundManager.Instance.totalScrapValueInLevel += scrapValue;
            component.SetScrapValue(scrapValue);
        }
        #endregion

        #region Base Methods
        internal override void InitTamingBehaviour(TamingBehaviour behaviour)
        {
            // ANY CLIENT
            base.InitTamingBehaviour(behaviour);

            switch (behaviour)
            {
                case TamingBehaviour.TamedFollowing:
                    if (ownerPlayer == null) return;

                    if (Butler.agent != null)
                        Butler.agent.speed = 6f;
                    
                    Butler.creatureAnimator.SetBool("Running", false);
                    break;

                case TamingBehaviour.TamedDefending:
                    if (!HasTargetEnemy) return;

                    if (Butler.agent != null)
                        Butler.agent.speed = 9f;

                    Butler.creatureAnimator.SetBool("Running", true);
                    break;

                default: break;
            }
        }

        internal override void OnTamedFollowing()
        {
            base.OnTamedFollowing();

            TargetNearestEnemy();
        }

        internal override void OnFoundTarget()
        {
            SwitchToCustomBehaviour((int)CustomBehaviour.RunTowardsDeadEnemy);
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            // ANY CLIENT
            base.OnEscapedFromBall(playerWhoThrewBall);

            Butler.targetPlayer = playerWhoThrewBall;
            if (IsOwner)
            {
                Butler.berserkModeTimer = 8f;
                Butler.SwitchOwnershipAndSetToStateServerRpc(2, playerWhoThrewBall.actualClientId, 0f);
            }
        }

        internal override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            base.OnUpdate(update, doAIInterval);

            Butler.CalculateAnimationDirection();
        }

        internal override void Start()
        {
            base.Start();

            if (IsTamed)
            {
                Butler.agent.speed = 6f;
                Butler.ambience1.volume = 0f;
                Butler.ambience2.volume = 0f;
            }
        }

        #endregion
    }
}