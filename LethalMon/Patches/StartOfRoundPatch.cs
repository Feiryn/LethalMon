using HarmonyLib;
using LethalMon.Behaviours;
using LethalMon.Save;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.Patches;

internal class StartOfRoundPatch
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
                var ballItem = tamedBehaviour.RetrieveInBall(tamedBehaviour.ownerPlayer.transform.position);
                tamedBehaviour.ownerPlayer.SetItemInElevator(true, true, ballItem);
                ballItem?.transform.SetParent(__instance.elevatorTransform, worldPositionStays: true);
            }
        }
    }

    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnPlayerDC))]
    [HarmonyPrefix]
    private static void OnPlayerDCPrefix(int playerObjectNumber, ulong clientId)
    {
        LethalMon.Log($"Client with ID {clientId} disconnected. Starting to delete its pets");

        TamedEnemyBehaviour[] tamedBehaviours = GameObject.FindObjectsOfType<TamedEnemyBehaviour>();

        foreach (TamedEnemyBehaviour tamedBehaviour in tamedBehaviours)
        {
            if (tamedBehaviour.OwnerID == clientId)
            {
                LethalMon.Log($"Found {tamedBehaviour.Enemy.enemyType.name}, retrieving in ball");

                tamedBehaviour.RetrieveInBall(tamedBehaviour.Enemy.transform.position);
            }
        }
        
        if (PC.PC.Instance.CurrentPlayer?.playerClientId == clientId)
        {
            PC.PC.Instance.StopUsingServerRpc(PC.PC.Instance.CurrentPlayer.GetComponent<NetworkObject>());
        }
    }
    
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ResetShipFurniture))]
    [HarmonyPostfix]
    private static void OnResetShipFurniturePostFix()
    {
        Utils.UnlockPCIfNotUnlocked();

        Registry.LoadAndCalculateMissingIds();
    }
}