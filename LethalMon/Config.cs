using GameNetcodeStuff;
using HarmonyLib;
using LethalCompanyInputUtils.Api;
using Newtonsoft.Json;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using static Unity.Netcode.CustomMessagingManager;

namespace LethalMon
{
    public sealed class ModConfig : LcInputActions
    {
        #region Properties
        public struct ConfigValues
        {
            public bool DebugLog { get; set; }
        }

        public ConfigValues values = new ConfigValues();

        // Seperate key
        public InputAction RetrieveBallKey => Asset["retreiveBallKey"];

        private static ModConfig instance = null;
        public static ModConfig Instance
        {
            get
            {
                if (instance == null)
                    instance = new ModConfig();
                
                return instance;
            }
        }
        #endregion

        public void Setup()
        {
            values.DebugLog = LethalMon.Instance.Config.Bind("Alpha", "DebugLog", false, "Additional logging to help identifying issues of this mod.").Value;
        }

        override public void CreateInputActions(in InputActionMapBuilder builder)
        {
            var retrieveBallKeyKeyboard = LethalMon.Instance.Config.Bind("Controls", "RetrieveBallKeyKeyboard", "<Keyboard>/p", "Key for retreiving the tamed enemy inside its ball. Requires a restart after changing.").Value;
            var retrieveBallKeyGamepad = LethalMon.Instance.Config.Bind("Controls", "RetrieveBallKeyGamepad", "<Gamepad>/rightShoulder", "Gamepad key for retreiving the tamed enemy inside its ball. Requires a restart after changing.").Value;
            builder.NewActionBinding()
                .WithActionId("retreiveBallKey")
                .WithActionType(InputActionType.Button)
                .WithBindingName("RetrieveBallKey")
                .WithKbmPath(retrieveBallKeyKeyboard)
                .WithGamepadPath(retrieveBallKeyGamepad)
                .Finish();
        }

        [HarmonyPatch]
        public class SyncHandshake
        {
            #region Constants
            private const string REQUEST_MESSAGE = MyPluginInfo.PLUGIN_NAME + "_" + "HostConfigRequested";
            private const string RECEIVE_MESSAGE = MyPluginInfo.PLUGIN_NAME + "_" + "HostConfigReceived";
            #endregion

            #region Networking
            [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
            [HarmonyPostfix]
            public static void Initialize()
            {
                if (Utils.IsHost)
                {
                    Debug.Log("Current player is the host.");
                    NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(REQUEST_MESSAGE, new HandleNamedMessageDelegate(HostConfigRequested));
                }
                else
                {
                    Debug.Log("Current player is not the host.");
                    NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(RECEIVE_MESSAGE, new HandleNamedMessageDelegate(HostConfigReceived));
                    RequestHostConfig();
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

                string json = JsonConvert.SerializeObject(Instance.values);
                Debug.Log("Client [" + clientId + "] requested host config. Sending own config: " + json);

                int writeSize = FastBufferWriter.GetWriteSize(json);
                using FastBufferWriter writer = new FastBufferWriter(writeSize, Allocator.Temp);
                writer.WriteValueSafe(json);
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(RECEIVE_MESSAGE, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }

            public static void HostConfigReceived(ulong clientId, FastBufferReader reader)
            {
                reader.ReadValueSafe(out string json);
                Debug.Log("Received host config: " + json);
                ConfigValues hostValues = JsonConvert.DeserializeObject<ConfigValues>(json);

                Instance.values = hostValues;
            }
            #endregion
        }
    }
}
