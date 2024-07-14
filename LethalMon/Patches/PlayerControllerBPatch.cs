using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using LethalMon.Behaviours;
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
    
    private static bool SentBallScanTip = false;
    
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
        LethalMon.Log("Send pet retrieve server rpc send finished");
    }
    
    private static void __rpc_handler_346187524u(NetworkBehaviour target, FastBufferReader reader,
        __RpcParams rpcParams)
    {
        NetworkManager networkManager = target.NetworkManager;
        if (networkManager != null && networkManager.IsListening)
        {
            LethalMon.Log("Execute RPC handler " + MethodBase.GetCurrentMethod().Name);
            
            PlayerControllerB player = (PlayerControllerB) target;
            TamedEnemyBehaviour? tamedEnemyBehaviour = Utils.GetPlayerPet(player);

            if (tamedEnemyBehaviour != null)
            {
                PetRetrieve(player, tamedEnemyBehaviour);
            }
            else
            {
                LethalMon.Log("No tamed enemy found for " + player + " but they sent a retrieve ball RPC");
            }
        }
    }

    private static void PetRetrieve(PlayerControllerB player, TamedEnemyBehaviour tamedEnemyBehaviour)
    {
        Vector3 spawnPos = Utils.GetPositionInFrontOfPlayerEyes(player);
        PokeballItem pokeballItem = tamedEnemyBehaviour.RetrieveInBall(spawnPos);
        bool inShip = StartOfRound.Instance.shipBounds.bounds.Contains(spawnPos);
        player.SetItemInElevator(inShip, inShip, pokeballItem);
        pokeballItem.transform.SetParent(StartOfRound.Instance.elevatorTransform, worldPositionStays: true);
    }

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.ConnectClientToPlayerObject))]
    [HarmonyPostfix]
    public static void ConnectPlayerPostfix(PlayerControllerB __instance)
    {
        ModConfig.Instance.RetrieveBallKey.performed += RetrieveBallKeyPressed;
        ModConfig.Instance.ActionKey1.performed += ActionKey1Pressed;
    }

    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Disconnect))]
    [HarmonyPrefix]
    public static void DisconnectPlayerPrefix()
    {
        ModConfig.Instance.RetrieveBallKey.performed -= RetrieveBallKeyPressed;
        ModConfig.Instance.ActionKey1.performed -= ActionKey1Pressed;
    }

    internal static void RetrieveBallKeyPressed(InputAction.CallbackContext dashContext)
    {
        if (Utils.IsHost)
        {
            LethalMon.Log("RetrieveBallKeyPressed");
            TamedEnemyBehaviour? tamedEnemyBehaviour = Utils.GetPlayerPet(Utils.CurrentPlayer);

            if (tamedEnemyBehaviour != null)
            {
                PetRetrieve(Utils.CurrentPlayer, tamedEnemyBehaviour);
            }
        }
        else
        {
            SendPetRetrievePacket(Utils.CurrentPlayer);
        }
    }
    internal static void ActionKey1Pressed(InputAction.CallbackContext dashContext)
    {
        LethalMon.Log("ActionKey1Pressed");
        TamedEnemyBehaviour? tamedEnemyBehaviour = Utils.GetPlayerPet(Utils.CurrentPlayer);
        if (tamedEnemyBehaviour != null)
            tamedEnemyBehaviour.ActionKey1Pressed();
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
                            pokeballItem.SetCaughtEnemyServerRpc(enemyType.name);
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
        TamedEnemyBehaviour? tamedBehaviour = Utils.GetPlayerPet(__instance);

        if (tamedBehaviour != null)
        {
            LethalMon.Log("Teleport tamed enemy to " + pos);
            tamedBehaviour.Enemy.agent.enabled = false;
            tamedBehaviour.Enemy.transform.position = pos;
            tamedBehaviour.Enemy.agent.enabled = true;
            tamedBehaviour.Enemy.serverPosition = pos;
            tamedBehaviour.Enemy.SetEnemyOutside(!__instance.isInsideFactory);
        }
    }
    
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.KillPlayer))]
    [HarmonyPostfix]
    private static void KillPlayerPostfix(PlayerControllerB __instance)
    {
        TamedEnemyBehaviour? tamedBehaviour = Utils.GetPlayerPet(__instance);
        
        if (tamedBehaviour != null)
        {
            LethalMon.Log("Owner is dead, go back to the ball");
            tamedBehaviour.RetrieveInBall(tamedBehaviour.Enemy.transform.position);
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DamagePlayer))]
    [HarmonyPostfix]
    private static void DamagePlayerPostfix(PlayerControllerB __instance, int damageNumber, bool hasDamageSFX, bool callRPC, CauseOfDeath causeOfDeath, int deathAnimation, bool fallDamage, Vector3 force)
    {
        TamedEnemyBehaviour? tamedBehaviour = Utils.GetPlayerPet(__instance);

        if (tamedBehaviour != null)
        {
            EnemyAI? enemyAI = Utils.GetMostProbableAttackerEnemy(__instance, new StackTrace());

            if (enemyAI != null)
            {
                tamedBehaviour.targetEnemy = enemyAI;
                tamedBehaviour.SwitchToTamingBehaviour(TamedEnemyBehaviour.TamingBehaviour.TamedDefending);
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

        TamedEnemyBehaviour? tamedBehaviour = Utils.GetPlayerPet(__instance);

        if (tamedBehaviour != null && (__instance.IsServer || __instance.IsHost))
        {
            PlayerControllerB playerWhoHitControllerB = StartOfRound.Instance.allPlayerScripts[playerWhoHit];
            LethalMon.Log($"Player {playerWhoHitControllerB.playerUsername} hit {__instance.playerUsername}");

            if (__instance != playerWhoHitControllerB &&
                Vector3.Distance(__instance.transform.position, playerWhoHitControllerB.transform.position) < 5f)
            {
                tamedBehaviour.targetPlayer = playerWhoHitControllerB;
                tamedBehaviour.SwitchToTamingBehaviour(TamedEnemyBehaviour.TamingBehaviour.TamedDefending);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.SwitchToItemSlot))]
    [HarmonyPostfix]
    private static void SwitchToItemSlotPostFix(PlayerControllerB __instance, int slot,
        GrabbableObject fillSlotWithItem = null)
    {
        if (SentBallScanTip || !StartOfRound.Instance.shipHasLanded) return;
        
        GrabbableObject currentItem = __instance.ItemSlots[slot];
        if (currentItem != null)
        {
            LethalMon.Log("Current item type: " + currentItem.GetType());
            if (currentItem.GetType().IsSubclassOf(typeof(PokeballItem)) && !((PokeballItem) currentItem).enemyCaptured)
            {
                HUDManager.Instance.DisplayTip("LethalMon Tip", "Scan enemies to know if they are catchable or not");
                SentBallScanTip = true;
            }
        }
    }
}