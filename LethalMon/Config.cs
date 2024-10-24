﻿using System;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using HarmonyLib;
using LethalCompanyInputUtils.Api;
using LethalCompanyInputUtils.BindingPathEnums;
using LethalMon.Items;
using Newtonsoft.Json;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using BepInEx.Configuration;
using LethalMon.Save;
using static Unity.Netcode.CustomMessagingManager;
using static LethalMon.ModConfig.ConfigValues;
using Object = UnityEngine.Object;

namespace LethalMon
{
    public sealed class ModConfig : LcInputActions
    {
        #region Properties
        public struct ConfigValues
        {
            public int Tier1BallSpawnWeight { get; set; }
            
            public int Tier2BallSpawnWeight { get; set; }
            
            public int Tier3BallSpawnWeight { get; set; }
            
            public int Tier4BallSpawnWeight { get; set; }
            
            public int Tier1BallCost { get; set; }
            
            public int Tier2BallCost { get; set; }
            
            public int Tier3BallCost { get; set; }
            
            public int Tier4BallCost { get; set; }
            
            public float FilledBallsPercentage { get; set; }
            
            public float EnemyHPCaptureProbabilityMultiplier { get; set; }

            public enum KeepBalls
            {
                No,
                FullOnly,
                Yes
            }

            public KeepBalls KeepBallsIfAllPlayersDead { get; set; }

            public int CaptureRateModifier { get; set; }
            
            public int[] DuplicationPrices { get; set; }

            public float TamedNameFontSize { get; set; }

            public string[] DisabledMonsters { get; set; }
            
            public bool MonstersReactToFailedCaptures { get; set; }
            
            public float BrackenGrabCooldown { get; set; }
            
            public float DressGirlTeleportCooldown { get; set; }
            
            public float HoardingBugBringItemCooldown { get; set; }
            
            public float FoxTongueHitCooldown { get; set; }

            public float EyelessDogHowlCooldown { get; set; }

            public float MaskedLendCooldown { get; set; }

            public float BeesStunCooldown { get; set; }

            public float MaskedEffectDistance { get; set; }

            public int BlobMaxItems { get; set; }

            public float SpiderWebCooldown { get; set; }
            
            public bool PcGlobalSave { get; set; }
            
            public float BarberCutWallCooldown { get; set; }
        }

        public ConfigValues values = new();
        
        public ConfigValues originalValues;
        
        public struct NetworkSyncData
        {
            public ConfigValues values;

            public Save.Save save;

            public Dictionary<ulong, PokeballSaveData> ballSaves;
        }

        // Seperate key
        public InputAction RetrieveBallKey => Asset["retreiveBallKey"];
        public InputAction ActionKey1 => Asset["actionKey1"];

        private static ModConfig? _instance = null;
        public static ModConfig Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ModConfig();
                
