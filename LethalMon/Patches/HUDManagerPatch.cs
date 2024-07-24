using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LethalMon.Patches;

public class HUDManagerPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(HUDManager), nameof(HUDManager.Awake))]
    static void AwakePostfix(HUDManager __instance)
    {
        FieldInfo? hudElementsFieldInfo =
            typeof(HUDManager).GetField("HUDElements", BindingFlags.NonPublic | BindingFlags.Instance);

        if (hudElementsFieldInfo == null)
        {
            LethalMon.Log("Unable to instantiate HUD, HUDElements array not found", LethalMon.LogType.Error);
            return;
        }
        
        HUDElement[] elements = (HUDElement[]) hudElementsFieldInfo.GetValue(__instance);
        
        if (elements.Length == 0)
        {
            LethalMon.Log("NO HUD element found (probably because of another mod), cannot instantiate LethalMon HUD", LethalMon.LogType.Error);
            return;
        }

        HUDElement? topRightCorner = elements.FirstOrDefault(e => e.canvasGroup.GetComponents<Component>().Any(c => c.name == "TopRightCorner"));
        
        if (topRightCorner == null)
        {
            LethalMon.Log("Top left corner HUDElement not found, cannot instantiate LethalMon HUD", LethalMon.LogType.Error);
            return;
        }
        
        LethalMon.Log(string.Join("\n", elements.Select(e => e.fadeCoroutine)));
        
        GameObject monsterHud = Object.Instantiate(LethalMon.hudPrefab, topRightCorner.canvasGroup.transform.parent);
        HUDElement hudElement = new HUDElement
        {
            canvasGroup = monsterHud.GetComponent<CanvasGroup>(),
            targetAlpha = topRightCorner.targetAlpha,
            fadeCoroutine = topRightCorner.fadeCoroutine
        };
        
        hudElementsFieldInfo.SetValue(__instance, new List<HUDElement>(elements) { hudElement }.ToArray());

        topRightCorner.canvasGroup.transform.localPosition -= new Vector3(0, hudElement.canvasGroup.GetComponent<RectTransform>().rect.height, 0);
        
        LethalMon.Log("HUD initialized");
    }
}