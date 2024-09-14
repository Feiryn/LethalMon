using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.Behaviours
{
    internal class CompanyMonsterAI : EnemyAI
    {
        #region Properties
        // Prefabs
        internal static GameObject? companyMonsterPrefab = null;
        internal static GameObject? tentaclePrefab = null;

        internal GameObject? model = null;
        internal GameObject? tentacleContainer = null;
        internal List<GameObject> tentacles = [];
        internal const string TentaclePrefabFileName = "companymonsterTentacles.prefab"; // wip
        internal const int TentacleCount = 3;
        internal const float TentacleSize = 0.5f; // wip

        // Audio
        internal CompanyMood? mood = null;

        // Components
        internal SphereCollider? collider = null;

        // Variables
        internal bool isAttacking = false;
        internal float tentacleScale = 0f;
        internal bool initialized = false;

        internal Dictionary<GrabbableObject, int> caughtItems = [];
        internal Dictionary<string, int> caughtEnemies = []; // EnemyType.name, amount

        internal bool tentacleAnimationFinished = false;

        internal float sellQuota = 1f;
        internal const float SellQuotaReductionPerItem = 0.05f;
        internal const float MinimumSellQuota = 0.25f;
        #endregion

        #region Initialization
        public static void LoadPrefab(AssetBundle assetBundle)
        {
            var enemyType = assetBundle.LoadAsset<EnemyType>("Assets/Enemies/CompanyMonster/EnemyType.asset");
            if (enemyType == null || enemyType.enemyPrefab == null)
            {
                LethalMon.Log("Unable to find CompanyMonster assets", LethalMon.LogType.Error);
                return;
            }
            enemyType.name = enemyType.enemyName; // todo: remove when changed to enemyName in PokeballItem.OnTriggerEnter
            companyMonsterPrefab = enemyType.enemyPrefab;

            var companyMonsterAIPrefab = companyMonsterPrefab.AddComponent<CompanyMonsterAI>();
            companyMonsterAIPrefab.enemyType = enemyType;

            var collisionDetect = companyMonsterPrefab.GetComponentInChildren<EnemyAICollisionDetect>();
            if(collisionDetect == null)
                LethalMon.Log("Unable to find EnemyAICollisionDetect component of company monster", LethalMon.LogType.Error);
            else
                collisionDetect.mainScript = companyMonsterAIPrefab;

            Utils.EnemyTypes.Add(enemyType);
            LethalMon.Log("Loaded company monster EnemyType.");
        }
        #endregion

        #region Base Methods
        public override void Start()
        {
            moveTowardsDestination = false;
            base.Start();

            if (initialized) return;
            initialized = true;

            model = transform.Find("CompanyMonsterModel")?.gameObject;
            if(model == null)
            {
                LethalMon.Log("Unable to load company monster mesh.", LethalMon.LogType.Error);
                return;
            }

            model.transform.localScale = Vector3.one * 0.42f;

            eye = transform.Find("Eye");
            var creatureSFXContainer = transform.Find("CreatureSFX");

            if (creatureSFX != null)
            {
                LethalMon.Log("Has creature voice");
            }
            if (creatureSFXContainer == null || !creatureSFXContainer.TryGetComponent(out creatureSFX))
            {
                LethalMon.Log("Failed to load audio source of company monster. Creating new one.", LethalMon.LogType.Warning);
                creatureSFX = Utils.CreateAudioSource(gameObject);
            }
            creatureSFX.volume = 0f;

            collider = model.GetComponent<SphereCollider>();
            if (collider == null)
                LethalMon.Log("No collider for company monster");

            if(tentacleContainer == null && tentaclePrefab != null && eye != null)
            {
                tentacleContainer = new GameObject("TentacleContainer");
                tentacleContainer.transform.SetPositionAndRotation(eye.transform.position - eye.transform.forward.normalized * 0.2f, eye.transform.rotation);
                tentacleContainer.transform.parent = eye;
                //tentacleContainer.transform.localScale = Vector3.one * TentacleSize;
                LethalMon.Log("Add tentacles for " + name);
                for (int i = 0; i < TentacleCount; i++)
                    AddTentacle();
            }

            agent.angularSpeed = 690f;
        }

        public override void Update()
        {
            base.Update();

            if (IsOwner && mood == null && TimeOfDay.Instance != null)
            {
                LethalMon.Log("Update mood");
                UpdateMoodServerRpc();
            }

            if (creatureSFX.isPlaying && creatureSFX.volume < 1f)
                creatureSFX.volume = Mathf.Lerp(creatureSFX.volume, 1f, Time.deltaTime);
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();
        }
        #endregion

        #region Tentacles
        private void AddTentacle()
        {
            if (tentacleContainer == null) return;

            var tentacleOrigin = new GameObject("TentacleOrigin");
            tentacleOrigin.transform.position = tentacleContainer.transform.position;
            tentacleOrigin.transform.SetParent(tentacleContainer.transform, true);

            var tentacle = Instantiate(tentaclePrefab, tentacleOrigin.transform.position, Quaternion.identity);
            if (tentacle == null) return;

            tentacle.transform.SetParent(tentacleOrigin.transform, true);
            tentacle.SetActive(true);
            tentacleOrigin.transform.localScale = Vector3.zero;

            tentacles.Add(tentacle);
        }

        private void ShowTentacles(bool show = true) => tentacles.ForEach(t => t?.GetComponent<Animator>()?.SetBool("visible", value: show));

        private void ScaleTentacles(float scale) => tentacles.ForEach(t => t.transform.parent.localScale = Vector3.one * scale);

        internal void RandomizeTentacleRotations() => tentacles.ForEach(t => t.transform.parent.localRotation = Quaternion.Euler(RandomRotation));
        #endregion

        #region Methods
        private Vector3 RandomRotation => new(UnityEngine.Random.Range(0, 360f), UnityEngine.Random.Range(0f, 180f), UnityEngine.Random.Range(0f, 360f));

        [ServerRpc(RequireOwnership = false)]
        internal void CaughtEnemyServerRpc(NetworkObjectReference enemyRef)
        {
            if(!enemyRef.TryGet(out NetworkObject networkObj) || !networkObj.TryGetComponent( out EnemyAI enemyAI))
            {
                LethalMon.Log("CaughtEnemyServerRpc: Unable to get EnemyAI.", LethalMon.LogType.Error);
                return;
            }

            CaughtEnemy(enemyAI);
        }

        internal void CaughtEnemy(EnemyAI enemy)
        {
            if (!caughtEnemies.ContainsKey(enemy.enemyType.name))
                caughtEnemies.Add(enemy.enemyType.name, 1);
            else
                caughtEnemies[enemy.enemyType.name]++;
        }

        internal void SpawnCaughtEnemiesOnServer()
        {
            var random = new System.Random();
            foreach(var enemy in caughtEnemies)
            {
                for(int i = 0; i < enemy.Value; i++)
                {
                    var position = RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(Vector3.zero, 200f, default, random);
                    Utils.SpawnEnemyAtPosition(enemy.Key, position);
                    LethalMon.Log($"Spawned enemy {enemy.Key} at position {position}");
                }
            }
            caughtEnemies.Clear();
        }

        [ServerRpc]
        internal void RedeemItemsServerRpc()
        {
            if(caughtItems.Count == 0) return;

            var depositItemsDesk = FindObjectOfType<DepositItemsDesk>();
            if(depositItemsDesk == null) return;

            depositItemsDesk.inSellingItemsAnimation = true;

            int profits = 0;
            foreach (var (item, profit) in caughtItems)
            {
                depositItemsDesk.itemsOnCounterNetworkObjects.Add(item.NetworkObject);
                depositItemsDesk.itemsOnCounter.Add(item);
                depositItemsDesk.AddObjectToDeskClientRpc(item.NetworkObject);
                if (item.itemProperties.isScrap)
                    profits += profit;
            }

            Terminal terminal = FindObjectOfType<Terminal>();
            terminal.groupCredits += profits;
            depositItemsDesk.SellItemsClientRpc(profits, terminal.groupCredits, caughtItems.Count, 1f); // or 100f?
            depositItemsDesk.SellAndDisplayItemProfits(profits, terminal.groupCredits);
        }

        [ServerRpc]
        public void UpdateMoodServerRpc()
        {
            if (TimeOfDay.Instance.CommonCompanyMoods.Length > 0)
                UpdateMoodClientRpc(UnityEngine.Random.RandomRangeInt(0, TimeOfDay.Instance.CommonCompanyMoods.Length - 1));
        }

        [ClientRpc]
        public void UpdateMoodClientRpc(int moodID)
        {
            LethalMon.Log("UpdateMoodClientRpc");
            if (TimeOfDay.Instance == null || TimeOfDay.Instance.CommonCompanyMoods.Length == 0) return;

            LethalMon.Log("UpdateMoodClientRpc 2");
            mood = TimeOfDay.Instance.CommonCompanyMoods[Mathf.Max(moodID, TimeOfDay.Instance.CommonCompanyMoods.Length - 1)];

            creatureSFX.clip = mood.behindWallSFX;
            creatureSFX.Play();
        }

        [ServerRpc]
        public void AttackServerRpc() => AttackClientRpc();

        [ClientRpc]
        public void AttackClientRpc() => StartCoroutine(Attack());

        internal IEnumerator Attack()
        {
            if(tentacles.Count == 0)
            {
                LethalMon.Log("CompanyMonster is unable to attack. No tentacles found.", LethalMon.LogType.Warning);
                yield break;
            }

            tentacleAnimationFinished = false;
            isAttacking = true;
            tentacleScale = 0f;

            LethalMon.Log("Tentacle count: " + tentacles.Count);

            RandomizeTentacleRotations();
            ShowTentacles(true);
            ScaleTentacles(0f);

            yield return new WaitForSeconds(0.5f);
            yield return new WaitWhile(() =>
            {
                tentacleScale += Time.deltaTime * 5f;
                ScaleTentacles(tentacleScale);
                return tentacleScale < 1f;
            });

            yield return new WaitForSeconds(3.5f);

            yield return new WaitWhile(() =>
            {
                tentacleScale -= Time.deltaTime * 5f;
                ScaleTentacles(tentacleScale);
                return tentacleScale > 0f;
            });

            ShowTentacles(false);
            tentacleAnimationFinished = true;
        }

        [ServerRpc]
        public void ReachOutForItemServerRpc(NetworkObjectReference itemRef) => ReachOutForItemClientRpc(itemRef);

        [ClientRpc]
        public void ReachOutForItemClientRpc(NetworkObjectReference itemRef)
        {
            if (!itemRef.TryGet(out NetworkObject networkObj) || !networkObj.TryGetComponent(out GrabbableObject item))
            {
                LethalMon.Log("GReachOutForItemClientRpc: Unable to get item.", LethalMon.LogType.Error);
                return;
            }

            StartCoroutine(ReachOutForItemCoroutine(item));
        }

        internal IEnumerator ReachOutForItemCoroutine(GrabbableObject item)
        {
            LethalMon.Log("GrabItemAndEatCoroutine");
            tentacleAnimationFinished = false;

            if (tentacles.Count == 0)
            {
                AddTentacle();
                if (tentacles.Count == 0)
                {
                    LethalMon.Log("CompanyMonsterAI: Unable to get item. No tentacles.");
                    yield break;
                }
            }

            var tentacle = tentacles[0];

            RoundManager.Instance.tempTransform.position = eye.transform.position;
            RoundManager.Instance.tempTransform.LookAt(item.transform.position);
            tentacle.transform.parent.rotation = RoundManager.Instance.tempTransform.rotation;
            tentacle.transform.parent.Rotate(0f, 90f, -90f); // wip

            if (!tentacle.TryGetComponent(out Animator tentacleAnimator))
            {
                LethalMon.Log("CompanyMonsterAI: Unable to get item. No tentacle animator.");
                yield break;
            }

            tentacleAnimator.SetBool("visible", true);

            tentacleScale = 0f;
            yield return new WaitForSeconds(0.5f);
            yield return new WaitWhile(() =>
            {
                tentacleScale += Time.deltaTime * 2f;
                tentacle.transform.parent.localScale = Vector3.one * tentacleScale;
                return tentacleScale < 1f;
            });

            yield return new WaitForSeconds(1f);

            yield return new WaitWhile(() =>
            {
                tentacleScale -= Time.deltaTime * 2f;
                tentacle.transform.parent.localScale = Vector3.one * tentacleScale;
                return tentacleScale > 0f;
            });

            tentacleAnimator.SetBool("visible", false);

            tentacleAnimationFinished = true;
        }

        internal void CaughtItem(GrabbableObject item)
        {
            item.gameObject.SetActive(false);
            DontDestroyOnLoad(item);

            sellQuota = Mathf.Max(sellQuota - SellQuotaReductionPerItem, MinimumSellQuota);
            var scrapValue = (int)(item.scrapValue * sellQuota);
            LethalMon.Log($"Caught item with a value of {scrapValue} (at {(int)(sellQuota * 100)}% quota)");

            caughtItems.Add(item, scrapValue);
        }
        #endregion
    }
}
