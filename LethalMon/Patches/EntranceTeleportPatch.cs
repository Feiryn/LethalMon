using HarmonyLib;
using UnityEngine;

namespace LethalMon.Patches
{
    [HarmonyPatch]
    internal class EntranceTeleportPatch
    {
        internal static Vector3? lastEntranceTeleportFrom = null;
        internal static Vector3? lastEntranceTeleportTo = null;

        internal static bool HasTeleported => lastEntranceTeleportFrom != null || lastEntranceTeleportTo != null;

        [HarmonyPatch(typeof(EntranceTeleport), nameof(EntranceTeleport.TeleportPlayer))]
        [HarmonyPrefix]
        public static void TeleportPlayerPrefix() => lastEntranceTeleportFrom = Utils.CurrentPlayer.transform.position;

        [HarmonyPatch(typeof(EntranceTeleport), nameof(EntranceTeleport.TeleportPlayer))]
        [HarmonyPostfix]
        public static void TeleportPlayerPostfix() => lastEntranceTeleportTo = Utils.CurrentPlayer.transform.position;
    }
}
