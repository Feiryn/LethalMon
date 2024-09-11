using GameNetcodeStuff;
using System.Collections;
using System.Collections.Generic;
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
        internal static GameObject? tentaclePrefab = null;

        //internal static MonsterAnimation? monsterAnimationPrefab = null;

        internal GameObject? companyMonsterMesh = null;
        internal GameObject? tentacleContainer = null;
        internal List<GameObject> tentacles = [];
        internal const string TentaclePrefabFileName = "companymonsterTentacles.prefab";
        internal const int TentacleCount = 3;

        // Audio
        internal CompanyMood? mood = null;

        // Components
        internal SphereCollider? collider = null;

        // Variables
        internal bool isAttacking = false;
        internal float tentacleScale = 0f;
        internal bool initialized = false;
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
        #endregion

        #region Base Methods
        public override void Start()
        {
            moveTowardsDestination = false;
            base.Start();

            if (initialized) return;
            initialized = true;

            companyMonsterMesh = transform.Find("CompanyMonsterMesh")?.gameObject;
            if(companyMonsterMesh == null)
            {
                LethalMon.Log("Unable to load company monster mesh.", LethalMon.LogType.Error);
                return;
            }

            companyMonsterMesh.transform.localScale = Vector3.one * 0.65f;

            eye = companyMonsterMesh.transform.Find("Eye");

            if (!companyMonsterMesh.TryGetComponent(out creatureSFX))
            {
                LethalMon.Log("Failed to load audio source of company monster. Creating new one.", LethalMon.LogType.Warning);
                creatureSFX = Utils.CreateAudioSource(companyMonsterMesh);
            }
            creatureSFX.volume = 0f;

            collider = companyMonsterMesh.GetComponent<SphereCollider>();
            if (collider == null)
                LethalMon.Log("No collider for company monster");

            if(tentacleContainer == null && tentaclePrefab != null && eye != null)
            {
                tentacleContainer = new GameObject("TentacleContainer");
                tentacleContainer.transform.SetPositionAndRotation(eye.transform.position - eye.transform.forward.normalized * 0.5f, eye.transform.rotation);
                tentacleContainer.transform.SetParent(eye, true);
                LethalMon.Log("Add tentacles for " + name);
                for (int i = 0; i < TentacleCount; i++)
                    AddTentacle();
            }

            agent.baseOffset = 0f;
        }

        private void AddTentacle()
        {
            if (tentacleContainer == null) return;

            var tentacle = Instantiate(tentaclePrefab);
            if (tentacle == null) return;

            tentacle.transform.SetParent(null, false);
            tentacle.transform.SetPositionAndRotation(tentacleContainer.transform.position - eye.transform.forward, RandomRotation);
            tentacle.transform.SetParent(tentacleContainer.transform, false);
            tentacle.SetActive(true);
            tentacle.transform.localScale = Vector3.zero;

            tentacles.Add(tentacle);
        }

        private void ShowTentacles(bool show = true) => tentacles.ForEach(t => t?.GetComponent<Animator>()?.SetBool("visible", value: show));

        private void ScaleTentacles(float scale) => tentacles.ForEach(t => t.transform.localScale = Vector3.one * scale);

        private void RandomizeTentacleRotations() => tentacles.ForEach(t => t.transform.localRotation = RandomRotation);

        private Quaternion RandomRotation => new(Random.Range(-1f, 1f), Random.Range(-0.3f, 0.3f), Random.Range(-1f, 1f), Random.Range(-1f, 1f));

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

        #region Methods
        [ServerRpc]
        public void UpdateMoodServerRpc()
        {
            if (TimeOfDay.Instance.CommonCompanyMoods.Length > 0)
                UpdateMoodClientRpc(Random.RandomRangeInt(0, TimeOfDay.Instance.CommonCompanyMoods.Length - 1));
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
        }
        #endregion

        #region CollisionDetect
        internal class CollisionDetect : MonoBehaviour
        {
            private CompanyMonsterAI? _companyMonster = null;
            private bool collided = false;

            void Start()
            {
                _companyMonster = GetComponentInParent<CompanyMonsterAI>();
                if (_companyMonster == null)
                {
                    LethalMon.Log("Unable to find company mosnter ai for tentacle collision detector.", LethalMon.LogType.Error);
                    Destroy(this);
                }
            }

            void OnTriggerEnter(Collider other)
            {
                if (collided) return;

                if(other.CompareTag("Player"))
                {
                    LethalMon.Log("Tentacle collided with player.");
                    StartCoroutine(OnColliderWithPlayer(other.GetComponent<PlayerControllerB>()));
                }
                else if (other.CompareTag("Enemy"))
                {
                    LethalMon.Log("Tentacle collided with enemy.");
                }
                else if(!other.CompareTag("Untagged"))
                    LethalMon.Log("Tentacle collided with tag: " + other.tag + ". Name: " + other.name);
            }

            IEnumerator OnColliderWithPlayer(PlayerControllerB player)
            {
                collided = true;
                Animator monsterAnimator = GetComponent<Animator>();
                Transform monsterAnimatorGrabTarget = gameObject.transform;
                monsterAnimator.SetBool("grabbingPlayer", value: true);
                monsterAnimatorGrabTarget.position = player.transform.position;
                yield return new WaitForSeconds(0.05f);
                if (player.IsOwner)
                {
                    player.KillPlayer(Vector3.zero);
                }
                float startTime = Time.realtimeSinceStartup;
                yield return new WaitUntil(() => player.deadBody != null || Time.realtimeSinceStartup - startTime > 4f);
                if (player.deadBody != null)
                {
                    player.deadBody.attachedTo = monsterAnimatorGrabTarget; // GrabPoint initially
                    player.deadBody.attachedLimb = player.deadBody.bodyParts[6];
                    player.deadBody.matchPositionExactly = true;
                }
                else
                {
                    Debug.Log("Player body was not spawned in time for animation.");
                }
                monsterAnimator.SetBool("grabbingPlayer", value: false);
                yield return new WaitWhile(() => _companyMonster!.isAttacking);
                if (player.deadBody != null)
                {
                    player.deadBody.attachedTo = null;
                    player.deadBody.attachedLimb = null;
                    player.deadBody.matchPositionExactly = false;
                    player.deadBody.gameObject.SetActive(value: false);
                }
                collided = false;
            }
        }
        #endregion
    }
}
