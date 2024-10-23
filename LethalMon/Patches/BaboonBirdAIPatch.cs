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
                return false;
            }

            return true;
        }
    }
}
