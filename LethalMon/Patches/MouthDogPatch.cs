using HarmonyLib;
using LethalMon.Behaviours;
using UnityEngine;

namespace LethalMon.Patches;

internal class MouthDogPatch
{
    [HarmonyPatch(typeof(MouthDogAI), nameof(MouthDogAI.ReactToOtherDogHowl))]
    [HarmonyPrefix]
    private static bool ReactToOtherDogHowlPreFix(MouthDogAI __instance/*, Vector3 howlPosition*/)
    {
        // Ignore other dogs howl if the dog is tamed
        return !(__instance.GetComponentInParent<MouthDogTamedBehaviour>()?.ownerPlayer != null);
    }
    
    [HarmonyPatch(typeof(MouthDogAI), nameof(MouthDogAI.KillPlayer))]
    [HarmonyPostfix]
    private static void KillPlayerPostfix(MouthDogAI __instance)
    {
        MouthDogTamedBehaviour? tamedBehaviour = __instance.GetComponentInParent<MouthDogTamedBehaviour>();
        if (tamedBehaviour != null && tamedBehaviour.IsTamed)
        {
            tamedBehaviour.SwitchToTamingBehaviour(TamedEnemyBehaviour.TamingBehaviour.TamedFollowing);
        }
    }
}