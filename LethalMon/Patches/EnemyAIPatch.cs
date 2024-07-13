using HarmonyLib;
using LethalMon.Behaviours;
using UnityEngine;

namespace LethalMon.Patches;

public class EnemyAIPatch
{
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.OnDestroy))]
    [HarmonyPrefix]
    private static void OnDestroyPreFix(EnemyAI __instance)
    {
        TamedEnemyBehaviour tamedEnemyBehaviour = __instance.GetComponent<TamedEnemyBehaviour>();
        if (tamedEnemyBehaviour != null)
        {
            LethalMon.Log("Destroying TamedEnemyBehaviour component");
            Object.Destroy(tamedEnemyBehaviour);
        }
        else
        {
            LethalMon.Log("No TamedEnemyBehaviour component found before EnemyAI OnDestroy", LethalMon.LogType.Warning);
        }
    }
}