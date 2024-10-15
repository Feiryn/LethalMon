using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DunGen;
using GameNetcodeStuff;
using UnityEngine;

namespace LethalMon.Behaviours;

internal class ClaySurgeonTamedBehaviour : TamedEnemyBehaviour
{
    private static GameObject? WallCrackPrefab = null;

    private static List<Tuple<Vector3, Vector3, Vector3>>? WallPositionsRanges = null;
    
    internal static List<Tuple<Vector3, Quaternion>>? WallPositions = null;
    #region Properties

    private ClaySurgeonAI? _claySurgeon = null;

    internal ClaySurgeonAI ClaySurgeon
    {
        get
        {
            if (_claySurgeon == null)
                _claySurgeon = (Enemy as ClaySurgeonAI)!;

            return _claySurgeon;
        }
    }

    public override bool CanDefend => false;
    
    internal GameObject? WallCrackA = null;
    
    internal GameObject? WallCrackB = null;
    #endregion

    #region Cooldowns

    private const string CooldownId = "claysurgeon_cutwall";

    public override Cooldown[] Cooldowns => [new Cooldown(CooldownId, "Cut wall", 1f)];

    private CooldownNetworkBehaviour? cooldown;

    #endregion

    #region Custom behaviours

    internal enum CustomBehaviour
    {
        CutWall = 1
    }

    public override List<Tuple<string, string, Action>>? CustomBehaviourHandler =>
    [
        new(CustomBehaviour.CutWall.ToString(), "Is cutting a wall...", OnCutWall)
    ];

    internal void OnCutWall()
    {
    }

    #endregion

    #region Action Keys

    private readonly List<ActionKey> _actionKeys =
    [
        new ActionKey() { Key = ModConfig.Instance.ActionKey1, Description = "Cut wall" }
    ];

    public override List<ActionKey> ActionKeys => _actionKeys;

    public override void ActionKey1Pressed()
    {
        base.ActionKey1Pressed();

        if (WallCrackA != null || WallCrackB || CurrentCustomBehaviour == (int)CustomBehaviour.CutWall)
            return;
        
        SwitchToCustomBehaviour((int)CustomBehaviour.CutWall);
        StartCoroutine(CutWallCoroutine());
    }

    #endregion

    #region Base Methods

    public override void Start()
    {
        base.Start();

        cooldown = GetCooldownWithId(CooldownId);

        if (IsTamed)
        {
            // InitWallRanges();
            // InitWallPositions();
        }
    }

    public override void InitTamingBehaviour(TamingBehaviour behaviour)
    {
        base.InitTamingBehaviour(behaviour);

        if (behaviour == TamingBehaviour.TamedFollowing)
        {
            EnableActionKeyControlTip(ModConfig.Instance.ActionKey1,
                true); // todo montrer que quand le cooldown est terminé
        }
    }

