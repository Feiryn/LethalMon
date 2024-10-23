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
        public static bool OnCollideWithPlayerPrefix(SandSpiderAI __instance) => !(Cache.GetTamedEnemyBehaviour(__instance)?.IsTamed ?? false);

        [HarmonyPatch(typeof(SandSpiderAI), nameof(SandSpiderAI.PlayerTripWebServerRpc))]
        [HarmonyPrefix]
        public static bool PlayerTripWebServerRpcPrefix(SandSpiderAI __instance) => !(Cache.GetTamedEnemyBehaviour(__instance)?.IsTamed ?? false);

        [HarmonyPatch(typeof(SandSpiderWebTrap), nameof(SandSpiderWebTrap.OnTriggerStay))]
        [HarmonyPrefix]
        public static bool OnTriggerStayPrefix(SandSpiderWebTrap __instance, Collider other)
        {
            TamedEnemyBehaviour? tamedEnemyBehaviour = Cache.GetTamedEnemyBehaviour(__instance.mainScript);
            if (GameNetworkManager.Instance == null || __instance.hinderingLocalPlayer || !(tamedEnemyBehaviour?.IsTamed ?? false))
                return true;

            SpiderTamedBehaviour? spiderEnemyBehaviour = tamedEnemyBehaviour as SpiderTamedBehaviour;
            if (Time.realtimeSinceStartup - spiderEnemyBehaviour!.timeOfLastWebJump < 1f) return false;

            if (other.TryGetComponent(out PlayerControllerB player) && player == Utils.CurrentPlayer)
            {
                if (player.isJumping || player.isFallingFromJump || player.isFallingNoJump)
                    spiderEnemyBehaviour.JumpOnWebLocalClient(__instance.trapID);
            }

            return false;
        }
        
        [HarmonyPatch(typeof(SandSpiderAI), nameof(SandSpiderAI.TriggerChaseWithPlayer))]
        [HarmonyPrefix]
        public static bool TriggerChaseWithPlayerPrefix(SandSpiderAI __instance/*, PlayerControllerB playerScript*/)
        {
            TamedEnemyBehaviour? tamedEnemyBehaviour = Cache.GetTamedEnemyBehaviour(__instance);
            return tamedEnemyBehaviour == null || !tamedEnemyBehaviour.IsTamed;
        }
    }
}
