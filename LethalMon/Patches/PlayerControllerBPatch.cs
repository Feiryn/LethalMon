using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using LethalMon.AI;
using LethalMon.Items;
using LethalMon.Throw;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LethalMon.Patches;

public class PlayerControllerBPatch
{
    internal static void InitializeRPCS()
    {
        NetworkManager.__rpc_func_table.Add(346187524u, __rpc_handler_346187524u);
    }
    
    public static void SendPetRetrievePacket(PlayerControllerB player)
    {
        ServerRpcParams rpcParams = default(ServerRpcParams);
        FastBufferWriter writer = (FastBufferWriter) player.GetType().GetMethod("__beginSendServerRpc", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(player, new object[]
            {
                346187524u,
                rpcParams,
                RpcDelivery.Reliable
            });
        NetworkObjectReference networkObject = player.GetComponent<NetworkObject>();
        player.GetType().GetMethod("__endSendServerRpc", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(player, new object[]
            {
                writer,
                346187524u,
                rpcParams,
                RpcDelivery.Reliable
            });
        Debug.Log("Send pet retrieve server rpc send finished");
    }
    
    private static void __rpc_handler_346187524u(NetworkBehaviour target, FastBufferReader reader,
        __RpcParams rpcParams)
    {
        NetworkManager networkManager = target.NetworkManager;
        if (networkManager != null && networkManager.IsListening)
        {
            Debug.Log("Execute RPC handler " + MethodBase.GetCurrentMethod().Name);
            
            PlayerControllerB player = (PlayerControllerB) target;
            CustomAI? customAI = Utils.GetPlayerPet(player);

            if (customAI != null)
            {
                PetRetrieve(player, customAI);
            }
            else
            {
                Debug.Log("No custom AI found for " + player + " but they sent a retrieve ball RPC");
            }
        }
    }

    private static void PetRetrieve(PlayerControllerB player, CustomAI customAI)
    {
        Vector3 spawnPos = Utils.GetPositionInFrontOfPlayerEyes(player);
        PokeballItem pokeballItem = customAI.RetrieveInBall(spawnPos);
        bool inShip = StartOfRound.Instance.shipBounds.bounds.Contains(spawnPos);
        player.SetItemInElevator(inShip, inShip, pokeballItem);
        pokeballItem.transform.SetParent(StartOfRound.Instance.elevatorTransform, worldPositionStays: true);
    }
    
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.Update))]
    [HarmonyPostfix]
    private static void UpdatePostfix(PlayerControllerB __instance)
    {
        if (__instance is { isPlayerControlled: true, IsOwner: true } && InputControlExtensions.IsPressed(Keyboard.current[Key.P]))
        {
            Debug.Log("P pressed");
            CustomAI? customAI = Utils.GetPlayerPet(__instance);
            
            if (customAI != null)
            {
                if (__instance.NetworkManager.IsServer || __instance.NetworkManager.IsHost)
                {
                    PetRetrieve(__instance, customAI);
                }
                else
                {
                    SendPetRetrievePacket(__instance);
                }
            }
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.TeleportPlayer))]
    [HarmonyPostfix]
    private static void TeleportPlayer(PlayerControllerB __instance, Vector3 pos)
    {
        CustomAI? customAI = Utils.GetPlayerPet(__instance);

        if (customAI != null)
        {
            Debug.Log("Teleport CustomAI to " + pos);
            customAI.agent.enabled = false;
            customAI.transform.position = pos;
            customAI.agent.enabled = true;
            customAI.serverPosition = pos;
        }
    }
    
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.KillPlayer))]
    [HarmonyPostfix]
    private static void KillPlayerPostfix(PlayerControllerB __instance)
    {
        CustomAI? customAI = Utils.GetPlayerPet(__instance);
        
        if (customAI != null)
        {
            Debug.Log("Owner is dead, go back to the ball");
            customAI.RetrieveInBall(customAI.transform.position);
        }
    }
}