using GameNetcodeStuff;
using HarmonyLib;
using LethalMon.Behaviours;
using UnityEngine;

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
                PlayerControllerB playerControllerB = __instance.MeetsStandardPlayerCollisionConditions(other, __instance.inSpecialAnimation || __instance.doingKillAnimation);
                if(playerControllerB != null)
                {
                    __instance.timeSinceHitting = 0f;
                    tinyHawkBehaviour.OnCollideWithPlayer(playerControllerB);
                    return false;
                }
            }

            return true;
        }
    }
}