    public override void LeaveTamingBehaviour(TamingBehaviour behaviour)
    {
        base.LeaveTamingBehaviour(behaviour);

        if (behaviour == TamingBehaviour.TamedFollowing)
        {
            EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, false);
        }
    }

    public override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
    {
        // ANY CLIENT
        base.OnEscapedFromBall(playerWhoThrewBall);
    }

    public override void OnUpdate(bool update = false, bool doAIInterval = true)
    {
        // ANY CLIENT
        base.OnUpdate(update, doAIInterval);
    }

    public override bool CanBeTeleported()
    {
        return CurrentTamingBehaviour == TamingBehaviour.TamedFollowing;
    }

    #endregion

    public override void OnDestroy()
    {
        /*
        if (WallCrack != null)
            Destroy(WallCrack);
            */
        
        base.OnDestroy();
    }

    #region Methods

    private static void InitWallPositions()
    {
        /*
        Tile[] tiles = FindObjectsOfType<Tile>();
        
        WallPositions = new();

        foreach (Tile tile in tiles)
        {
            var doorways = tile.UnusedDoorways;

            foreach (var doorway in doorways)
            {
                LethalMon.Log("Blockers count: " + doorway.BlockerSceneObjects.Count);
                foreach (var blocker in doorway.BlockerSceneObjects)
                {
                    LethalMon.Log("    Blocker: " + blocker.name);
                }
                WallPositions.Add(new Tuple<Vector3, Quaternion>(doorway.transform.position + doorway.transform.up * 1.5f - doorway.transform.forward * 0.05f, Quaternion.Euler(doorway.transform.rotation.x, doorway.transform.rotation.y + 270f, doorway.transform.rotation.z)));
            }
        }
        
        // Log
        foreach (Tuple<Vector3, Quaternion> wallPosition in WallPositions)
        {
            LethalMon.Log($"Wall position: {wallPosition.Item1} (dir: {wallPosition.Item2})");
        }*/
        
        GlobalProp[] globalProps = FindObjectsOfType<GlobalProp>();
        
        WallPositions = new();
        
        foreach (GlobalProp globalProp in globalProps)
        {
            LethalMon.Log("Global prop: " + globalProp.name + ", group: " + globalProp.PropGroupID);
            if (globalProp.PropGroupID == 5) // vent
            {
                var rotation = globalProp.transform.eulerAngles;
                WallPositions.Add(new Tuple<Vector3, Quaternion>(globalProp.transform.position + Vector3.up, Quaternion.Euler(rotation.x - 90f, rotation.y - 90f, rotation.z)));
            }
        }
        
        foreach (Tuple<Vector3, Quaternion> wallPosition in WallPositions)
        {
            LethalMon.Log($"Wall position: {wallPosition.Item1} (dir: {wallPosition.Item2})");
        }
    }

    private static void InitWallRanges()
    {
        Tile[] tiles = FindObjectsOfType<Tile>();

        WallPositionsRanges = new();
        
        foreach (Tile tile in tiles)
        {
            Doorway[] doorways = tile.GetComponentsInChildren<Doorway>();
            SpawnSyncedObject[] spawnSyncedObjects = tile.GetComponentsInChildren<SpawnSyncedObject>();
            
            Vector3 maxTile = tile.Bounds.max;
            Vector3 minTile = tile.Bounds.min;

            // Doorways walls are on x and y, z doesn't move
            foreach (Doorway doorway in doorways)
            {
                Vector3 localSpaceTileMax = doorway.transform.InverseTransformPoint(maxTile);
                Vector3 localSpaceTileMin = doorway.transform.InverseTransformPoint(minTile);
                Vector3 localTilePosition = doorway.transform.InverseTransformPoint(tile.transform.position);
                
                Vector3 min = doorway.transform.TransformPoint(new Vector3(localSpaceTileMin.x + 1, Mathf.Max(localSpaceTileMin.y, localTilePosition.y), 0)); // Don't go under the floor level
                Vector3 max = doorway.transform.TransformPoint(new Vector3(localSpaceTileMax.x - 1, Mathf.Min(localSpaceTileMax.y, localTilePosition.y + 2), 0));
                
                var lineRenderer1 = new GameObject("Line").AddComponent<LineRenderer>();
                lineRenderer1.startColor = Color.red;
                lineRenderer1.endColor = Color.red;
                lineRenderer1.startWidth = 0.01f;
                lineRenderer1.endWidth = 0.01f;
                lineRenderer1.positionCount = 5;
                lineRenderer1.useWorldSpace = true;    
                lineRenderer1.SetPosition(0, new Vector3(min.x, min.y, min.z));
                lineRenderer1.SetPosition(1, new Vector3(max.x, min.y, min.z));
                lineRenderer1.SetPosition(2, new Vector3(max.x, max.y, min.z));
                lineRenderer1.SetPosition(3, new Vector3(min.x, max.y, min.z));
                lineRenderer1.SetPosition(4, new Vector3(min.x, min.y, min.z));
                
                // We need enough space to cut the wall
                if (min.y + 1 > max.y - 1 || min.x + 1 > max.x - 1)
                    continue;
                
                // The direction is on the minus z axis
                Vector3 direction = doorway.transform.TransformDirection(Vector3.back);
                
                WallPositionsRanges.Add(new Tuple<Vector3, Vector3, Vector3>(min, max, direction));
            }
            
            // SpawnSyncedObjects walls are on y and z, x doesn't move
            foreach (SpawnSyncedObject spawnSyncedObject in spawnSyncedObjects)
            {
                Vector3 localSpaceTileMax = spawnSyncedObject.transform.InverseTransformPoint(maxTile);
                Vector3 localSpaceTileMin = spawnSyncedObject.transform.InverseTransformPoint(minTile);
                Vector3 localTilePosition = spawnSyncedObject.transform.InverseTransformPoint(tile.transform.position);
                
                Vector3 min = spawnSyncedObject.transform.TransformPoint(new Vector3(0, Mathf.Max(localSpaceTileMin.y, localTilePosition.y), localSpaceTileMin.z + 1)); // Don't go under the floor level
                Vector3 max = spawnSyncedObject.transform.TransformPoint(new Vector3(0, Mathf.Min(localSpaceTileMax.y, localTilePosition.y + 2), localSpaceTileMax.z - 1));
                
                // We need enough space to cut the wall
                if (min.y + 1 > max.y - 1 || min.z + 1 > max.z - 1)
                    continue;
                
                // The direction is on the minus x axis
                Vector3 direction = spawnSyncedObject.transform.TransformDirection(Vector3.left);
                
                WallPositionsRanges.Add(new Tuple<Vector3, Vector3, Vector3>(min, max, direction));
            }
        }
        
        // Log
        foreach (Tuple<Vector3, Vector3, Vector3> wallPositionRange in WallPositionsRanges)
        {
            LethalMon.Log($"Wall position range: {wallPositionRange.Item1} - {wallPositionRange.Item2} (dir: {wallPositionRange.Item3})");
        }
    }
    
    private IEnumerator CutWallCoroutine()
    {
        /*
        var closestWall = WallPositionsRanges!.OrderBy(wallPositionRange => Vector3.Distance(wallPositionRange.Item1, ClaySurgeon.transform.position)).First();
        var randomWall = WallPositionsRanges!.OrderBy(wallPositionRange => UnityEngine.Random.value).First();
        
        Vector3 closestRandomPosition = new Vector3(UnityEngine.Random.Range(closestWall.Item1.x, randomWall.Item2.x), transform.position.y, UnityEngine.Random.Range(closestWall.Item1.z, randomWall.Item2.z));
        Vector3 randomRandomPosition = new Vector3(UnityEngine.Random.Range(randomWall.Item1.x, randomWall.Item2.x), UnityEngine.Random.Range(randomWall.Item1.y + 2, randomWall.Item2.y - 2), UnityEngine.Random.Range(randomWall.Item1.z, randomWall.Item2.z));
        
        WallCrackA = Instantiate(WallCrackPrefab!, closestRandomPosition, Quaternion.Euler(closestWall.Item3));
        WallCrackB = Instantiate(WallCrackPrefab!, randomRandomPosition, Quaternion.Euler(randomWall.Item3));
        */
        
        var closest = WallPositions!.OrderBy(wallPosition => Vector3.Distance(wallPosition.Item1, ClaySurgeon.transform.position)).First();
        var random = WallPositions!.OrderBy(wallPosition => UnityEngine.Random.value).First();
        
        WallCrackA = Instantiate(WallCrackPrefab!, closest.Item1, closest.Item2);
        WallCrackB = Instantiate(WallCrackPrefab!, random.Item1, random.Item2);
        
        LethalMon.Log("Placed wall cracks at " + WallCrackA.transform.position + " and " + WallCrackB.transform.position);

        var wallCrackAScript = WallCrackA.AddComponent<WallCrack>();
        var wallCrackBScript = WallCrackB.AddComponent<WallCrack>();
        
        wallCrackAScript.otherWallCrack = wallCrackBScript;
        wallCrackBScript.otherWallCrack = wallCrackAScript;
        
        SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
        yield return null;
    }

    internal static void LoadAssets(AssetBundle assetBundle)
    {
        WallCrackPrefab = assetBundle.LoadAsset<GameObject>("Assets/Enemies/Barber/WallCrack.prefab");
    }
    #endregion

    internal class WallCrack : MonoBehaviour
    {
        internal WallCrack? otherWallCrack;
        
        private void OnTriggerEnter(Collider other)
        {
            LethalMon.Log("Wall crack trigger enter");
            
            PlayerControllerB? player = Cache.GetPlayerFromCollider(other);
            if (player != null && player == Utils.CurrentPlayer && otherWallCrack != null)
            {
                player.TeleportPlayer(otherWallCrack.transform.position + otherWallCrack.transform.forward, true, otherWallCrack.transform.eulerAngles.y);
            }
        }
    }
    
    // todo prevent portal behind pipes
}