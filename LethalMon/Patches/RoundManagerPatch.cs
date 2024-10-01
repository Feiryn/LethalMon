using System.Linq;
using HarmonyLib;
using LethalMon.Items;
using Unity.Netcode;
using static LethalMon.ModConfig.ConfigValues;

namespace LethalMon.Patches;

public class RoundManagerPatch
{
    private static void ChangeIsScrapForBalls(bool isScrap, bool fullOnly)
    {
        GrabbableObject[] grabbableObjects = UnityEngine.Object.FindObjectsOfType<GrabbableObject>();
        foreach (GrabbableObject grabbableObject in grabbableObjects)
        {
            if (grabbableObject.GetType().IsSubclassOf(typeof(BallItem)) && (!fullOnly || ((BallItem)grabbableObject).enemyCaptured))
            {
                grabbableObject.itemProperties.isScrap = isScrap;
            }
        }
    }
    
    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.DespawnPropsAtEndOfRound))]
    [HarmonyPrefix]
    private static void DespawnPropsAtEndOfRoundPrefix(RoundManager __instance, bool despawnAllItems = false)
    {
        if (ModConfig.Instance.values.KeepBallsIfAllPlayersDead != KeepBalls.No)
        {
            ChangeIsScrapForBalls(false, ModConfig.Instance.values.KeepBallsIfAllPlayersDead == KeepBalls.FullOnly);
        }
    }
    
    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.DespawnPropsAtEndOfRound))]
    [HarmonyPostfix]
    private static void DespawnPropsAtEndOfRoundPostfix(RoundManager __instance, bool despawnAllItems = false)
    {
        if (ModConfig.Instance.values.KeepBallsIfAllPlayersDead != KeepBalls.No)
        {
            ChangeIsScrapForBalls(true, ModConfig.Instance.values.KeepBallsIfAllPlayersDead == KeepBalls.FullOnly);
        }
    }

    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.waitForScrapToSpawnToSync))]
    [HarmonyPostfix]
    private static void WaitForScrapToSpawnToSyncPostfix(RoundManager __instance, NetworkObjectReference[] spawnedScrap, int[] scrapValues)
    {
        if (!Utils.IsHost) return;
        
        foreach (NetworkObjectReference scrap in spawnedScrap)
        {
            if (scrap.TryGet(out NetworkObject networkObject))
            {
                BallItem ballItem = networkObject.gameObject.GetComponent<BallItem>();
                if (ballItem != null &&  UnityEngine.Random.Range(0f, 1f) <= ModConfig.Instance.values.FilledBallsPercentage)
                {
                    int totalDifficulty = Data.CatchableMonsters.Sum(cm => 10 - cm.Value.CatchDifficulty);
                    int randomValue = UnityEngine.Random.Range(0, totalDifficulty);
                    foreach (var catchableMonster in Data.CatchableMonsters)
                    {
                        randomValue -= 10 - catchableMonster.Value.CatchDifficulty;
                        if (randomValue <= 0)
                        {
                            ballItem.SetCaughtEnemyServerRpc(catchableMonster.Key, string.Empty);
                            LethalMon.Log("A random " + ballItem.itemProperties.itemName + " has been spawned with a " + catchableMonster.Key + " in it");
                            break;
                        }
                    }
                }
            }
        }
    }
}