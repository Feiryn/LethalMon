using HarmonyLib;
using LethalMon.Behaviours;

namespace LethalMon.Patches
{
    [HarmonyPatch]
    internal class GameNetworkManagerPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Start))]
        static void StartPostfix()
        {
            BlobTamedBehaviour.AddPhysicsSectionToPrefab();
        }
    }
}
