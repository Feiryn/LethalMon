using GameNetcodeStuff;
using HarmonyLib;
using LethalMon.Behaviours;
using System.Collections.Generic;
using UnityEngine;

namespace LethalMon.Patches
{
    internal class MaskedPlayerEnemyPatch
    {
        internal static Dictionary<int, int> lastColliderIDs = new Dictionary<int, int>();
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.OnCollideWithPlayer))]
        public static bool OnCollideWithPlayerPrefix(MaskedPlayerEnemy __instance, Collider other)
        {
            if (lastColliderIDs.GetValueOrDefault(__instance.GetInstanceID(), -1) == other.GetInstanceID()) return true; // Don't re-run this patch every frame
            lastColliderIDs[__instance.GetInstanceID()] = other.GetInstanceID();

            PlayerControllerB player = other.gameObject.GetComponent<PlayerControllerB>();
            if (player == null) return true; // For mod compatibility purposes return true

            if (__instance.TryGetComponent(out MaskedTamedBehaviour tamedBehaviour))
            {
                LethalMon.Log("Collided -> Collider: " + other.name + " / Target: " + tamedBehaviour.targetPlayer?.name + " / Behaviour:" + (tamedBehaviour.CurrentCustomBehaviour == null ? "" : ((MaskedTamedBehaviour.CustomBehaviour)tamedBehaviour.CurrentCustomBehaviour!).ToString()));
                if (tamedBehaviour.targetPlayer != null)
                {
                    if (tamedBehaviour.CurrentCustomBehaviour.GetValueOrDefault(-1) == (int)MaskedTamedBehaviour.CustomBehaviour.Ghostified)
                    {
                        LethalMon.Log("Ghost hitting player " + player.playerClientId);
                        tamedBehaviour.GhostHitPlayerServerRpc(player.playerClientId);
                        return false;
                    }
                    else if(tamedBehaviour.escapeFromBallEventRunning && player == tamedBehaviour.targetPlayer)
                    {
                        LethalMon.Log("Player " + player.playerClientId + " ran into the masked that does the escape event.");
                        tamedBehaviour.CleanUp();
                        tamedBehaviour.Masked.maskEyesGlowLight.intensity = 0.6f;

                        if (tamedBehaviour.Masked.agent != null)
                            tamedBehaviour.Masked.agent.enabled = true;
                        tamedBehaviour.Masked.enabled = true;

                        tamedBehaviour.EscapeFromBallEventEndedServerRpc();

                        // Assure player can die from masked
                        if (Time.realtimeSinceStartup - tamedBehaviour.Masked.timeAtLastUsingEntrance < 1.75f)
                            tamedBehaviour.Masked.timeAtLastUsingEntrance = Time.realtimeSinceStartup - 2f;
                    }
                }
            }
            return true;
        }
    }
}
