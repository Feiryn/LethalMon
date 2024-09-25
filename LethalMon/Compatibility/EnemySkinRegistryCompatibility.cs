using System;
using AntlerShed.SkinRegistry;
using BepInEx;
using HarmonyLib;
using LethalMon.Behaviours;

namespace LethalMon.Compatibility;

public class EnemySkinRegistryCompatibility() : ModCompatibility("antlershed.lethalcompany.enemyskinregistry")
{
    public static EnemySkinRegistryCompatibility Instance { get; } = new();

    private BaseUnityPlugin? _pluginInstance;
    
    internal BaseUnityPlugin? PluginInstance
    {
        get
        {
            if (_pluginInstance == null)
            {
                if (Instance.Enabled)
                {
                    _pluginInstance = BepInEx.Bootstrap.Chainloader.PluginInfos["antlershed.lethalcompany.enemyskinregistry"].Instance as EnemySkinRegistry;
                }
                else
                {
                    return null;
                }
            }
            
            return _pluginInstance;
        }
    }

    public static string GetEnemySkinId(EnemyAI enemy)
    {
        return Instance.Enabled ? EnemySkinRegistry.GetSkinId(enemy.gameObject) ?? string.Empty : string.Empty;
    }
    
    public static string GetSkinName(string skinId)
    {
        if (!Instance.Enabled)
            return string.Empty;

        try
        {
            var skinData = ((EnemySkinRegistry) Instance.PluginInstance!).GetSkinData(skinId);
            return skinData != null ? skinData.Label : string.Empty;
        }
        catch (Exception e)
        {
            LethalMon.Log("Error getting skin name for " + skinId + ": " + e, LethalMon.LogType.Warning);
            return string.Empty;
        }
    }
    
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.Start))]
    [HarmonyPostfix]
    [HarmonyAfter(["antlershed.lethalcompany.enemyskinregistry"])]
    private static void EnemyStartPostFix(EnemyAI __instance)
    {
        if (__instance.TryGetComponent(out TamedEnemyBehaviour tamedEnemyBehaviour) && tamedEnemyBehaviour.IsOwnedByAPlayer() && !string.IsNullOrEmpty(tamedEnemyBehaviour.ForceEnemySkinRegistryId) && !string.IsNullOrEmpty(GetSkinName(tamedEnemyBehaviour.ForceEnemySkinRegistryId)))
        {
            LethalMon.Log("Set enemy skin to " + tamedEnemyBehaviour.ForceEnemySkinRegistryId);
            EnemySkinRegistry.ReassignSkin(tamedEnemyBehaviour.Enemy.gameObject, tamedEnemyBehaviour.ForceEnemySkinRegistryId);
        }
    }
}