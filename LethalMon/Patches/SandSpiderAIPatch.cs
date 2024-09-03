using GameNetcodeStuff;
using HarmonyLib;
using LethalMon.Behaviours;
using UnityEngine;

namespace LethalMon.Patches
{
    internal class SandSpiderAIPatch
    {
        [HarmonyPatch(typeof(SandSpiderAI), nameof(SandSpiderAI.OnCollideWithPlayer))]
        [HarmonyPrefix]
        public static bool OnCollideWithPlayerPrefix(SandSpiderAI __instance)
        {
            TamedEnemyBehaviour tamedEnemyBehaviour = __instance.GetComponent<TamedEnemyBehaviour>();
            return tamedEnemyBehaviour == null || !tamedEnemyBehaviour.IsTamed;
        }

        [HarmonyPatch(typeof(SandSpiderWebTrap), nameof(SandSpiderWebTrap.OnTriggerStay))]
        [HarmonyPrefix]
        public static bool OnTriggerStayPrefix(SandSpiderWebTrap __instance, Collider other)
        {
            SpiderTamedBehaviour spiderEnemyBehaviour = __instance.mainScript.GetComponent<SpiderTamedBehaviour>();
            if(spiderEnemyBehaviour != null && spiderEnemyBehaviour.IsTamed)
            {
                if (Time.realtimeSinceStartup - spiderEnemyBehaviour.timeOfLastWebJump < 1f) return false;

                if (other.TryGetComponent(out PlayerControllerB player) && player == Utils.CurrentPlayer)
                    spiderEnemyBehaviour.JumpOnWebLocalClient(__instance.trapID);
                return false;
            }

            return true;
        }
    }
}
