using HarmonyLib;
using LethalMon.Behaviours;

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
    }
}
