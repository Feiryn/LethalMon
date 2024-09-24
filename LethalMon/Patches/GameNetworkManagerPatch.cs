using HarmonyLib;
using LethalMon.Behaviours;

namespace LethalMon.Patches;

public class GameNetworkManagerPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Start))]
    static void StartPostfix()
    {
        BlobTamedBehaviour.AddPhysicsSectionToPrefab();
    }
}