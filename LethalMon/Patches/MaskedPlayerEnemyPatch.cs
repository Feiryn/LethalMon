using GameNetcodeStuff;
using HarmonyLib;
using LethalMon.Behaviours;
using System.Collections.Generic;
using UnityEngine;

namespace LethalMon.Patches
{
    internal class MaskedPlayerEnemyPatch
    {
        internal static Dictionary<int, int> lastGhostColliderIDs = new Dictionary<int, int>();
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.OnCollideWithPlayer))]
        public static bool OnCollideWithPlayerPrefix(MaskedPlayerEnemy __instance, Collider other)
        {
            if (lastGhostColliderIDs.GetValueOrDefault(__instance.GetInstanceID(), -1) == other.GetInstanceID()) return false; // Hit player as ghost. Don't re-run every frame

            PlayerControllerB player = other.gameObject.GetComponent<PlayerControllerB>();
            if(player == null) return false;

            lastGhostColliderIDs[__instance.GetInstanceID()] = other.GetInstanceID();

            if (__instance.TryGetComponent(out MaskedTamedBehaviour tamedBehaviour))
            {
                LethalMon.Log("Collided -> Collider: " + other.name + " / Target: " + tamedBehaviour.targetPlayer?.name + " / Behaviour:" + (tamedBehaviour.CurrentCustomBehaviour == null ? "" : ((MaskedTamedBehaviour.CustomBehaviour)tamedBehaviour.CurrentCustomBehaviour!).ToString()));
                if (tamedBehaviour.targetPlayer != null && tamedBehaviour.CurrentCustomBehaviour.GetValueOrDefault(-1) == (int)MaskedTamedBehaviour.CustomBehaviour.Ghostified)
                {
                    LethalMon.Log("Ghost hitting player " + player.playerClientId);
                    tamedBehaviour.GhostHitPlayerServerRpc(player.playerClientId);
                    return false;
                }
            }
            return true;
        }
    }
}
