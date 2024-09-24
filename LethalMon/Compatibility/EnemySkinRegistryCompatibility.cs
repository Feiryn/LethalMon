using AntlerShed.SkinRegistry;
using AntlerShed.SkinRegistry.Events;
using HarmonyLib;
using LethalMon.Behaviours;

namespace LethalMon.Compatibility;

public class EnemySkinRegistryCompatibility() : ModCompatibility("antlershed.lethalcompany.enemyskinregistry")
{
    public static EnemySkinRegistryCompatibility Instance { get; } = new();

    public static string GetEnemySkinId(EnemyAI enemy)
    {
        return Instance.Enabled ? EnemySkinRegistry.GetSkinId(enemy.gameObject) ?? string.Empty : string.Empty;
    }
    
    public static bool DoesSkinExist(string skinId)
    {
        return Instance.Enabled; // todo
    }
    
    public static string GetSkinName(string skinId)
    {
        return Instance.Enabled ? string.Empty : string.Empty; // todo
    }
    
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.Start))]
    [HarmonyPostfix]
    [HarmonyAfter(["antlershed.lethalcompany.enemyskinregistry"])]
    private static void EnemyStartPostFix(EnemyAI __instance)
    {
        if (__instance.TryGetComponent(out TamedEnemyBehaviour tamedEnemyBehaviour) && tamedEnemyBehaviour.IsOwnedByAPlayer() && tamedEnemyBehaviour.ForceEnemySkinRegistryId != string.Empty)
        {
            LethalMon.Log("Set enemy skin to " + tamedEnemyBehaviour.ForceEnemySkinRegistryId);
            EnemySkinRegistry.ReassignSkin(tamedEnemyBehaviour.Enemy.gameObject,
                tamedEnemyBehaviour.ForceEnemySkinRegistryId);
        }
    }
}