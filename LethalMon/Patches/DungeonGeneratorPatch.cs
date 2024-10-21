using System;
using System.Linq;
using DunGen;
using HarmonyLib;
using LethalMon.Behaviours;
using UnityEngine;

namespace LethalMon.Patches;

public class DungeonGeneratorPatch
{
    [HarmonyPatch(typeof(DungeonGenerator), nameof(DungeonGenerator.ProcessGlobalProps))]
    [HarmonyPrefix]
    private static void ProcessGlobalPropsPrefix(DungeonGenerator __instance)
    {
        try
        {
            ClaySurgeonTamedBehaviour.WallPositions = new();

            foreach (Tile allTile in __instance.CurrentDungeon.AllTiles)
            {
                GlobalProp[] globalProps = allTile.GetComponentsInChildren<GlobalProp>();

                foreach (GlobalProp globalProp in globalProps)
                {
                    if (globalProp.PropGroupID == 5) // vent
                    {
                        var rotation = globalProp.transform.eulerAngles;
                        ClaySurgeonTamedBehaviour.WallPositions.Add(new Tuple<Vector3, Quaternion>(
                            globalProp.transform.position + Vector3.up + globalProp.transform.forward * 0.02f,
                            Quaternion.Euler(rotation.x - 90f, rotation.y - 90f, rotation.z)));
                    }
                }
            }
        }
        catch (Exception e)
        {
            LethalMon.Log($"Error in ProcessGlobalPropsPrefix: {e.Message}", LethalMon.LogType.Error);
        }
    }

    [HarmonyPatch(typeof(DungeonGenerator), nameof(DungeonGenerator.ProcessGlobalProps))]
    [HarmonyPostfix]
    private static void ProcessGlobalPropsPostfix(DungeonGenerator __instance)
    {
        // Remove used vents
        
        try
        {
            foreach (Tile allTile in __instance.CurrentDungeon.AllTiles)
            {
                GlobalProp[] globalProps = allTile.GetComponentsInChildren<GlobalProp>();

                foreach (GlobalProp globalProp in globalProps)
                {
                    if (globalProp.PropGroupID == 5) // vent
                    {
                        ClaySurgeonTamedBehaviour.WallPositions = ClaySurgeonTamedBehaviour.WallPositions!.Where(wp => !Mathf.Approximately(wp.Item1.x, globalProp.transform.position.x) && !Mathf.Approximately(wp.Item1.z, globalProp.transform.position.z)).ToList();
                    }
                }
            }
        }
        catch (Exception e)
        {
            LethalMon.Log($"Error in ProcessGlobalPropsPostfix: {e.Message}", LethalMon.LogType.Error);
        }
    }
}