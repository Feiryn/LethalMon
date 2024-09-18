using System.Linq;
using HarmonyLib;
using LethalMon.Behaviours;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

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
                var pokeballItem = tamedBehaviour.RetrieveInBall(tamedBehaviour.ownerPlayer.transform.position);
                tamedBehaviour.ownerPlayer.SetItemInElevator(true, true, pokeballItem);
                pokeballItem?.transform.SetParent(__instance.elevatorTransform, worldPositionStays: true);
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

    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Start))]
    [HarmonyPrefix]
    private static void StartPrefix(StartOfRound __instance)
    {
        if (__instance.unlockablesList.unlockables.Any(u => u.unlockableName == PC.PC.UnlockableName))
            return;
        
        PC.PC.AddToShip();
    }
    
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.LoadUnlockables))]
    [HarmonyPostfix]
    private static void LoadUnlockablesPostFix(StartOfRound __instance)
    {
        if (Object.FindObjectsOfType<PC.PC>().Length == 0)
            __instance.SpawnUnlockable(__instance.unlockablesList.unlockables.FindIndex(u => u.unlockableName == PC.PC.UnlockableName));
    }
}