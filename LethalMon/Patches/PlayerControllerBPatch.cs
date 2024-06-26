using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using LethalMon.AI;
using LethalMon.Items;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace LethalMon.Patches;

public class PlayerControllerBPatch
{
    private static bool lastTestPressed = false;

    private static int currentTestEnemyTypeIndex = 0;

    private static string[] testEnemyTypes = new List<string>(Data.CatchableMonsters.Keys).ToArray();
    
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

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.ConnectClientToPlayerObject))]
    [HarmonyPostfix]
    public static void ConnectPlayerPostfix(PlayerControllerB __instance)
    {
        ModConfig.Instance.RetrieveBallKey.performed += RetrieveBallKeyPressed;
    }

    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Disconnect))]
    [HarmonyPrefix]
    public static void DisconnectPlayerPrefix()
    {
        ModConfig.Instance.RetrieveBallKey.performed -= RetrieveBallKeyPressed;
    }

    internal static void RetrieveBallKeyPressed(InputAction.CallbackContext dashContext)
    {
        LethalMon.Logger.LogInfo("RetrieveBallKeyPressed");
        CustomAI? customAI = Utils.GetPlayerPet(GameNetworkManager.Instance.localPlayerController);

        if (customAI != null)
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                PetRetrieve(GameNetworkManager.Instance.localPlayerController, customAI);
            }
            else
            {
                SendPetRetrievePacket(GameNetworkManager.Instance.localPlayerController);
            }
        }
    }
    
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.Update))]
    [HarmonyPostfix]
    private static void UpdatePostfix(PlayerControllerB __instance)
    {
        if (__instance is { isPlayerControlled: true, IsOwner: true })
        {
            if (StartOfRound.Instance.testRoom != null && (__instance.IsHost || __instance.IsServer))
            {
                bool oPressed = Keyboard.current[Key.O].IsPressed();
                if (oPressed && !lastTestPressed)
                {
                    lastTestPressed = true;

                    GrabbableObject heldItem = __instance.ItemSlots[__instance.currentItemSlot];
                    if (heldItem != null)
                    {
                        PokeballItem pokeballItem = heldItem.GetComponent<PokeballItem>();
                        if (pokeballItem != null)
                        {
                            EnemyType enemyType = Resources.FindObjectsOfTypeAll<EnemyType>().First(enemyType =>
                                enemyType.name == testEnemyTypes[currentTestEnemyTypeIndex]);
                            pokeballItem.SetCaughtEnemy(enemyType);
                            HUDManager.Instance.AddTextMessageClientRpc("Caught enemy: " + enemyType.name);
                            
                            currentTestEnemyTypeIndex++;
                            if (currentTestEnemyTypeIndex >= testEnemyTypes.Length)
                            {
                                currentTestEnemyTypeIndex = 0;
                            }
                        }
                    }
                }
                else if (!oPressed)
                {
                    lastTestPressed = false;
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

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DamagePlayer))]
    [HarmonyPostfix]
    private static void DamagePlayerPostfix(PlayerControllerB __instance, int damageNumber, bool hasDamageSFX, bool callRPC, CauseOfDeath causeOfDeath, int deathAnimation, bool fallDamage, Vector3 force)
    {
        CustomAI? customAI = Utils.GetPlayerPet(__instance);

        if (customAI != null && customAI.GetType() == typeof(RedLocustBeesCustomAI))
        {
            EnemyAI? enemyAI = Utils.GetMostProbableAttackerEnemy(__instance, new StackTrace());

            if (enemyAI != null)
            {
                ((RedLocustBeesCustomAI)customAI).AttackEnemyAI(enemyAI);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DamagePlayerFromOtherClientServerRpc))]
    [HarmonyPostfix]
    private static void DamagePlayerFromOtherClientServerRpcPostFix(PlayerControllerB __instance, Vector3 hitDirection, int playerWhoHit)
    {
        if ((int) __instance.playerClientId == playerWhoHit)
        {
            return;
        }
        
        CustomAI? customAI = Utils.GetPlayerPet(__instance);

        if (customAI != null && customAI.GetType() == typeof(RedLocustBeesCustomAI) && (__instance.IsServer || __instance.IsHost))
        {
            PlayerControllerB playerWhoHitControllerB = StartOfRound.Instance.allPlayerScripts[playerWhoHit];
            Debug.Log($"Player {playerWhoHitControllerB.playerUsername} hit {__instance.playerUsername}");

            if (__instance != playerWhoHitControllerB &&
                Vector3.Distance(__instance.transform.position, playerWhoHitControllerB.transform.position) < 5f)
            {
                ((RedLocustBeesCustomAI)customAI).AttackPlayer(playerWhoHitControllerB);
            }
        }
    }
}