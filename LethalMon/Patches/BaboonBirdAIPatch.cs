using GameNetcodeStuff;
using HarmonyLib;
using LethalMon.Behaviours;
using UnityEngine;
using System.Linq;

namespace LethalMon.Patches
{
    internal class BaboonBirdAIPatch
    {
        [HarmonyPatch(typeof(BaboonBirdAI), nameof(BaboonBirdAI.OnCollideWithPlayer))]
        [HarmonyPrefix]
        public static bool OnCollideWithPlayerPrefix(BaboonBirdAI __instance, Collider other)
        {
            if (__instance.timeSinceHitting < 0.5f) return false;

            if (__instance.TryGetComponent(out BaboonHawkTamedBehaviour.TinyHawkBehaviour tinyHawkBehaviour))
            {
                /*if (tinyHawkBehaviour.motherBird != null && tinyHawkBehaviour.motherBird.TryGetComponent(out BaboonHawkTamedBehaviour tamedBehaviour) && other.gameObject.TryGetComponent(out PlayerControllerB player))
                {
                    __instance.timeSinceHitting = 0f;
                    tamedBehaviour.TinyHawkGotHitServerRpc(__instance.NetworkObject, player.NetworkObject);
                }*/

                return false;
            }

            return true;
        }
    }
}
