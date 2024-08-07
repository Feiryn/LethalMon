﻿using HarmonyLib;
using UnityEngine.InputSystem;
using GameNetcodeStuff;
using UnityEngine;
using Unity.Netcode;
using System.Collections;
using LethalMon.Items;
using UnityEngine.Rendering.HighDefinition;
using LethalMon.CustomPasses;

using static LethalMon.CustomPasses.CustomPassManager.CustomPassType;

namespace LethalMon.Patches
{
    [HarmonyPatch]
    internal class DebugPatches : NetworkBehaviour
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
            if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
                ToggleTestRoom();
            }

            else if (Keyboard.current.f2Key.wasPressedThisFrame)
            {
                CustomPassManager.Instance.EnableCustomPass(SeeThroughEnemies, !CustomPassManager.Instance.IsCustomPassEnabled(SeeThroughEnemies));
            }

            else if (Keyboard.current.f3Key.wasPressedThisFrame)
            {
                CustomPassManager.Instance.EnableCustomPass(SeeThroughItems, !CustomPassManager.Instance.IsCustomPassEnabled(SeeThroughItems));
            }

            else if (Keyboard.current.f4Key.wasPressedThisFrame)
            {
                TeleportOutsideDungeon();
            }

            else if (Keyboard.current.f5Key.wasPressedThisFrame)
            {
                LethalMon.Log("-------------------");
                Collider[] colliders = Physics.OverlapSphere(Utils.CurrentPlayer.transform.position, 3f);
                // Log all the colliders and sub components
                foreach (var collider in colliders)
                {
                    LethalMon.Log(collider.name);
                    foreach (var component in collider.GetComponents<Component>())
                    {
                        LethalMon.Log("  " + component.GetType().Name + " (los: " + !Physics.Linecast(Utils.CurrentPlayer.transform.position, component.transform.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault) + ")");
                    }
                }
            }

            else if (Keyboard.current.f6Key.wasPressedThisFrame)
            {
                var enemy = SpawnEnemyInFrontOfPlayer(Utils.CurrentPlayer, Utils.Enemy.Crawler.ToString());
                GameNetworkManager.Instance.StartCoroutine(KillEnemyLater(enemy!));
            }

            else if (Keyboard.current.f7Key.wasPressedThisFrame)
            {
                SpawnEnemyInFrontOfPlayer(Utils.CurrentPlayer, Utils.Enemy.RedLocustBees.ToString());
            }

            else if (Keyboard.current.f8Key.wasPressedThisFrame)
            {
                foreach (var bigDoor in FindObjectsOfType<TerminalAccessibleObject>())
                {
                    if (bigDoor.isBigDoor && bigDoor.isDoorOpen)
                    {
                        bigDoor.SetDoorOpenServerRpc(false);
                    }
                }
                
                foreach (var smallDoor in FindObjectsOfType<DoorLock>())
                {
                    if (!smallDoor.isLocked)
                    {
                        smallDoor.LockDoor();
                    }
                }
            }

            else if (Keyboard.current.f9Key.wasPressedThisFrame)
            {
                if (Pokeball.SpawnPrefab != null)
                    SpawnItemInFront(Pokeball.SpawnPrefab);
            }

            else if (Keyboard.current.f10Key.wasPressedThisFrame)
            {
                if (Greatball.SpawnPrefab != null)
                    SpawnItemInFront(Greatball.SpawnPrefab);
            }

            else if (Keyboard.current.f11Key.wasPressedThisFrame)
            {
                if (Ultraball.SpawnPrefab != null)
                    SpawnItemInFront(Ultraball.SpawnPrefab);
            }

            else if (Keyboard.current.f12Key.wasPressedThisFrame)
            {
                if (Masterball.SpawnPrefab != null)
                    SpawnItemInFront(Masterball.SpawnPrefab);
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

        public static IEnumerator KillEnemyLater(EnemyAI enemyAI)
        {
            yield return new WaitForSeconds(0.5f);
            enemyAI.KillEnemyServerRpc(false);
        }

        public static EnemyAI? SpawnEnemyInFrontOfPlayer(PlayerControllerB targetPlayer, string enemyName)
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
                enemyAI.enabled = StartOfRound.Instance.testRoom == null;
                enemyAI.SetEnemyOutside(StartOfRound.Instance.testRoom != null || !Utils.CurrentPlayer.isInsideFactory);

                return enemyAI;
            }

            LethalMon.Logger.LogInfo("No enemy found..");
            return null;
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

        public static void ToggleTestRoom()
        {
            if (StartOfRound.Instance.testRoom == null)
            {
                StartOfRound.Instance.testRoom = Instantiate(StartOfRound.Instance.testRoomPrefab, StartOfRound.Instance.testRoomSpawnPosition.position, StartOfRound.Instance.testRoomSpawnPosition.rotation, StartOfRound.Instance.testRoomSpawnPosition);
                if (Utils.IsHost)
                    StartOfRound.Instance.testRoom.GetComponent<NetworkObject>().Spawn();
                Utils.CurrentPlayer.TeleportPlayer(StartOfRound.Instance.testRoomSpawnPosition.position);
            }
            else
            {
                if (Utils.IsHost)
                    StartOfRound.Instance.testRoom.GetComponent<NetworkObject>().Despawn();
                Destroy(StartOfRound.Instance.testRoom);
                Utils.CurrentPlayer.TeleportPlayer(Vector3.zero);
            }
        }
        #endregion
#endif
        }
}
