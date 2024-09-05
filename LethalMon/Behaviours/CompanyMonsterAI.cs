using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace LethalMon.Behaviours
{
    internal class CompanyMonsterAI : EnemyAI
    {
        #region Properties
        // Prefabs
        internal static GameObject? companyMonsterPrefab = null;
        internal GameObject? tentaclePrefab = null;

        // Components
        internal BoxCollider? collider = null;
        internal Animator? monsterAnimator = null;

        internal bool enemyMeshEnabled = true;

        internal DepositItemsDesk? deskInside = null;
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
            Utils.EnemyTypes.Add(enemyType);

            if (companyMonsterPrefab.TryGetComponent(out EnemyAICollisionDetect collisionDetect))
                collisionDetect.mainScript = companyMonsterAIPrefab;
            else
                LethalMon.Log("Unable to find EnemyAICollisionDetect component of company monster", LethalMon.LogType.Error);

            companyMonsterAIPrefab.creatureVoice = companyMonsterPrefab.GetComponent<AudioSource>();

            LethalMon.Log("Loaded company monster prefab.");
        }

        internal static void ExtractTentaclesFromItemDesk(DepositItemsDesk depositItemsDesk)
        {
            var companyMonsterAI = companyMonsterPrefab?.GetComponent<CompanyMonsterAI>();
            if(companyMonsterAI == null)
            {
                LethalMon.Log("Unable to attach company monster tentacles. No EnemyAI.", LethalMon.LogType.Error);
                return;
            }

            if (companyMonsterAI == null || companyMonsterAI.tentaclePrefab != null) return; // Asset not loaded or tentacles already extracted

            if (depositItemsDesk.monsterAnimations.Length == 0)
            {
                LethalMon.Log("Unable to attach company monster tentacles. No monster animations.", LethalMon.LogType.Error);
                return;
            }

            var tentacleAnim = depositItemsDesk.monsterAnimations.First();
            companyMonsterAI.tentaclePrefab = Instantiate(tentacleAnim.monsterAnimator.gameObject, companyMonsterPrefab?.transform);
            if(companyMonsterAI.tentaclePrefab == null)
            {
                LethalMon.Log("Unable to attach company monster tentacles. Failed to instantiate monster animation.", LethalMon.LogType.Error);
                return;
            }

            LethalMon.Log("Extracted tentacles from item desk.");
        }

        internal static void AttachToItemsDesk(DepositItemsDesk __instance)
        {
            if(companyMonsterPrefab == null)
            {
                LethalMon.Log("Failed to attach CompanyMonsterAI to item desk. No prefab.", LethalMon.LogType.Error);
                return;
            }

            var companyMonsterObject = Instantiate(companyMonsterPrefab, __instance.transform.position, Quaternion.Euler(Vector3.zero));
            companyMonsterObject.gameObject.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
            companyMonsterObject.transform.localScale = Vector3.one * 1.5f;
            var companyMonsterAI = companyMonsterObject.GetComponent<CompanyMonsterAI>();
            if (companyMonsterAI != null)
            {
                companyMonsterAI.enemyMeshEnabled = true;
                companyMonsterAI.deskInside = __instance;
            }
        }
        #endregion

        #region Base Methods
        void Start()
        {
            moveTowardsDestination = false;
            base.Start();

            collider = GetComponent<BoxCollider>();
            monsterAnimator = tentaclePrefab?.GetComponent<Animator>();

            EnableEnemyMesh(enemyMeshEnabled);

        }

        void Update()
        {
            base.Update();
            if(deskInside != null)
                transform.position = deskInside.deskObjectsContainer.transform.position;
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();
        }

        public override void EnableEnemyMesh(bool enable, bool overrideDoNotSet = false)
        {
            base.EnableEnemyMesh(enable, overrideDoNotSet);

            if (collider != null)
                collider.enabled = enable;

            enemyMeshEnabled = enable;
        }
        #endregion
    }
}
