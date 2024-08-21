using GameNetcodeStuff;
using HarmonyLib;
using LethalMon.Behaviours;
using UnityEngine;

namespace LethalMon.Patches;

public class CaveDwellerAIPatch
{
    [HarmonyPatch(typeof(CaveDwellerAI), nameof(CaveDwellerAI.DetectNoise))]
    [HarmonyPrefix]
    public static bool DetectNoisePrefix(CaveDwellerAI __instance, Vector3 noisePosition, float noiseLoudness, int timesPlayedInOneSpot = 0, int noiseID = 0)
    {
        if (__instance.TryGetComponent(out ManeaterTamedBehaviour tamedBehaviour) && tamedBehaviour.IsTamed)
        {
            tamedBehaviour.DetectNoise(noisePosition, noiseLoudness, timesPlayedInOneSpot, noiseID);
            return false;
        }

        return true;
    }

    static int previousBehaviourStateIndex = 0;
    [HarmonyPatch(typeof(CaveDwellerAI), nameof(CaveDwellerAI.DropBabyLocalClient))]
    [HarmonyPrefix]
    public static void DropBabyLocalClientPrefix(CaveDwellerAI __instance)
    {
        if (__instance.TryGetComponent(out ManeaterTamedBehaviour tamedBehaviour) && tamedBehaviour.IsTamed)
        {
            previousBehaviourStateIndex = __instance.currentBehaviourStateIndex;
            __instance.currentBehaviourStateIndex = 0;
        }
    }

    [HarmonyPatch(typeof(CaveDwellerAI), nameof(CaveDwellerAI.DropBabyLocalClient))]
    [HarmonyPostfix]
    public static void DropBabyLocalClientPostfix(CaveDwellerAI __instance)
    {
        if (__instance.TryGetComponent(out ManeaterTamedBehaviour tamedBehaviour) && tamedBehaviour.IsTamed)
        {
            __instance.currentBehaviourStateIndex = previousBehaviourStateIndex;
            tamedBehaviour.StartCoroutine(tamedBehaviour.EndSpecialAnimationAfterLanding());
        }
    }

    [HarmonyPatch(typeof(CaveDwellerAI), nameof(CaveDwellerAI.PickUpBabyLocalClient))]
    [HarmonyPostfix]
    public static void PickUpBabyLocalClientPostfix(CaveDwellerAI __instance)
    {
        if (__instance.TryGetComponent(out ManeaterTamedBehaviour tamedBehaviour) && tamedBehaviour.IsTamed)
            __instance.currentOwnershipOnThisClient = (int)tamedBehaviour.ownerPlayer!.playerClientId;
    }

    [HarmonyPatch(typeof(CaveDwellerAI), nameof(CaveDwellerAI.OnCollideWithPlayer))]
    [HarmonyPostfix]
    public static void OnCollideWithPlayerPostfix(CaveDwellerAI __instance, Collider other)
    {
        if (__instance.TryGetComponent(out ManeaterTamedBehaviour tamedBehaviour) && tamedBehaviour.IsTamed && tamedBehaviour.Target != null)
        {
            if (tamedBehaviour.IsAttacking && other.gameObject.TryGetComponent(out PlayerControllerB player) && player.gameObject == tamedBehaviour.Target)
                tamedBehaviour.KillTarget();
        }
    }
}