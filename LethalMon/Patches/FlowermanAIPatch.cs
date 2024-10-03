using GameNetcodeStuff;
using HarmonyLib;
using LethalMon.Behaviours;
using UnityEngine;

namespace LethalMon.Patches;

internal class FlowermanAIPatch
{
    [HarmonyPatch(typeof(FlowermanAI), nameof(FlowermanAI.HitEnemy))]
    [HarmonyPrefix]
    private static bool HitEnemyPrefix(FlowermanAI __instance/*, int force = -1, PlayerControllerB playerWhoHit = null, bool playHitSFX = false, int hitID = -1*/)
    {
        TamedEnemyBehaviour tamedEnemyBehaviour = __instance.GetComponent<TamedEnemyBehaviour>();
        return tamedEnemyBehaviour == null || tamedEnemyBehaviour.ownerPlayer == null;
    }

    [HarmonyPatch(typeof(FlowermanAI), nameof(FlowermanAI.OnCollideWithPlayer))]
    [HarmonyPrefix]
    private static bool OnCollideWithPlayerPrefix(FlowermanAI __instance/*, Collider other*/)
    {
        TamedEnemyBehaviour tamedEnemyBehaviour = __instance.GetComponent<TamedEnemyBehaviour>();
        return tamedEnemyBehaviour == null || tamedEnemyBehaviour.ownerPlayer == null;
    }
}