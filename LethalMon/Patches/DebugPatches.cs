using HarmonyLib;
using UnityEngine.InputSystem;
using GameNetcodeStuff;
using UnityEngine;
using Unity.Netcode;
using System.Collections;
using LethalMon.Items;
using LethalMon.CustomPasses;
using System;
using System.Linq;
using LethalMon.Save;

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
            if (!Keyboard.current.leftShiftKey.isPressed)
                return;
            
            if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
                ToggleTestRoom();
            }

            else if (Keyboard.current.f2Key.wasPressedThisFrame)
            {
                TeleportOutsideDungeon();
            }

            else if (Keyboard.current.f3Key.wasPressedThisFrame)
            {
            }

            else if (Keyboard.current.f4Key.wasPressedThisFrame)
            {
                LogCollidersInRange(1f);
            }

            else if (Keyboard.current.f5Key.wasPressedThisFrame)
            {
                SpawnEnemyInFrontOfCurrentPlayer(Utils.Enemy.CaveDweller);
            }

            else if (Keyboard.current.f6Key.wasPressedThisFrame)
            {
                SpawnEnemyInFrontOfCurrentPlayer(Utils.Enemy.Crawler, 0.5f);
            }

            else if (Keyboard.current.f7Key.wasPressedThisFrame)
            {
                DebugEnterBuildMode();
            }

            else if (Keyboard.current.f8Key.wasPressedThisFrame)
            {
                SaveManager.DebugUnlockAll();
            }

            else if (Keyboard.current.f9Key.wasPressedThisFrame)
            {
                SpawnItemInFront(Pokeball.SpawnPrefab);
            }

            else if (Keyboard.current.f10Key.wasPressedThisFrame)
            {
                SpawnItemInFront(Greatball.SpawnPrefab);
            }

            else if (Keyboard.current.f11Key.wasPressedThisFrame)
            {
                SpawnItemInFront(Ultraball.SpawnPrefab);
            }

            else if (Keyboard.current.f12Key.wasPressedThisFrame)
            {
                SpawnItemInFront(Masterball.SpawnPrefab);
            }

            else
                return;

            GameNetworkManager.Instance?.StartCoroutine(WaitAfterKeyPress());
        }

        #region Visuals
        public static GameObject CreateCube(ref Color color, Vector3 pos)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = pos;
            cube.transform.localScale = Vector3.one * 0.5f;

            if (cube.TryGetComponent(out BoxCollider boxCollider))
                boxCollider.enabled = false;

            if (cube.TryGetComponent(out MeshRenderer meshRenderer))
            {
                meshRenderer.material = color.a < 1f ? Utils.Glass : new Material(Shader.Find("HDRP/Lit"));
                meshRenderer.material.color = color;
                meshRenderer.enabled = true;
            }

            return cube;
        }
        #endregion

        #region Item
        public static PokeballItem? SpawnRandomlyFilledBall(GameObject? networkPrefab)
        {
            var ball = SpawnItemInFront(networkPrefab);
            if (ball == null) return null;

            if (!ball.TryGetComponent(out PokeballItem pokeballItem))
                return null;

            var enemyName = Data.CatchableMonsters.ElementAt(UnityEngine.Random.RandomRangeInt(0, Data.CatchableMonsters.Count - 1)).Key;
                    pokeballItem.SetCaughtEnemyServerRpc(enemyName);

            return pokeballItem;
        }

        public static PokeballItem? SpawnBall(GameObject? networkPrefab, Utils.Enemy? withEnemyInside = null)
        {
            var ball = SpawnItemInFront(networkPrefab);
            if(ball == null) return null;

            if(!ball.TryGetComponent(out PokeballItem pokeballItem))
                return null;

            if(withEnemyInside != null)
            {
                var enemyName = withEnemyInside.ToString();
                if (!Data.CatchableMonsters.ContainsKey(enemyName))
                    LethalMon.Logger.LogInfo("Spawning ball: Enemy not found.");
                else
                    pokeballItem.SetCaughtEnemyServerRpc(enemyName);
            }

            return pokeballItem;
        }

        public static GameObject? SpawnItemInFront(Item item) => item != null ? SpawnItemInFront(item.spawnPrefab) : null;

        public static GameObject? SpawnItemInFront(GameObject? networkPrefab)
        {
            if (!Utils.IsHost)
            {
                LethalMon.Logger.LogError("That's a host-only debug feature.");
                return null;
            }

            if (networkPrefab == null)
            {
                LethalMon.Logger.LogError("Unable to spawn item. networkPrefab was null.");
                return null;
            }

            var position = Utils.CurrentPlayer.transform.position + Utils.CurrentPlayer.transform.forward * 1.5f;
            var item = Instantiate(networkPrefab, position, Quaternion.identity, RoundManager.Instance.spawnedScrapContainer);
            DontDestroyOnLoad(item);
            item.GetComponent<NetworkObject>()?.Spawn();
            if (item.TryGetComponent(out GrabbableObject grabbableObject))
                grabbableObject.itemProperties.canBeGrabbedBeforeGameStart = true;

            return item;
        }
        #endregion

        #region Enemy

        public static IEnumerator KillEnemyLater(EnemyAI enemyAI, float delay)
        {
            yield return new WaitForSeconds(delay);
            enemyAI.KillEnemyServerRpc(false);
        }

        public static void LogAllEnemyTypes() => Utils.EnemyTypes.ForEach((type) => LethalMon.Log(type.name));

        public static EnemyAI? SpawnEnemyInFrontOfCurrentPlayer(Utils.Enemy enemy, float? killTimer = null) => SpawnEnemyInFrontOfPlayer(Utils.CurrentPlayer, enemy, killTimer);
        public static EnemyAI? SpawnEnemyInFrontOfPlayer(PlayerControllerB targetPlayer, Utils.Enemy enemy, float? killTimer = null)
        {
            var location = targetPlayer.transform.position + targetPlayer.transform.forward * 5f;
            var enemyAI = Utils.SpawnEnemyAtPosition(enemy, location);
            if (enemyAI != null && killTimer != null)
                KillEnemyLater(enemyAI, killTimer.Value);

            return enemyAI;
        }

        /* EXAMPLE
        enemyAI?.StartCoroutine(DoTillDeath(enemyAI, (enemyAI) =>
        {
            if(enemyAI.agent != null)
                enemyAI.agent.speed = 1f;
        }));
         */
        public static IEnumerator DoTillDeath(EnemyAI? enemyAI, Action<EnemyAI> action)
        {
            if (enemyAI == null) yield break;

            yield return new WaitUntil(() =>
            {
                action(enemyAI);
                return enemyAI == null || !enemyAI.gameObject.activeSelf || enemyAI.isEnemyDead;
            });
        }
        #endregion

        #region Player
        public static void LogCollidersInRange(float range)
        {
            Collider[] colliders = Physics.OverlapSphere(Utils.CurrentPlayer.transform.position, range);
            // Log all the colliders and subcomponents
            foreach (var collider in colliders)
            {
                LethalMon.Log(collider.name);
                foreach (var component in collider.GetComponents<Component>())
                {
                    LethalMon.Log("  " + component.GetType().Name);
                    LethalMon.Log("    LOS: " + !Physics.Linecast(Utils.CurrentPlayer.transform.position, component.transform.position, StartOfRound.Instance.collidersAndRoomMaskAndDefault));
                    LethalMon.Log("    Layer: " + LayerMask.LayerToName(component.gameObject.layer));
                }
            }
        }

        public static void DebugEnterBuildMode()
        {
            bool playerMeetsConditionsToBuild = ShipBuildModeManager.Instance.PlayerMeetsConditionsToBuild(Utils.CurrentPlayer);
            bool raycastForward = Physics.Raycast(Utils.CurrentPlayer.gameplayCamera.transform.position, Utils.CurrentPlayer.gameplayCamera.transform.forward, out RaycastHit rayHitForward, 4f, ShipBuildModeManager.Instance.placeableShipObjectsMask, QueryTriggerInteraction.Ignore);
            bool raycastDown = Physics.Raycast(Utils.CurrentPlayer.gameplayCamera.transform.position + Vector3.up * 5f, Vector3.down, out RaycastHit rayHitDown, 5f, ShipBuildModeManager.Instance.placeableShipObjectsMask, QueryTriggerInteraction.Ignore);
            
            LethalMon.Log("Player meets conditions to build: " + playerMeetsConditionsToBuild);
            LethalMon.Log("Raycast forward: " + (raycastForward ? "Hit" : "Miss"));
            LethalMon.Log("Raycast down: " + (raycastDown ? "Hit" : "Miss"));
            LethalMon.Log("Raycast hit forward: " + rayHitForward.collider?.name);
            LethalMon.Log("Raycast hit down: " + rayHitDown.collider?.name);
            if (raycastForward)
                LethalMon.Log("Raycast forward is placeable object: " + rayHitForward.collider?.gameObject.CompareTag("PlaceableObject"));
            if (raycastDown)
                LethalMon.Log("Raycast down is placeable object: " + rayHitDown.collider?.gameObject.CompareTag("PlaceableObject"));
            
            PlaceableShipObject? component = raycastForward ? rayHitForward.collider?.gameObject.GetComponent<PlaceableShipObject>() : raycastDown ? rayHitDown.collider?.gameObject.GetComponent<PlaceableShipObject>() : null;
            LethalMon.Log("Hit component: " + component);
            
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

        #region Interiors
        public static void LockAllDoors()
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
        #endregion

        #region Other
        public static void ToggleCustomPass(CustomPassManager.CustomPassType customPassType)
        {
            CustomPassManager.Instance.EnableCustomPass(customPassType, !CustomPassManager.Instance.IsCustomPassEnabled(customPassType));
        }

        private static int _currentLayerColored = 0;
        
        public static void MakeLayerColor()
        {
            foreach (var go in FindObjectsOfType<GameObject>())
            {
                if (go.layer == _currentLayerColored)
                {
                    if (go.TryGetComponent(out MeshRenderer meshRenderer))
                    {
                        meshRenderer.material.color = Color.white;
                    }
                }
            }

            _currentLayerColored++;
            if (_currentLayerColored == (int) Utils.LayerMasks.Mask.HelmetVisor) // Skip, else it will screw the vision up
            {
                _currentLayerColored++;
            }
            if (_currentLayerColored > 31)
                _currentLayerColored = 0;
            
            LethalMon.Log("Layer: " + LayerMask.LayerToName(_currentLayerColored));

            foreach (var go in FindObjectsOfType<GameObject>())
            {
                if (go.layer == _currentLayerColored)
                {
                    if (go.TryGetComponent(out MeshRenderer meshRenderer))
                    {
                        meshRenderer.material = Utils.Glass;
                        meshRenderer.material.color = Color.red;
                    }
                }
            }
        }

        #endregion
#endif
    }
}
