using HarmonyLib;
using LethalMon.Items;

namespace LethalMon.Patches;

public class RoundManagerPatch
{
    private static void ChangeIsScrapForBalls(bool isScrap, bool fullOnly)
    {
        GrabbableObject[] grabbableObjects = UnityEngine.Object.FindObjectsOfType<GrabbableObject>();
        foreach (GrabbableObject grabbableObject in grabbableObjects)
        {
            if (grabbableObject.GetType().IsSubclassOf(typeof(PokeballItem)) && (!fullOnly || ((PokeballItem)grabbableObject).enemyCaptured))
            {
                grabbableObject.itemProperties.isScrap = isScrap;
            }
        }
    }
    
    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.DespawnPropsAtEndOfRound))]
    [HarmonyPrefix]
    private static void DespawnPropsAtEndOfRoundPrefix(RoundManager __instance, bool despawnAllItems = false)
    {
        if (ModConfig.Instance.values.KeepBallsIfAllPlayersDead != "no")
        {
            ChangeIsScrapForBalls(false, ModConfig.Instance.values.KeepBallsIfAllPlayersDead == "fullOnly");
        }
    }
    
    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.DespawnPropsAtEndOfRound))]
    [HarmonyPostfix]
    private static void DespawnPropsAtEndOfRoundPostfix(RoundManager __instance, bool despawnAllItems = false)
    {
        if (ModConfig.Instance.values.KeepBallsIfAllPlayersDead != "no")
        {
            ChangeIsScrapForBalls(true, ModConfig.Instance.values.KeepBallsIfAllPlayersDead == "fullOnly");
        }
    }
}