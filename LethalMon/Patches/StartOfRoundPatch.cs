using GameNetcodeStuff;
using HarmonyLib;
using LethalMon.AI;
using LethalMon.Items;
using UnityEngine;

namespace LethalMon.Patches;

public class StartOfRoundPatch
{
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ShipHasLeft))]
    [HarmonyPrefix]
    private static void OnShipHasLeftPreFix(StartOfRound __instance)
    {
        TamedEnemyBehaviour[] tamedBehaviours = GameObject.FindObjectsOfType<TamedEnemyBehaviour>();
        LethalMon.Log($"End of game, processing {tamedBehaviours.Length} tamed enemies");
        
        foreach (TamedEnemyBehaviour tamedBehaviour in tamedBehaviours)
        {
            if(tamedBehaviour.ownerPlayer == null) continue;

            LethalMon.Log("Player is in hangar ship room: " + tamedBehaviour.ownerPlayer.isInHangarShipRoom);
            if (tamedBehaviour.ownerPlayer.isInHangarShipRoom)
            {
                PokeballItem pokeballItem = tamedBehaviour.RetrieveInBall(tamedBehaviour.ownerPlayer.transform.position);
                tamedBehaviour.ownerPlayer.SetItemInElevator(true, true, pokeballItem);
                pokeballItem.transform.SetParent(__instance.elevatorTransform, worldPositionStays: true);
            }
        }
    }

    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnPlayerDC))]
    [HarmonyPrefix]
    private static void OnPlayerDCPrefix(StartOfRound __instance, int playerObjectNumber, ulong clientId)
    {
        LethalMon.Log($"Client with ID {clientId} disconnected. Starting to delete its pets");

        TamedEnemyBehaviour[] tamedBehaviours = GameObject.FindObjectsOfType<TamedEnemyBehaviour>();

        foreach (TamedEnemyBehaviour tamedBehaviour in tamedBehaviours)
        {
            if (tamedBehaviour.ownClientId == clientId)
            {
                LethalMon.Log($"Found {tamedBehaviour.Enemy.enemyType.name}, retrieving in ball");

                tamedBehaviour.RetrieveInBall(tamedBehaviour.Enemy.transform.position);
            }
        }
    }
}