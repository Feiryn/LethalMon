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
        CustomAI[] customAis = GameObject.FindObjectsOfType<CustomAI>();
        Debug.Log($"End of game, processing {customAis.Length} custom AIs");
        
        foreach (CustomAI customAi in customAis)
        {
            PlayerControllerB player = customAi.ownerPlayer;
            Debug.Log("Player is in hangar ship room: " + player.isInHangarShipRoom);
            if (player.isInHangarShipRoom)
            {
                PokeballItem pokeballItem = customAi.RetrieveInBall(player.transform.position);
                player.SetItemInElevator(true, true, pokeballItem);
                pokeballItem.transform.SetParent(__instance.elevatorTransform, worldPositionStays: true);
            }
        }
    }

    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnClientDisconnect))]
    [HarmonyPostfix]
    private static void OnClientDisconnectPostFix(StartOfRound __instance, ulong clientId)
    {
        Debug.Log($"Client with ID {clientId} disconnected. Starting to delete its pets");
        
        CustomAI[] customAis = GameObject.FindObjectsOfType<CustomAI>();

        foreach (CustomAI customAi in customAis)
        {
            if (customAi.ownClientId == clientId)
            {
                Debug.Log($"Found {customAi.enemyType.name}, retrieving in ball");
                
                customAi.RetrieveInBall(__instance.transform.position);
            }
        }
    }
}