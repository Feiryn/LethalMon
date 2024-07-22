using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GameNetcodeStuff;
using LethalMon.Behaviours;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;
using UnityEngine.AI;
using LethalLib.Modules;
using Unity.Services.Authentication;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace LethalMon;

public class Utils
{
    public static readonly Random Random = new Random();
    
    public static TamedEnemyBehaviour? GetPlayerPet(PlayerControllerB player)
    {
        return GameObject.FindObjectsOfType<TamedEnemyBehaviour>().FirstOrDefault(tamedBehaviour => tamedBehaviour.ownerPlayer == player);
    }

    public static Vector3 GetPositionInFrontOfPlayerEyes(PlayerControllerB player)
    {
        return player.playerEye.position + player.playerEye.forward * 2.5f;
    }
    
    public static Vector3 GetPositionBehindPlayer(PlayerControllerB player)
    {
        return player.transform.position + player.transform.forward * -2.0f;
    }

    public static EnemyAI? GetMostProbableAttackerEnemy(PlayerControllerB player, StackTrace stackTrace)
    {
        StackFrame[] stackFrames = stackTrace.GetFrames();

        foreach (StackFrame stackFrame in stackFrames[1..])
        {
            Type classType = stackFrame.GetMethod().DeclaringType;
            LethalMon.Log("Stackframe type: " + classType);

            if (classType.IsSubclassOf(typeof(EnemyAI)))
            {
                LethalMon.Log("Class is assignable from EnemyAI");
                EnemyAI? closestEnemy = null;
                float? closestEnemyDistance = float.MaxValue;
                
                Collider[] colliders = Physics.OverlapSphere(player.transform.position, 10f);
                foreach (Collider collider in colliders)
                {
                    EnemyAI? enemyAI = collider.GetComponentInParent<EnemyAI>();
                    if (enemyAI != null && enemyAI.GetType() == classType)
                    {
                        float distance = Vector3.Distance(player.transform.position, enemyAI.transform.position);
                        if (closestEnemyDistance > distance)
                        {
                            closestEnemy = enemyAI;
                            closestEnemyDistance = distance;
                        }
                    }
                }

                if (closestEnemy != null)
                {
                    return closestEnemy;
                }
            }
        }
        
        return null;
    }
    
    private static Dictionary<string, TerminalNode> infoNodes = new Dictionary<string, TerminalNode>();
    
    public static TerminalNode CreateTerminalNode(string name, string description)
    {
        if (infoNodes.TryGetValue(name, out var terminalNode)) return terminalNode;
        
        TerminalNode node = ScriptableObject.CreateInstance<TerminalNode>();
        Object.DontDestroyOnLoad(node);
        node.clearPreviousText = true;
        node.name = name + "InfoNode";
        node.displayText = description + "\n\n";
        infoNodes.Add(name, node);
        return node;
    }
    
    public static Vector3 GetRandomNavMeshPositionOnRadius(Vector3 pos, float radius, NavMeshHit navHit = default(NavMeshHit))
    {
            float y = pos.y;
            pos = UnityEngine.Random.onUnitSphere * radius + pos;
            pos.y = y;
            if (NavMesh.SamplePosition(pos, out navHit, radius, -1))
                return navHit.position;

            return pos;
    }

    public static void PlaySoundAtPosition(Vector3 position, AudioClip clip, float volume = 1f)
    {
        var audioSource = SoundManager.Instance.tempAudio1.isPlaying ? SoundManager.Instance.tempAudio2 : SoundManager.Instance.tempAudio1;
        audioSource.transform.position = position;
        audioSource.PlayOneShot(clip, volume);
    }

    public static void PlaySoundAtPosition(Vector3 position, AudioClip clip)
    {
        SoundManager.Instance.tempAudio1.transform.position = position;
        SoundManager.Instance.tempAudio1.PlayOneShot(clip);
    }
    
    public static void EnableShotgunHeldByEnemyAi(EnemyAI enemyAI, bool enable)
    {
        ShotgunItem[] shotguns = GameObject.FindObjectsOfType<ShotgunItem>();
        foreach (ShotgunItem shotgunItem in shotguns)
        {
            if (shotgunItem.heldByEnemy == enemyAI)
            {
                shotgunItem.EnableItemMeshes(enable);
            }
        }
    }
    
