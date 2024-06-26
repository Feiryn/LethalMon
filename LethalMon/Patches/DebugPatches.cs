using HarmonyLib;
using UnityEngine.InputSystem;
using GameNetcodeStuff;
using UnityEngine;
using Unity.Netcode;
using System.Collections;
using LethalMon.Items;

namespace LethalMon.Patches
{
    [HarmonyPatch]
    internal class DebugPatches
    {
#if DEBUG
        private static bool Executing = false;

        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        public static void OnUpdate()
        {
            if (!Executing)
                CheckFunctionKeys();
        }

        public static IEnumerator WaitAfterKeyPress()
        {
            if (Executing) yield break;

            Executing = true;
            yield return new WaitForSeconds(0.2f);
            Executing = false;
        }

        public static void CheckFunctionKeys()
        {
            /*if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
            }

            else if (Keyboard.current.f2Key.wasPressedThisFrame)
            {
            }

            else if (Keyboard.current.f3Key.wasPressedThisFrame)
            {
            }

            else if (Keyboard.current.f4Key.wasPressedThisFrame)
            {
            }

            else if (Keyboard.current.f5Key.wasPressedThisFrame)
            {
            }

            else if (Keyboard.current.f6Key.wasPressedThisFrame)
            {
            }

            else if (Keyboard.current.f7Key.wasPressedThisFrame)
            {
            }

            else if (Keyboard.current.f8Key.wasPressedThisFrame)
            {
            }

            else */if (Keyboard.current.f9Key.wasPressedThisFrame)
            {
                if (Pokeball.spawnPrefab != null)
                    SpawnItemInFront(Pokeball.spawnPrefab);
            }

            else if (Keyboard.current.f10Key.wasPressedThisFrame)
            {
                if (Greatball.spawnPrefab != null)
                    SpawnItemInFront(Greatball.spawnPrefab);
            }

            else if (Keyboard.current.f11Key.wasPressedThisFrame)
            {
                if (Ultraball.spawnPrefab != null)
                    SpawnItemInFront(Ultraball.spawnPrefab);
            }

            else if (Keyboard.current.f12Key.wasPressedThisFrame)
            {
                if (Masterball.spawnPrefab != null)
                    SpawnItemInFront(Masterball.spawnPrefab);
            }

            else
                return;

            GameNetworkManager.Instance?.StartCoroutine(WaitAfterKeyPress());
        }

        #region Methods
        public static void SpawnItemInFront(Item item)
        {
            if (item == null) return;

            SpawnItemInFront(item.spawnPrefab);
        }

        public static void SpawnItemInFront(GameObject networkPrefab)
        {
            if (!Utils.IsHost)
            {
                LethalMon.Log("That's a host-only debug feature.", LethalMon.LogType.Error);
                return;
            }

            if (networkPrefab == null)
            {
                LethalMon.Log("Unable to spawn item. networkPrefab was null.", LethalMon.LogType.Error);
                return;
            }

            var item = Object.Instantiate(networkPrefab);
            Object.DontDestroyOnLoad(item);
            item.GetComponent<NetworkObject>()?.Spawn();
            item.transform.position = Utils.CurrentPlayer.transform.position + Utils.CurrentPlayer.transform.forward * 1.5f;
            if (item.TryGetComponent(out GrabbableObject grabbableObject))
                grabbableObject.itemProperties.canBeGrabbedBeforeGameStart = true;
        }
        #endregion
#endif
    }
}
