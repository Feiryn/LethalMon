using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using LethalMon.Behaviours;
using LethalMon.Items;
using LethalMon.Save;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LethalMon.Patches;

public class PlayerControllerBPatch
{
    private static bool lastTestPressed = false;

    private static int currentTestEnemyTypeIndex = 0;

    private static readonly string[] testEnemyTypes = new List<string>(Data.CatchableMonsters.Keys).ToArray();
    
    private static bool SentBallScanTip = false;
    
    internal static void InitializeRPCS()
    {
        NetworkManager.__rpc_func_table.Add(346187524u, __rpc_handler_346187524u);
    }
    
    public static void SendPetRetrievePacket(PlayerControllerB player)
    {
        ServerRpcParams rpcParams = default;
        FastBufferWriter writer = (FastBufferWriter) player.GetType().GetMethod("__beginSendServerRpc", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(player,
            [
                346187524u,
                rpcParams,
                RpcDelivery.Reliable
            ]);
        player.GetType().GetMethod("__endSendServerRpc", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(player,
            [
                writer,
                346187524u,
                rpcParams,
                RpcDelivery.Reliable
            ]);
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
        PokeballItem? pokeballItem = tamedEnemyBehaviour.RetrieveInBall(spawnPos);
        if (pokeballItem == null) return;

        bool inShip = StartOfRound.Instance.shipBounds.bounds.Contains(spawnPos);
        player.SetItemInElevator(inShip, inShip, pokeballItem);
        pokeballItem.transform.SetParent(StartOfRound.Instance.elevatorTransform, worldPositionStays: true);
        
        if (StartOfRound.Instance.shipBounds.bounds.Contains(spawnPos))
        {
            player.SetItemInElevator(inShip, inShip, pokeballItem);
            pokeballItem.transform.SetParent(StartOfRound.Instance.elevatorTransform, worldPositionStays: true);
        }
        else
        {
            pokeballItem.transform.SetParent(StartOfRound.Instance.propsContainer, worldPositionStays: true);
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.ConnectClientToPlayerObject))]
    [HarmonyPostfix]
    public static void ConnectPlayerPostfix()
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
        HUDManagerPatch.EnableHUD(false);
        if (!ModConfig.Instance.values.PcGlobalSave)
        {
            SaveManager.ClearSave();
        }
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
        Utils.GetPlayerPet(Utils.CurrentPlayer)?.ActionKey1Pressed();
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
                            pokeballItem.SetCaughtEnemyServerRpc(enemyType.name, string.Empty);
                            pokeballItem.isDnaComplete = true;
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

#if DEBUG
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.TeleportPlayer))]
    [HarmonyPrefix]
    private static void TeleportPlayerPrefix(PlayerControllerB __instance, Vector3 pos)
    {
        LethalMon.Log(__instance.playerUsername + " is teleporting from " + __instance.transform.position + " to " + pos);
    }
#endif

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.TeleportPlayer))]
    [HarmonyPostfix]
    private static void TeleportPlayer(PlayerControllerB __instance, Vector3 pos)
    {
        TamedEnemyBehaviour? tamedBehaviour = Utils.GetPlayerPet(__instance);

        if (tamedBehaviour != null && tamedBehaviour.CanBeTeleported())
        {
            var position = pos;
            LethalMon.Log("Teleport tamed enemy to " + position);
            var isControlled = tamedBehaviour.TryGetComponent(out EnemyController controller) && controller.IsPlayerControlled;
            if(isControlled)
            {
                position += controller.EnemyOffsetWhileControlling;
                position.y += __instance.transform.localScale.y;
            }
            tamedBehaviour.Enemy.transform.position = position;

            if(controller == null || !controller.EnemyCanFly)
                tamedBehaviour.Enemy.agent.enabled = true;
            tamedBehaviour.Enemy.serverPosition = position;
            tamedBehaviour.isOutside = !__instance.isInsideFactory;
        }
    }
    
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.KillPlayerServerRpc))]
    [HarmonyPostfix]
    private static void KillPlayerServerRpcPostfix(PlayerControllerB __instance)
    {
        TamedEnemyBehaviour? tamedBehaviour = Utils.GetPlayerPet(__instance);
        
        if (tamedBehaviour != null && Utils.IsHost && !tamedBehaviour.hasBeenRetrieved)
        {
            LethalMon.Log("Owner is dead, go back to the ball");
            tamedBehaviour.RetrieveInBall(tamedBehaviour.Enemy.transform.position);
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DamagePlayer))]
    [HarmonyPostfix]
    private static void DamagePlayerPostfix(PlayerControllerB __instance/*, int damageNumber, bool hasDamageSFX, bool callRPC, CauseOfDeath causeOfDeath, int deathAnimation, bool fallDamage, Vector3 force*/)
    {
        TamedEnemyBehaviour? tamedBehaviour = Utils.GetPlayerPet(__instance);

        if (tamedBehaviour != null && tamedBehaviour.CanDefend)
        {
            EnemyAI? enemyAI = Utils.GetMostProbableAttackerEnemy(__instance, new StackTrace());
            if (enemyAI != null && enemyAI != tamedBehaviour.Enemy)
            {
                TamedEnemyBehaviour? tamedEnemyBehaviour = enemyAI.GetComponentInParent<TamedEnemyBehaviour>();

                if (tamedEnemyBehaviour == null || tamedEnemyBehaviour.ownerPlayer == null)
                {
                    tamedBehaviour.targetEnemy = enemyAI;
                    tamedBehaviour.SwitchToTamingBehaviour(TamedEnemyBehaviour.TamingBehaviour.TamedDefending);
                }
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

        if (tamedBehaviour != null && (__instance.IsServer || __instance.IsHost) && tamedBehaviour.CanDefend)
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
    private static void SwitchToItemSlotPostFix(PlayerControllerB __instance, int slot/*, GrabbableObject fillSlotWithItem = null*/)
    {
        if (SentBallScanTip || !StartOfRound.Instance.shipHasLanded || __instance != Utils.CurrentPlayer) return;
        
        GrabbableObject currentItem = __instance.ItemSlots[slot];
        if (currentItem != null)
        {
            if (currentItem.GetType().IsSubclassOf(typeof(PokeballItem)) && !((PokeballItem) currentItem).enemyCaptured)
            {
                HUDManager.Instance.DisplayTip("LethalMon Tip", "Scan base game enemies to know if they are catchable or not. Modded enemies are not catchable.");
                SentBallScanTip = true;
            }
        }
    }
    
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.KillPlayer))]
    [HarmonyPrefix]
    private static void KillPlayerPrefix(PlayerControllerB __instance)
    {
        if (__instance.inTerminalMenu && PC.PC.Instance.CurrentPlayer == __instance && Utils.CurrentPlayer == __instance && __instance.AllowPlayerDeath())
        {
            PC.PC.Instance.StopUsing();
        }
    }
}