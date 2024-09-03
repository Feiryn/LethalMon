using GameNetcodeStuff;
using HarmonyLib;
using LethalMon.Behaviours;
using System.Collections.Generic;
using UnityEngine;

namespace LethalMon.Patches
{
    internal class SandSpiderAIPatch
    {
        private static HashSet<SandSpiderAI> untamedSpiders = [];

        [HarmonyPatch(typeof(SandSpiderAI), nameof(SandSpiderAI.OnCollideWithPlayer))]
        [HarmonyPrefix]
        public static bool OnCollideWithPlayerPrefix(SandSpiderAI __instance) => !TryGetTamedBehaviour(__instance, out _);

        [HarmonyPatch(typeof(SandSpiderAI), nameof(SandSpiderAI.PlayerTripWebServerRpc))]
        [HarmonyPrefix]
        public static bool PlayerTripWebServerRpcPrefix(SandSpiderAI __instance) => !TryGetTamedBehaviour(__instance, out _);

        [HarmonyPatch(typeof(SandSpiderWebTrap), nameof(SandSpiderWebTrap.OnTriggerStay))]
        [HarmonyPrefix]
        public static bool OnTriggerStayPrefix(SandSpiderWebTrap __instance, Collider other)
        {
            if (GameNetworkManager.Instance == null || __instance.hinderingLocalPlayer || !TryGetTamedBehaviour(__instance.mainScript, out SpiderTamedBehaviour? spiderEnemyBehaviour))
                return true;

            if (Time.realtimeSinceStartup - spiderEnemyBehaviour!.timeOfLastWebJump < 1f) return false;

            if (other.TryGetComponent(out PlayerControllerB player) && player == Utils.CurrentPlayer)
            {
                if (player.isJumping || player.isFallingFromJump || player.isFallingNoJump)
                    spiderEnemyBehaviour.JumpOnWebLocalClient(__instance.trapID);
            }

            return false;
        }

        private static bool TryGetTamedBehaviour(SandSpiderAI spider, out SpiderTamedBehaviour? behaviour)
        {
            behaviour = null;
            if (untamedSpiders.Contains(spider)) return false;

            if (spider.TryGetComponent(out behaviour) && behaviour!.IsTamed)
                return true;

            untamedSpiders.Add(spider);
            return false;
        }
    }
}
