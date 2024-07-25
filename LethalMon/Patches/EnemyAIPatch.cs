using System;
using System.Linq;
using HarmonyLib;
using LethalMon.Behaviours;
using Object = UnityEngine.Object;

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
    
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.SwitchToBehaviourStateOnLocalClient))]
    [HarmonyPostfix]
    private static void SwitchToBehaviourStateOnLocalClientPostfix(EnemyAI __instance, int stateIndex)
    {
        TamedEnemyBehaviour tamedEnemyBehaviour = __instance.GetComponent<TamedEnemyBehaviour>();
        if (tamedEnemyBehaviour != null)
        {
            if (stateIndex > tamedEnemyBehaviour.LastDefaultBehaviourIndex)
            {
                if (stateIndex <= tamedEnemyBehaviour.LastDefaultBehaviourIndex + TamedEnemyBehaviour.TamedBehaviourCount)
                {
                    tamedEnemyBehaviour.InitTamingBehaviour((TamedEnemyBehaviour.TamingBehaviour) stateIndex - tamedEnemyBehaviour.LastDefaultBehaviourIndex);
                }
                else
                {
                    tamedEnemyBehaviour.InitCustomBehaviour(stateIndex - TamedEnemyBehaviour.TamedBehaviourCount - tamedEnemyBehaviour.LastDefaultBehaviourIndex);
                }
            }
            
            HUDManagerPatch.UpdateTamedMonsterAction(tamedEnemyBehaviour.GetCurrentStateDescription());
        }
        else
        {
            LethalMon.Log("No TamedEnemyBehaviour component found after EnemyAI SwitchToBehaviourStateOnLocalClient", LethalMon.LogType.Warning);
        }
    }
}