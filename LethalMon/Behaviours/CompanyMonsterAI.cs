using GameNetcodeStuff;
using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.Behaviours
{
    internal class CompanyMonsterAI : EnemyAI
    {
        #region Properties
        // Prefabs
        internal static GameObject? companyMonsterPrefab = null;
        internal GameObject? tentaclePrefab = null;

        // Components
        internal SphereCollider? collider = null;
        internal Animator? monsterAnimator = null;
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
            companyMonsterAI.tentaclePrefab = Instantiate(tentacleAnim.monsterAnimator.gameObject);
            companyMonsterAI.tentaclePrefab.SetActive(false);
            if(companyMonsterAI.tentaclePrefab == null)
            {
                LethalMon.Log("Unable to attach company monster tentacles. Failed to instantiate monster animation.", LethalMon.LogType.Error);
                return;
            }

            LethalMon.Log("Extracted tentacles from item desk.");
        }
        #endregion

        #region Base Methods
        public override void Start()
        {
            moveTowardsDestination = false;
            base.Start();

            eye = transform.Find("Eye");

            collider = GetComponent<SphereCollider>();
            if (collider == null)
            {
                LethalMon.Log("No collider for company monster");
            }

            if(tentaclePrefab != null)
            {
                monsterAnimator = tentaclePrefab.GetComponent<Animator>();
                if (eye != null)
                {
                    tentaclePrefab.SetActive(true);
                    tentaclePrefab.transform.position = eye.position;
                    tentaclePrefab.transform.SetParent(eye, false);
                }
            }

            transform.position += Vector3.up;
        }

        public override void Update()
        {
            base.Update();
        }

        public override void DoAIInterval()
        {
            base.DoAIInterval();
        }
        #endregion
    }
}