    public static void DestroyShotgunHeldByEnemyAi(EnemyAI enemyAI)
    {
        ShotgunItem[] shotguns = GameObject.FindObjectsOfType<ShotgunItem>();
        foreach (ShotgunItem shotgunItem in shotguns)
        {
            if (shotgunItem.heldByEnemy == enemyAI)
            {
                shotgunItem.GetComponent<NetworkObject>().Despawn(true);
            }
        }
    }

    #region Player
    public static List<PlayerControllerB>? AllPlayers => StartOfRound.Instance?.allPlayerScripts?.Where(pcb => pcb != null && (pcb.isPlayerControlled || pcb.isPlayerDead)).ToList();
    public static List<PlayerControllerB>? AlivePlayers => AllPlayers?.Where(pcb => !pcb.isPlayerDead).ToList();

    public static PlayerControllerB CurrentPlayer => GameNetworkManager.Instance.localPlayerController;

    public static ulong? CurrentPlayerID => CurrentPlayer?.playerClientId;

    public static bool IsHost
    {
        get
        {
            if (NetworkManager.Singleton != null)
                return NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer;

            if (CurrentPlayerID.HasValue)
                return CurrentPlayerID.Value == 0ul;

            return false;
        }
    }

    public static readonly float DefaultJumpForce = 13f;
    public static float DefaultPlayerSpeed => CurrentPlayer.isSprinting ? 2.25f : 1f;
    #endregion

    #region Enemy
    public static List<EnemyType> EnemyTypes => Resources.FindObjectsOfTypeAll<EnemyType>().ToList();

    public static void OpenDoorsAsEnemyAroundPosition(Vector3 position)
    {
        Collider[] colliders = Physics.OverlapSphere(position, 0.5f);
        foreach (Collider collider in colliders)
        {
            DoorLock doorLock = collider.GetComponentInParent<DoorLock>();
            if (doorLock != null && !doorLock.isDoorOpened && !doorLock.isLocked)
            {
                LethalMon.Log("Door opened at " + position);
                if (doorLock.gameObject.TryGetComponent(out AnimatedObjectTrigger trigger))
                {
                    trigger.TriggerAnimationNonPlayer(false, true, false);
                }

                doorLock.OpenDoorAsEnemyServerRpc();
            }
        }
    }

    public enum Enemy // EnemyType.name
    {
        BaboonHawk,
        Blob,
        BushWolf,
        Butler,
        ButlerBees,
        Centipede,
        ClaySurgeon,
        Crawler,
        DocileLocustBees,
        Doublewing,
        FlowerSnake,
        RedLocustBees,
        DressGirl,
        Flowerman,
        ForestGiant,
        HoarderBug,
        Jester,
        LassoMan,
        MaskedPlayerEnemy,
        MouthDog,
        Nutcracker,
        Puffer,
        RadMech,
        RedPillEnemyType,
        SandSpider,
        SandWorm,
        SpringMan
    }

