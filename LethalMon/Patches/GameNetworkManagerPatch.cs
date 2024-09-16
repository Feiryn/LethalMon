using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using LethalMon.Behaviours;
using LethalMon.Items;
using Newtonsoft.Json;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LethalMon.Patches
{
    [HarmonyPatch]
    internal class GameNetworkManagerPatch
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
        
        /*
        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.SaveItemsInShip))]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> SaveItemsInShipTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeInstruction[] instructionArray = instructions.ToArray();
            List<CodeInstruction> instructionList = [];
            
            if (instructionArray.Any(i => i.operand is "shipAdvancedItemSaveData"))
            {
	            LethalMon.Log("SaveItemsInShip code already patched");
	            return instructions;
            }

            int callVirtualIndex = -1;
            int deleteShipItemSaveDataIndex = -1;
            bool insertedDeletion = false;
            bool insertedAdvancedSave = false;
            
            for (var i = 0; i < instructionArray.Length; i++)
            {
                CodeInstruction instruction = instructionArray[i];
                instructionList.Add(instruction);
                
                // Detect when the game deletes saved items and insert our advanced saveable items deletion
                if (instruction.opcode == OpCodes.Ldstr && (string) instruction.operand == "shipItemSaveData")
				{
					deleteShipItemSaveDataIndex = i;
				}
                if (!insertedDeletion && instruction.opcode == OpCodes.Call && instruction.operand.ToString() == "Void DeleteKey(System.String, System.String)" && deleteShipItemSaveDataIndex == i - 3)
				{
					instructionList.Add(new CodeInstruction(OpCodes.Ldstr, "shipAdvancedItemSaveData"));
					instructionList.Add(new CodeInstruction(OpCodes.Ldarg_0));
					instructionList.Add(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(GameNetworkManager), nameof(GameNetworkManager.currentSaveFileName))));
					instructionList.Add(new CodeInstruction(OpCodes.Call, typeof(ES3).GetMethod("DeleteKey", [typeof(string), typeof(string)])));
					insertedDeletion = true;
				}
                
                // Call our custom code that adds advanced saveable items to the list of items to save
                if (instruction.opcode == OpCodes.Callvirt && (MethodInfo) instruction.operand == typeof(GrabbableObject).GetMethod("GetItemDataToSave"))
                {
                   LethalMon.Log("Opcode operand: " + instruction.operand);
                   callVirtualIndex = i;
                }
                if (!insertedAdvancedSave && callVirtualIndex == i - 1 && instruction.opcode == OpCodes.Stloc_S)
                {
                    instructionList.Add(new CodeInstruction(OpCodes.Ldloc_0));
                    instructionList.Add(new CodeInstruction(OpCodes.Ldloc_S, 6));
                    instructionList.Add(new CodeInstruction(OpCodes.Ldelem_Ref));
                    instructionList.Add(new CodeInstruction(OpCodes.Call, typeof(GameNetworkManagerPatch).GetMethod("AdvancedSave", BindingFlags.Static | BindingFlags.Public)));
                    insertedAdvancedSave = true;
                }
            }

            if (insertedDeletion && insertedAdvancedSave)
            {
	            LethalMon.Log("Successfully inserted advanced saveable item deletion and advanced saveable item save code.");
	            return instructionList.AsEnumerable();
            }

            LethalMon.Log("Failed to insert advanced saveable item deletion or advanced saveable item save code.", LethalMon.LogType.Error);
			return instructionArray;

        }
        */
        
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
