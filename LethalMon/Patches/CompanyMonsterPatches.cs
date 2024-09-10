using HarmonyLib;
using LethalMon.Behaviours;
using LethalMon.CatchableEnemy;
using LethalMon.Items;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LethalMon.Patches
{
    internal class CompanyMonsterPatches
    {
        internal static readonly string TentaclePrefabPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), CompanyMonsterAI.TentaclePrefabFileName);

        [HarmonyPatch(typeof(DepositItemsDesk), nameof(DepositItemsDesk.Start))]
        [HarmonyPostfix]
        private static void DepositItemsDeskStartPostfix(DepositItemsDesk __instance)
        {
            //CompanyMonsterAI.ExtractTentaclesFromItemDesk(__instance);

            var companyMonsterCaught = Object.FindObjectsOfType<PokeballItem>().Where(ball => ball.enemyCaptured && ball.catchableEnemy?.GetType() == typeof(CatchableCompanyMonster)).Any();
            LethalMon.Log("Caught company monster: " + companyMonsterCaught);
            if (companyMonsterCaught)
            {
                __instance.gameObject.SetActive(false);
                return;
            }
        }

        static float loadSceneStartTime;

        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Start))]
        [HarmonyPostfix]
        private static void LoadOrExtractTentaclePrefab()
        {
            CompanyMonsterAI.tentaclePrefab = Resources.Load<GameObject>(TentaclePrefabPath);
            if (CompanyMonsterAI.tentaclePrefab != null) return;

            LethalMon.Log("Loading CompanyBuilding scene..");
            loadSceneStartTime = Time.realtimeSinceStartup;
            var loadSceneOpteration = SceneManager.LoadSceneAsync("CompanyBuilding", LoadSceneMode.Additive);
            loadSceneOpteration.allowSceneActivation = false;
            loadSceneOpteration.completed += ExtractTentacles;
        }

        private static void ExtractTentacles(AsyncOperation operation)
        {
            LethalMon.Log($"CompanyBuilding scene loaded in {Time.realtimeSinceStartup - loadSceneStartTime} seconds.");

            var companyScene = SceneManager.GetSceneByName("CompanyBuilding");
            if (companyScene == null || !companyScene.IsValid())
            {
                LethalMon.Log("ExtractTentacles: CompanyBuilding scene not found or can't be loaded.");
                return;
            }

            /*var depositCounter = companyScene.GetRootGameObjects().Where(o => o.name == "DepositCounter").First();
            var depositItemsDesk = depositCounter.GetComponentInChildren<DepositItemsDesk>();
            if (depositItemsDesk != null)
            {
                LethalMon.Log("FOUND ITEMS DESK");
                var monsterAnim = depositItemsDesk.monsterAnimations[0];
                CompanyMonsterAI.monsterAnimationPrefab = new MonsterAnimation()
                {
                    monsterAnimator = Object.Instantiate(monsterAnim.monsterAnimator),
                    monsterAnimatorGrabPoint = Object.Instantiate(monsterAnim.monsterAnimatorGrabPoint),
                    monsterAnimatorGrabTarget = Object.Instantiate(monsterAnim.monsterAnimatorGrabTarget),
                };
                Object.DontDestroyOnLoad(CompanyMonsterAI.monsterAnimationPrefab.monsterAnimator);

                // Add custom trigger
                var triggers = CompanyMonsterAI.monsterAnimationPrefab.monsterAnimator.GetComponentsInChildren<CompanyMonsterCollisionDetect>();
                for (int i = triggers.Length - 1; i >= 0; --i)
                {
                    LethalMon.Log("Replacing trigger");
                    triggers[i].gameObject.AddComponent<CompanyMonsterAI.CollisionDetect>();
                    Object.Destroy(triggers[i]);
                }
            }*/
            
            var monsterAnims = companyScene.GetRootGameObjects().Where(o => o.name == "CompanyMonstersAnims");
            if (!monsterAnims.Any())
            {
                LethalMon.Log("ExtractTentacles: CompanyBuilding scene had no CompanyMonstersAnims.");
                return;
            }

            var grossTentacle = monsterAnims.First().transform.Find("TentacleAnimContainer")?.Find("GrossTentacle")?.gameObject;
            if (grossTentacle == null)
            {
                LethalMon.Log("ExtractTentacles: CompanyMonstersAnims had no GrossTentacle.");
                return;
            }

            CompanyMonsterAI.tentaclePrefab = Object.Instantiate(grossTentacle);

            // Add custom trigger
            var triggers = CompanyMonsterAI.tentaclePrefab.GetComponentsInChildren<CompanyMonsterCollisionDetect>();
            for(int i = triggers.Length - 1; i >= 0; --i)
            {
                LethalMon.Log("Replacing trigger");
                triggers[i].gameObject.AddComponent<CompanyMonsterAI.CollisionDetect>();
                Object.Destroy(triggers[i]);
            }

            CompanyMonsterAI.tentaclePrefab.transform.SetParent(null, false);
            Object.DontDestroyOnLoad(CompanyMonsterAI.tentaclePrefab);
            CompanyMonsterAI.tentaclePrefab.SetActive(false);
            CompanyMonsterAI.tentaclePrefab.name = grossTentacle.name;

            LethalMon.Log("CompanyMonster prefab name: " + CompanyMonsterAI.tentaclePrefab.name);

            //LethalMon.Log("CompanyMonster prefab path: " + TentaclePrefabPath);
            //PrefabUtility.SaveAsPrefabAsset(CompanyMonsterAI.tentaclePrefab, TentaclePrefabPath); // not working yet
            LethalMon.Log("Extracted tentacles from company building scene.");

            SceneManager.UnloadSceneAsync(companyScene);
        }

        [HarmonyPatch(typeof(CompanyMonsterCollisionDetect), nameof(CompanyMonsterCollisionDetect.OnTriggerEnter))]
        [HarmonyPostfix]
        private static void OnTriggerEnterPostfix(CompanyMonsterCollisionDetect __instance, Collider other)
        {
            LethalMon.Log("Collided with: " + other.tag);
            if (!other.gameObject.TryGetComponent(out PokeballItem ball)) return; // Todo: performance

            LethalMon.Log("Collided with pokeball.");
            var companyMonsterAI = Object.Instantiate(CompanyMonsterAI.companyMonsterPrefab)?.GetComponent<CompanyMonsterAI>();
            if(companyMonsterAI == null)
            {
                LethalMon.Log("Unable to create CompanyMonsterAI.");
                return;
            }

            companyMonsterAI.GetComponent<NetworkObject>()?.Spawn();

            ball.CollidedWithEnemy(companyMonsterAI);

            StopDeskAnimation(__instance.monsterAnimationID);
        }

        static void StopDeskAnimation(int monsterAnimationID)
        {
            var desk = Object.FindObjectOfType<DepositItemsDesk>();
            if (desk == null || !desk.attacking) return;

            desk.StopAllCoroutines();
            desk.attacking = false;
            desk.deskAudio.Stop();
            desk.wallAudio.Stop();

            var players = Utils.AllPlayers;
            if (players != null && desk.monsterAnimations.Length > monsterAnimationID)
            {
                foreach (var playerDying in players)
                {
                    if (!playerDying.isPlayerDead || playerDying.deadBody.attachedTo == null) continue;

                    if (desk.monsterAnimations[monsterAnimationID].monsterAnimatorGrabPoint == playerDying.deadBody.attachedTo)
                    {
                        playerDying.deadBody.attachedTo = null;
                        playerDying.deadBody.attachedLimb = null;
                        playerDying.deadBody.matchPositionExactly = false;
                    }
                }
            }

            desk.FinishKillAnimation();
            foreach (var animator in desk.monsterAnimations)
                animator.monsterAnimator.StopPlayback();
        }
    }
}
