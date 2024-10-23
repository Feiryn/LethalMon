using HarmonyLib;
using LethalMon.Behaviours;

namespace LethalMon.Patches;

internal class BushWolfEnemyPatch
{
    [HarmonyPatch(typeof(BushWolfEnemy), nameof(BushWolfEnemy.Start))]
    [HarmonyPostfix]
    public static void OnStartPostfix(BushWolfEnemy __instance)
    {
        KidnapperFoxTamedBehaviour? behaviour = __instance.GetComponent<KidnapperFoxTamedBehaviour>();
        if (behaviour != null && behaviour.IsTamed)
        {
            __instance.inSpecialAnimation = false;
            __instance.EnableEnemyMesh(enable: true);
            __instance.agent.enabled = true;
            __instance.agent.speed = 5f;
        }
    }
}