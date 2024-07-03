﻿using HarmonyLib;
using UnityEngine.InputSystem;
using GameNetcodeStuff;
using UnityEngine;
using Unity.Netcode;
using System.Collections;
using LethalMon.Items;
using System.Collections.Generic;
using System.Linq;
using Unity.Services.Authentication;

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

            else */if (Keyboard.current.f4Key.wasPressedThisFrame)
            {
                TeleportOutsideDungeon();
            }

            else if (Keyboard.current.f5Key.wasPressedThisFrame)
            {
                SpawnEnemyInFrontOfPlayer(Utils.CurrentPlayer, Utils.Enemy.HoarderBug.ToString());
            }

            else if (Keyboard.current.f6Key.wasPressedThisFrame)
            {
                SpawnEnemyInFrontOfPlayer(Utils.CurrentPlayer, Utils.Enemy.Puffer.ToString());
            }

            else if (Keyboard.current.f7Key.wasPressedThisFrame)
            {
                SpawnEnemyInFrontOfPlayer(Utils.CurrentPlayer, Utils.Enemy.RedLocustBees.ToString());
            }

            else if (Keyboard.current.f8Key.wasPressedThisFrame)
            {
                SpawnEnemyInFrontOfPlayer(Utils.CurrentPlayer, Utils.Enemy.Flowerman.ToString());
            }

            else if (Keyboard.current.f9Key.wasPressedThisFrame)
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

        #region Item
        public static void SpawnItemInFront(Item item)
        {
            if (item == null) return;

            SpawnItemInFront(item.spawnPrefab);
        }

        public static void SpawnItemInFront(GameObject networkPrefab)
        {
            if (!Utils.IsHost)
            {
                LethalMon.Logger.LogError("That's a host-only debug feature.");
                return;
            }

            if (networkPrefab == null)
            {
                LethalMon.Logger.LogError("Unable to spawn item. networkPrefab was null.");
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

        #region Enemy
        public static void SpawnEnemyInFrontOfPlayer(PlayerControllerB targetPlayer, string enemyName)
        {
            foreach (EnemyType enemyType in Utils.EnemyTypes)
            {
                if (enemyName != enemyType.name) continue;

                var location = targetPlayer.transform.position + targetPlayer.transform.forward * 5f;
                LethalMon.Logger.LogInfo("Spawn enemy: " + enemyName);
                GameObject gameObject = Object.Instantiate(enemyType.enemyPrefab, location, Quaternion.Euler(new Vector3(0f, 0f /*yRot*/, 0f)));
                gameObject.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
                var enemyAI = gameObject.GetComponent<EnemyAI>();
                RoundManager.Instance.SpawnedEnemies.Add(enemyAI);
                enemyAI.enabled = !StartOfRound.Instance.inShipPhase;

                return;
            }

            LethalMon.Logger.LogInfo("No enemy found..");
        }
        #endregion

        #region Moons
        public static void TeleportOutsideDungeon()
        {
            if (StartOfRound.Instance.inShipPhase) return;

            var entrance = GameObject.Find("EntranceTeleportA")?.GetComponent<EntranceTeleport>();
            if (entrance == null) return;

            Utils.CurrentPlayer.TeleportPlayer(entrance.entrancePoint.position);
        }
        #endregion
#endif
    }
}