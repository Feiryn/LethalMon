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
}