using System;
using DunGen;
using HarmonyLib;
using LethalMon.Behaviours;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LethalMon.Patches;

public class DungeonGeneratorPatch
{
    [HarmonyPatch(typeof(DungeonGenerator), nameof(DungeonGenerator.ProcessGlobalProps))]
    [HarmonyPrefix]
    private static void ProcessGlobalPropsPostfix(DungeonGenerator __instance)
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
            LethalMon.Log($"Error in ProcessGlobalPropsPostfix: {e.Message}", LethalMon.LogType.Error);
        }
    }
}