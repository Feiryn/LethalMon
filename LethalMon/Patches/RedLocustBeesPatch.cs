using System;
using System.Collections.Generic;
using HarmonyLib;
using LethalMon.Behaviours;

namespace LethalMon.Patches;

internal class RedLocustBeesPatch
{
    public static readonly Dictionary<int, DateTime> AngryUntil = new();
    
    [HarmonyPatch(typeof(RedLocustBees), nameof(RedLocustBees.IsHivePlacedAndInLOS))]
    [HarmonyPrefix]
    private static bool IsHivePlacedAndInLOSPrefix(RedLocustBees __instance, ref bool __result)
    {
        int id = __instance.GetInstanceID();
        if (AngryUntil.TryGetValue(id, out DateTime until))
        {
            if (until < DateTime.Now)
            {
                AngryUntil.Remove(id);
                return true; // Do not skip base
            }

            __result = false;
            LethalMon.Log("ANGRY BEES!!!!");
            return false; // Skip base
        }

        return true; // Do not skip base
    }

    [HarmonyPatch(typeof(RedLocustBees), nameof(RedLocustBees.SpawnHiveNearEnemy))]
    [HarmonyPrefix]
    public static bool SpawnHiveNearEnemyPrefix(RedLocustBees __instance)
    {
        TamedEnemyBehaviour tamedEnemyBehaviour = __instance.GetComponent<TamedEnemyBehaviour>();

        return !(tamedEnemyBehaviour != null && tamedEnemyBehaviour.IsOwnedByAPlayer());
    }
}