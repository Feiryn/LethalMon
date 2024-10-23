using System.Diagnostics;
using GameNetcodeStuff;
using HarmonyLib;
using LethalMon.Behaviours;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LethalMon.Patches;

internal class EnemyAIPatch
{
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.OnDestroy))]
    [HarmonyPrefix]
    private static void OnDestroyPreFix(EnemyAI __instance)
    {
        TamedEnemyBehaviour? tamedEnemyBehaviour = Cache.GetTamedEnemyBehaviour(__instance);
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
    [HarmonyPrefix]
    private static void SwitchToBehaviourStateOnLocalClientPrefix(EnemyAI __instance)
    {
        TamedEnemyBehaviour? tamedEnemyBehaviour = Cache.GetTamedEnemyBehaviour(__instance);
        if (tamedEnemyBehaviour != null)
        {
            if (__instance.currentBehaviourStateIndex > tamedEnemyBehaviour.LastDefaultBehaviourIndex)
            {
                if (__instance.currentBehaviourStateIndex <= tamedEnemyBehaviour.LastDefaultBehaviourIndex + TamedEnemyBehaviour.TamedBehaviourCount)
                {
                    tamedEnemyBehaviour.LeaveTamingBehaviour((TamedEnemyBehaviour.TamingBehaviour)__instance.currentBehaviourStateIndex - tamedEnemyBehaviour.LastDefaultBehaviourIndex);
                }
                else
                {
                    tamedEnemyBehaviour.LeaveCustomBehaviour(__instance.currentBehaviourStateIndex - TamedEnemyBehaviour.TamedBehaviourCount - tamedEnemyBehaviour.LastDefaultBehaviourIndex);
                }
            }
        }
    }

    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.SwitchToBehaviourStateOnLocalClient))]
    [HarmonyPostfix]
    private static void SwitchToBehaviourStateOnLocalClientPostfix(EnemyAI __instance, int stateIndex)
    {
        TamedEnemyBehaviour? tamedEnemyBehaviour = Cache.GetTamedEnemyBehaviour(__instance);
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

            if (tamedEnemyBehaviour.ownerPlayer == Utils.CurrentPlayer)
            {
                HUDManagerPatch.UpdateTamedMonsterAction(tamedEnemyBehaviour.GetCurrentStateDescription());
            }
        }
        else
        {
            LethalMon.Log("No TamedEnemyBehaviour component found after EnemyAI SwitchToBehaviourStateOnLocalClient", LethalMon.LogType.Warning);
        }
    }

    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.HitEnemy))]
    [HarmonyPrefix]
    private static bool HitEnemyPrefix(EnemyAI __instance/*, int force = -1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1*/)
    {
        TamedEnemyBehaviour? tamedEnemyBehaviour = Cache.GetTamedEnemyBehaviour(__instance);
        return tamedEnemyBehaviour == null || !tamedEnemyBehaviour.IsTamed;
    }
    
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.KillEnemy))]
    [HarmonyPrefix]
    private static bool KillEnemyPrefix(EnemyAI __instance/*, bool destroy = false*/)
    {
        TamedEnemyBehaviour? tamedEnemyBehaviour = Cache.GetTamedEnemyBehaviour(__instance);
        return tamedEnemyBehaviour == null || !tamedEnemyBehaviour.IsTamed;
    }
    
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.KillEnemyClientRpc))]
    [HarmonyPrefix]
    private static bool KillEnemyClientRpcPrefix(EnemyAI __instance/*, bool destroy*/)
    {
        TamedEnemyBehaviour? tamedEnemyBehaviour = Cache.GetTamedEnemyBehaviour(__instance);
        return tamedEnemyBehaviour == null || !tamedEnemyBehaviour.IsTamed;
    }
    
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.KillEnemyServerRpc))]
    [HarmonyPrefix]
    private static bool KillEnemyServerRpcPrefix(EnemyAI __instance/*, bool destroy*/)
    {
        TamedEnemyBehaviour? tamedEnemyBehaviour = Cache.GetTamedEnemyBehaviour(__instance);
        return tamedEnemyBehaviour == null || !tamedEnemyBehaviour.IsTamed;
    }
    
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.KillEnemyOnOwnerClient))]
    [HarmonyPrefix]
    private static bool KillEnemyOnOwnerClientPrefix(EnemyAI __instance/*, bool overrideDestroy = false*/)
    {
        TamedEnemyBehaviour? tamedEnemyBehaviour = Cache.GetTamedEnemyBehaviour(__instance);
        return tamedEnemyBehaviour == null || !tamedEnemyBehaviour.IsTamed;
    }
    
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.OnCollideWithEnemy))]
    [HarmonyPostfix]
    private static void OnCollideWithEnemyPostfix(EnemyAI __instance, Collider other, EnemyAI collidedEnemy = null)
    {
        TamedEnemyBehaviour? tamedEnemyBehaviour = Cache.GetTamedEnemyBehaviour(__instance);
        if (tamedEnemyBehaviour != null && tamedEnemyBehaviour.IsTamed)
        {
            tamedEnemyBehaviour.OnCollideWithEnemy(other, collidedEnemy);
        }
    }
    
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.OnCollideWithPlayer))]
    [HarmonyPrefix]
    private static bool OnCollideWithPlayerPrefix(EnemyAI __instance, Collider other)
    {
        PlayerControllerB? player = Cache.GetPlayerFromCollider(other);
        if (player != null && Cache.GetPlayerPet(player, out var tamedEnemyBehaviour) && tamedEnemyBehaviour!.IsTamed && tamedEnemyBehaviour is MouthDogTamedBehaviour mouthDogTamedBehaviour)
        {
            return mouthDogTamedBehaviour.enemyBeingDamaged == null;
        }

        return true;
    }
    
    #if DEBUG
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.SwitchToBehaviourState))]
    [HarmonyPrefix]
    private static void SwitchToBehaviourStatePrefix(EnemyAI __instance, int stateIndex)
    {
        TamedEnemyBehaviour? tamedEnemyBehaviour = Cache.GetTamedEnemyBehaviour(__instance);

        if (tamedEnemyBehaviour != null && tamedEnemyBehaviour.ownerPlayer != null)
        {
            LethalMon.Log(__instance.enemyType.name + " switches from behaviour state (" +
                          __instance.currentBehaviourStateIndex + ") to behaviour state (" + stateIndex +
                          "), called by:\n" + new StackTrace());
        }
    }
    #endif
}