    public static Bounds? RealEnemyBounds(EnemyAI enemy)
    {
        if (enemy == null) return null;

        var renderers = enemy.gameObject.GetComponentsInChildren<Renderer>();
        if (renderers == null) return null;

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; ++i)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }
    #endregion

    #region LayerMasks
    internal class LayerMasks
    {
        /* for (int i = 0; i <= 29; i++)
            {
                if(LayerMask.LayerToName(i) != "")
                    log(LayerMask.LayerToName(i) + " = " + i + ",");
            } */
        internal enum Mask
        {
            All = ~0,
            Default = 0,
            TransparentFX = 1,
            Ignore_Raycast = 2,
            Player = 3,
            Water = 4,
            UI = 5,
            Props = 6,
            HelmetVisor = 7,
            Room = 8,
            InteractableObject = 9,
            Foliage = 10,
            Colliders = 11,
            PhysicsObject = 12,
            Triggers = 13,
            MapRadar = 14,
            NavigationSurface = 15,
            RoomLight = 16,
            Anomaly = 17,
            LineOfSight = 18,
            Enemies = 19,
            PlayerRagdoll = 20,
            MapHazards = 21,
            ScanNode = 22,
            EnemiesNotRendered = 23,
            MiscLevelGeometry = 24,
            Terrain = 25,
            PlaceableShipObjects = 26,
            PlacementBlocker = 27,
            Railing = 28,
            DecalStickableSurface = 29,
            CompanyCruiser = 30,
        }

        internal static int ToInt(Mask[] masks)
        {
            int bitcode = 0;
            foreach (Mask mask in masks)
                bitcode |= (1 << (int)mask);
            return bitcode;
        }
    }
    #endregion

    #region Items
    public enum VanillaItem
    {
        Custom,
        Binoculars,
        Boombox,
        clipboard,
        Homemade_flashbang,
        Extension_ladder,
        Flashlight,
        Ammo,
        Jetpack,
        Key,
        Lockpicker,
        Apparatus,
        Mapper,
        Pro_flashlight,
        Radar_booster,
        Body,
        Magic_7_ball,
        Airhorn,
        Bell,
        Big_bolt,
        Bottles,
        Brush,
        Candy,
        Cash_register,
        Chemical_jug,
        Clown_horn,
        Large_axle,
        Comedy,
        Teeth,
        Dust_pan,
        Easter_egg,
        Egg_beater,
        V_type_engine,
        Golden_cup,
        Fancy_lamp,
        Painting,
        Plastic_fish,
        Laser_pointer,
        Flask,
        Gift,
        Gold_bar,
        Hairdryer,
        Kitchen_knife,
        Magnifying_glass,
        Metal_sheet,
        Cookie_mold_pan,
        Mug,
        Perfume_bottle,
        Old_phone,
        Jar_of_pickles,
        Pill_bottle,
        Hive,
        Remote,
        Ring,
        Toy_robot,
        Rubber_Ducky,
        Red_soda,
        Steering_wheel,
        Stop_sign,
        Tea_kettle,
        Toothpaste,
        Toy_cube,
        Tragedy,
        Whoopie_cushion,
        Yield_sign,
        Shotgun,
        Shovel,
        Spray_paint,
        Sticky_note,
        Stun_grenade,
        TZP_Inhalant,
        Walkie_talkie,
        Zap_gun
    }

    public static VanillaItem itemTypeByName(string name)
    {
        if (name == null || name.Length == 0) return VanillaItem.Custom;

        name = name.Replace("-", "_");
        name = name.Replace(" ", "_");

        if (Enum.TryParse(typeof(VanillaItem), name, true, out object item))
            return (VanillaItem)item;
        return VanillaItem.Custom;
    }

    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Start))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void LoadItemsPrefix() => SpawnableItems = Resources.FindObjectsOfTypeAll<Item>().ToList();
    public static List<Item> SpawnableItems = new();
    public static Item itemByName(string name) => SpawnableItems.Find(x => x.itemName == name || x.itemName.Replace("-", "_") == name || x.itemName.Replace(" ", "_") == name);
    public static Item itemByType(VanillaItem type) => itemByName(type.ToString());

    public static bool TrySpawnItemAtPosition(VanillaItem itemType, Vector3 position, out GrabbableObject? item) => TrySpawnItemAtPosition(itemByType(itemType).spawnPrefab, position, out item);
    private static bool TrySpawnItemAtPosition(GameObject spawnPrefab, Vector3 position, out GrabbableObject? item)
    {
        if (!IsHost || spawnPrefab == null)
        {
            LethalMon.Log("TrySpawnItemAtPosition: Not a valid item or not the host.");
            item = null;
            return false;
        }

        LethalMon.Log("Instantiating item " + spawnPrefab.name);
        var itemObj = Object.Instantiate(spawnPrefab, position, Quaternion.identity, StartOfRound.Instance.elevatorTransform);
        itemObj.GetComponent<NetworkObject>()?.Spawn();
        return itemObj.TryGetComponent(out item);
    }

    public static bool TrySpawnRandomItemAtPosition(Vector3 position, out GrabbableObject? item)
    {
        LethalMon.Log("Spawnable items: " + SpawnableItems.Count);
        return TrySpawnItemAtPosition(SpawnableItems[UnityEngine.Random.Range(0, SpawnableItems.Count - 1)].spawnPrefab, position, out item);
    }
    #endregion
}