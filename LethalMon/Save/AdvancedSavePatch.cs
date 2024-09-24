using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;

namespace LethalMon.Save;

internal class AdvancedSavePatch
{
    private const string ES3AdvancedItemSaveDataKey = "shipAdvancedItemSaveData";
    
    public static List<object?> advancedItemSaveData;
    
    private string currentSaveFileName;
    
    private static int loadIndex;

    private static object?[] shipAdvancedItemSaveData;
    
    public static void AdvancedSave(GrabbableObject grabbableObject)
    {
        if (grabbableObject is IAdvancedSaveableItem advancedSaveableItem)
        {
	        try
	        {
		        advancedItemSaveData.Add(advancedSaveableItem.GetAdvancedItemDataToSave());
		        LethalMon.Log("Advanced saveable item data saved.");
	        }
	        catch (Exception e)
	        {
		        advancedItemSaveData.Add(null);
		        LethalMon.Log("Advanced saveable item data not saved. An error occurred while serializing the data: " + e);
	        }
        }
        else
        {
	        advancedItemSaveData.Add(null);
        }
    }
    
    public static void AdvancedLoad(GrabbableObject grabbableObject)
	{
        if (grabbableObject is IAdvancedSaveableItem advancedSaveableItem && loadIndex < shipAdvancedItemSaveData.Length && shipAdvancedItemSaveData[loadIndex] != null)
        {
	        try
	        {
		        advancedSaveableItem.LoadAdvancedItemData(shipAdvancedItemSaveData[loadIndex]!);
		        LethalMon.Log("Advanced saveable item data loaded: " + JsonConvert.SerializeObject(shipAdvancedItemSaveData[loadIndex]));
	        }
	        catch (Exception e)
	        {
		        LethalMon.Log("Advanced saveable item data not loaded. An error occurred while deserializing the data: " + e);
	        }
        }
	}
    
    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.SaveItemsInShip))]
    [HarmonyPrefix]
    private static void SaveItemsInShipPrefix(GameNetworkManager __instance)
    {
        advancedItemSaveData = [];
        ES3.DeleteKey(ES3AdvancedItemSaveDataKey, __instance.currentSaveFileName);
    }
    
    [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.SaveItemsInShip))]
    [HarmonyPostfix]
    private static void SaveItemsInShipPostfix(GameNetworkManager __instance)
	{
		LethalMon.Log("Saving advanced saveable items...");
		if (advancedItemSaveData.Count > 0)
		{
			ES3.Save(ES3AdvancedItemSaveDataKey, advancedItemSaveData.ToArray(), __instance.currentSaveFileName);
			LethalMon.Log("Saved advanced saveable items: " + JsonConvert.SerializeObject(advancedItemSaveData));
		}
		else
		{
			ES3.DeleteKey(ES3AdvancedItemSaveDataKey, __instance.currentSaveFileName);
			LethalMon.Log("No advanced saveable items to save.");
		}
	}
    
    [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.GetItemDataToSave))]
    [HarmonyPostfix]
    private static void GetItemDataToSavePostfix(GrabbableObject __instance)
	{
        AdvancedSave(__instance);
	}

	[HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.LoadItemSaveData))]
	[HarmonyPostfix]
	private static void LoadItemSaveDataPostfix(GrabbableObject __instance)
	{
		if ((new StackTrace().GetFrames() ?? []).Any(f => f.GetMethod().Name.Contains("LoadShipGrabbableItems")))
		{
			AdvancedLoad(__instance);
			loadIndex++;
		}
	}

	[HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.LoadShipGrabbableItems))]
	[HarmonyPrefix]
	private static void LoadShipGrabbableItemsPrefix(StartOfRound __instance)
	{
		loadIndex = 0;
		if (ES3.KeyExists(ES3AdvancedItemSaveDataKey,
			    GameNetworkManager.Instance.currentSaveFileName))
		{
			shipAdvancedItemSaveData = ES3.Load<object?[]>(ES3AdvancedItemSaveDataKey, GameNetworkManager.Instance.currentSaveFileName);
		}
	}
}
