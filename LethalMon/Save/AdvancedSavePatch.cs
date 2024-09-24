using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using LethalMon.Behaviours;
using LethalMon.Items;
using Newtonsoft.Json;

namespace LethalMon.Patches
{
    [HarmonyPatch]
    internal class AdvancedSavePatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.Start))]
        static void StartPostfix()
        {
            BlobTamedBehaviour.AddPhysicsSectionToPrefab();
        }
        
        public static List<object?> advancedItemSaveData;
        
        private string currentSaveFileName;
        
        public static void AdvancedSave(GrabbableObject grabbableObject)
        {
	        LethalMon.Log("Saving advanced saveable item data...");
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
		        LethalMon.Log("Advanced saveable item data not saved. Item does not implement IAdvancedSaveableItem.");
	        }
        }
        
        public static void AdvancedLoad(GrabbableObject grabbableObject)
		{
	        LethalMon.Log("Loading advanced saveable item data...");
	        if (grabbableObject is IAdvancedSaveableItem advancedSaveableItem && shipAdvancedItemSaveData[loadIndex] != null)
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
	        else
	        {
		        LethalMon.Log("Advanced saveable item data not loaded. Item does not implement IAdvancedSaveableItem.");
	        }
		}
        
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.SaveItemsInShip))]
        [HarmonyPrefix]
        private static void SaveItemsInShipPrefix(GameNetworkManager __instance)
        {
	        advancedItemSaveData = [];
	        ES3.DeleteKey("shipItemSaveData", __instance.currentSaveFileName);
        }
        
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.SaveItemsInShip))]
        [HarmonyPostfix]
        private static void SaveItemsInShipPostfix(GameNetworkManager __instance)
		{
			LethalMon.Log("Saving advanced saveable items...");
			if (advancedItemSaveData.Count > 0)
			{
				ES3.Save("shipAdvancedItemSaveData", advancedItemSaveData.ToArray(), __instance.currentSaveFileName);
				LethalMon.Log("Saved advanced saveable items: " + JsonConvert.SerializeObject(advancedItemSaveData));
			}
			else
			{
				ES3.DeleteKey("shipAdvancedItemSaveData", __instance.currentSaveFileName);
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

		private static int loadIndex;

		private static object?[] shipAdvancedItemSaveData;

		[HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.LoadShipGrabbableItems))]
		[HarmonyPrefix]
		private static void LoadShipGrabbableItemsPrefix()
		{
			loadIndex = 0;
			shipAdvancedItemSaveData = ES3.Load<object?[]>("shipAdvancedItemSaveData", GameNetworkManager.Instance.currentSaveFileName);
		}
    }
}