                return _instance;
            }
        }
        #endregion

        public void Setup()
        {
            // Saves
            values.PcGlobalSave = LethalMon.Instance.Config.Bind("Saves", "PcGlobalSave", true, "Make the PC saves global (true) or per save file (false)").Value;
            
            // Items
            values.Tier1BallCost = LethalMon.Instance.Config.Bind("Items", "Tier1BallCost", 40, "The cost of the tier 1 ball (pokeball) item in the shop. -1 to disable").Value;
            values.Tier2BallCost = LethalMon.Instance.Config.Bind("Items", "Tier2BallCost", 125, "The cost of the tier 1 ball (great ball) item in the shop. -1 to disable").Value;
            values.Tier3BallCost = LethalMon.Instance.Config.Bind("Items", "Tier3BallCost", 375, "The cost of the tier 1 ball (ultra ball) item in the shop. -1 to disable").Value;
            values.Tier4BallCost = LethalMon.Instance.Config.Bind("Items", "Tier4BallCost", 700, "The cost of the tier 1 ball (master ball) item in the shop. -1 to disable").Value;
            values.Tier1BallSpawnWeight = LethalMon.Instance.Config.Bind("Items", "Tier1BallSpawnWeight", 20, "The spawn weight of the tier 1 ball (pokeball). Higher = more common").Value;
            values.Tier2BallSpawnWeight = LethalMon.Instance.Config.Bind("Items", "Tier2BallSpawnWeight", 10, "The spawn weight of the tier 2 ball (great ball). Higher = more common").Value;
            values.Tier3BallSpawnWeight = LethalMon.Instance.Config.Bind("Items", "Tier3BallSpawnWeight", 6, "The spawn weight of the tier 3 ball (ultra ball). Higher = more common").Value;
            values.Tier4BallSpawnWeight = LethalMon.Instance.Config.Bind("Items", "Tier4BallSpawnWeight", 2, "The spawn weight of the tier 4 ball (master ball). Higher = more common").Value;
            values.KeepBallsIfAllPlayersDead = LethalMon.Instance.Config.Bind("Items", "KeepBallsIfAllPlayersDead", KeepBalls.No, "Make the balls don't despawn even if all the players are dead.").Value;
            values.FilledBallsPercentage = LethalMon.Instance.Config.Bind("Items", "FilledBallsPercentage", 0.5f, "Percentage of filled balls in the dungeon").Value;
            values.EnemyHPCaptureProbabilityMultiplier = LethalMon.Instance.Config.Bind("Items", "EnemyHPCaptureProbabilityMultiplier", 1f, "Lower enemy HP increases the capture probability. Set this to 0 to disable this feature").Value;
            
            // Monsters
            values.DisabledMonsters = LethalMon.Instance.Config.Bind("Monsters", "DisabledMonsters", "", "Disabled monsters types. Separate with a comma and don't put spaces. Example: Monster1,Monster2. Available monsters: " + string.Join(", ", Enum.GetNames(typeof(Utils.Enemy)))).Value.Split(",");
            values.MonstersReactToFailedCaptures = LethalMon.Instance.Config.Bind("Monsters", "MonstersReactToFailedCaptures", true, "Make the monsters react aggressively if a capture fails").Value;
            values.CaptureRateModifier = LethalMon.Instance.Config.Bind("Monsters", "CaptureRateModifier", 0, new ConfigDescription("Modifier for the capture rate. Each monster have a difficulty to catch between 1 and 10. You can modify all the monsters difficulty by adding this modifier to the base difficulty. Negative = easier to catch, positive = harder to catch", new AcceptableValueRange<int>(-10, 10))).Value;
            values.TamedNameFontSize = LethalMon.Instance.Config.Bind("Monsters", "TamedNameFontSize", 10f, new ConfigDescription("Font size of the text above tamed monsters, that shows the owner. Set this to 0 to disable the text.", new AcceptableValueRange<float>(0f, 20f))).Value;
            
            // Monsters > Duplication prices
            string[] duplicationPrices = LethalMon.Instance.Config.Bind("Monsters", "DuplicationPrices", String.Join(",", Data.DuplicationPrices), "Prices for duplicating a monster. The first value is the price for a monster with a difficulty of 1, the second value for a monster with a difficulty of 2, etc. 10 difficulties in total").Value.Split(",");
            values.DuplicationPrices = new int[10];
            for (var i = 0; i < 10; i++)
            {
                try
                {
                    values.DuplicationPrices[i] = int.Parse(duplicationPrices[i]);
                }
                catch
                {
                    values.DuplicationPrices[i] = Data.DuplicationPrices[i];
                }
            }

            // Cooldowns
            values.BrackenGrabCooldown = LethalMon.Instance.Config.Bind("Cooldowns", "BrackenGrabCooldown", 20f, "Grab cooldown time in seconds for the bracken").Value;
            values.DressGirlTeleportCooldown = LethalMon.Instance.Config.Bind("Cooldowns", "DressGirlTeleportCooldown", 60f, "Teleport cooldown time in seconds for the dress girl").Value;
            values.HoardingBugBringItemCooldown = LethalMon.Instance.Config.Bind("Cooldowns", "HoardingBugBringItemCooldown", 5f, "Bring item cooldown time in seconds for the hoarder bug").Value;
            values.FoxTongueHitCooldown = LethalMon.Instance.Config.Bind("Cooldowns", "FoxTongueHitCooldown", 5f, "Tongue hit cooldown time in seconds for the fox").Value;
            values.EyelessDogHowlCooldown = LethalMon.Instance.Config.Bind("Cooldowns", "EyelessDogHowlCooldown", 5f, "Howl cooldown time in seconds for the eyeless dog").Value;
            values.MaskedLendCooldown = LethalMon.Instance.Config.Bind("Cooldowns", "MaskedLendCooldown", 7f, "Mask lending cooldown time in seconds for the masked").Value;
            values.BeesStunCooldown = LethalMon.Instance.Config.Bind("Cooldowns", "BeesStunCooldown", 10f, "Stunning cooldown time in seconds for the bees").Value;
            values.SpiderWebCooldown = LethalMon.Instance.Config.Bind("Cooldowns", "SpiderWebCooldown", 8f, "Web shooting cooldown time in seconds for the spider").Value;
            values.BarberCutWallCooldown = LethalMon.Instance.Config.Bind("Cooldowns", "BarberCutWallCooldown", 30f, "Cutting wall cooldown time in seconds for the barber").Value;

            // Behaviours
            values.MaskedEffectDistance = LethalMon.Instance.Config.Bind("Behaviours", "MaskedEffectDistance", 20f, "Distance till which enemies can be seen through walls.").Value;
            values.BlobMaxItems = LethalMon.Instance.Config.Bind("Behaviours", "BlobMaxItems", 4, "Maximum amount of items a tamed blob can carry.").Value;

            // Save the config for game changes
            originalValues = values;
        }

        override public void CreateInputActions(in InputActionMapBuilder builder)
        {
            var retrieveBallKeyKeyboard = LethalMon.Instance.Config.Bind("Controls", "RetrieveBallKeyKeyboard", "<Keyboard>/" + KeyboardControl.P.ToString(), "Key for retrieving the tamed enemy inside its ball. Requires a restart after changing.").Value;
            var retrieveBallKeyGamepad = LethalMon.Instance.Config.Bind("Controls", "RetrieveBallKeyGamepad", "<Gamepad>/" + GamepadControl.RightStickPress.ToString(), "Gamepad key for retrieving the tamed enemy inside its ball. Requires a restart after changing.").Value;
            builder.NewActionBinding()
                .WithActionId("retreiveBallKey")
                .WithActionType(InputActionType.Button)
                .WithBindingName("RetrieveBallKey")
                .WithKbmPath(retrieveBallKeyKeyboard)
                .WithGamepadPath(retrieveBallKeyGamepad)
                .Finish();

            
            var actionKey1Keyboard = LethalMon.Instance.Config.Bind("Controls", "ActionKey1Keyboard", "<Keyboard>/" + KeyboardControl.B.ToString(), "Key for the first custom action on a tamed enemy. Requires a restart after changing.").Value;
            var actionKey1Gamepad = LethalMon.Instance.Config.Bind("Controls", "ActionKey1Gamepad", "<Gamepad>/" + GamepadControl.RightShoulder.ToString(), "Gamepad key for the first custom action on a tamed enemy. Requires a restart after changing.").Value;
            builder.NewActionBinding()
                .WithActionId("actionKey1")
                .WithActionType(InputActionType.Button)
                .WithBindingName("ActionKey1")
                .WithKbmPath(actionKey1Keyboard)
                .WithGamepadPath(actionKey1Gamepad)
                .Finish();
        }

        public static void ProcessValues()
        {
            LethalMon.Log("Processing config");
            
            if (Pokeball.BallItem != null)
            {
                if (Instance.values.Tier1BallCost >= 0)
                    LethalLib.Modules.Items.UpdateShopItemPrice(Pokeball.BallItem, Instance.values.Tier1BallCost);
                else
                    LethalLib.Modules.Items.RemoveShopItem(Pokeball.BallItem);
            }
            
            if (Greatball.BallItem != null)
            {
                if (Instance.values.Tier2BallCost >= 0)
                    LethalLib.Modules.Items.UpdateShopItemPrice(Greatball.BallItem, Instance.values.Tier2BallCost);
                else
                    LethalLib.Modules.Items.RemoveShopItem(Greatball.BallItem);
            }
            
            if (Ultraball.BallItem != null)
            {
                if (Instance.values.Tier3BallCost >= 0)
                    LethalLib.Modules.Items.UpdateShopItemPrice(Ultraball.BallItem, Instance.values.Tier3BallCost);
                else
                    LethalLib.Modules.Items.RemoveShopItem(Ultraball.BallItem);
            }
            
            if (Masterball.BallItem != null)
            {
                if (Instance.values.Tier4BallCost >= 0)
                    LethalLib.Modules.Items.UpdateShopItemPrice(Masterball.BallItem, Instance.values.Tier4BallCost);
                else
                    LethalLib.Modules.Items.RemoveShopItem(Masterball.BallItem);
            }

            foreach (var disabledMonster in Instance.values.DisabledMonsters)
            {
                Registry.RemoveEnemy(disabledMonster);
            }
        }

        [HarmonyPatch]
        public class SyncHandshake
        {
            #region Constants
            private const string REQUEST_MESSAGE = MyPluginInfo.PLUGIN_NAME + "_" + "HostConfigRequested";
            private const string RECEIVE_MESSAGE = MyPluginInfo.PLUGIN_NAME + "_" + "HostConfigReceived";
            #endregion

            #region Networking
            [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.ConnectClientToPlayerObject))]
            [HarmonyPostfix]
            public static void Initialize(PlayerControllerB __instance)
            {
                if (Utils.CurrentPlayer == __instance)
                {
                    if (Utils.IsHost)
                    {
                        Debug.Log("Current player is the host.");
                        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(REQUEST_MESSAGE, new HandleNamedMessageDelegate(HostConfigRequested));
                        Instance.values = Instance.originalValues; // Load the original config, if the game changed
                        ProcessValues();
                    }
                    else
                    {
                        Debug.Log("Current player is not the host.");
                        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(RECEIVE_MESSAGE, new HandleNamedMessageDelegate(HostConfigReceived));
                        RequestHostConfig();
                    }
                }
            }

            public static void RequestHostConfig()
            {
                if (!Utils.IsHost)
                {
                    Debug.Log("Sending config request to host.");
                    NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(REQUEST_MESSAGE, 0uL, new FastBufferWriter(0, Allocator.Temp), NetworkDelivery.ReliableSequenced);
                }
                else
                    Debug.Log("Config request not required. No other player available."); // Shouldn't happen, but who knows..
            }

            public static void HostConfigRequested(ulong clientId, FastBufferReader reader)
            {
                if (!Utils.IsHost) // Current player is not the host and therefor not the one who should react
                    return;
                
                string json = JsonConvert.SerializeObject(new NetworkSyncData
                {
                    save = SaveManager.GetSave(),
                    values = Instance.values,
                    ballSaves = Object.FindObjectsOfType<PokeballItem>().ToDictionary(ball => ball.NetworkObjectId, ball => (PokeballSaveData) ball.GetAdvancedItemDataToSave())
                });
                Debug.Log("Client [" + clientId + "] requested host config. Sending own config: " + json);

                int writeSize = FastBufferWriter.GetWriteSize(json);
                using FastBufferWriter writer = new(writeSize, Allocator.Temp);
                writer.WriteValueSafe(json);
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(RECEIVE_MESSAGE, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Method Declaration", "Harmony003:Harmony non-ref patch parameters modified", Justification = "Not an issue")]
            public static void HostConfigReceived(ulong clientId, FastBufferReader reader)
            {
                reader.ReadValueSafe(out string json);
                Debug.Log("Received host config: " + json);
                NetworkSyncData hostData = JsonConvert.DeserializeObject<NetworkSyncData>(json);

                SaveManager.SyncSave(hostData.save);
                Instance.values = hostData.values;
                
                if (PC.PC.Instance != null)
                    PC.PC.Instance.tutorialApp.UpdateTutorialPage2();

                foreach (var ball in Object.FindObjectsOfType<PokeballItem>())
                {
                    if (hostData.ballSaves.TryGetValue(ball.NetworkObjectId, out PokeballSaveData? saveData))
                        ball.LoadAdvancedItemData(saveData);
                }

                ProcessValues();
            }
            #endregion
        }
    }
}
