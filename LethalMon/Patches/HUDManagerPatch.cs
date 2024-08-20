using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LethalMon.Behaviours;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LethalMon.Patches;

public class HUDManagerPatch
{
    private static HUDElement? monsterHudElement;

    private static Image? monsterIcon;

    private static TextMeshProUGUI? monsterName;
    
    private static TextMeshProUGUI? pressKeyTip;
    
    private static TextMeshProUGUI? monsterAction;
    
    private static Image? cooldownCircle1;
    
    private static TextMeshProUGUI? cooldownTime1;
    
    private static TextMeshProUGUI? cooldownName1;
    
    private static Image? cooldownCircle2;
    
    private static TextMeshProUGUI? cooldownTime2;
    
    private static TextMeshProUGUI? cooldownName2;

    // todo ping HUD
    
    public static void UpdatePressKeyTip()
    {
        if (pressKeyTip != null)
        {
            pressKeyTip.text = "[" + ModConfig.Instance.RetrieveBallKey.GetBindingDisplayString() + "]\nto retrieve";
        }
    }
    
    public static void ChangeToTamedBehaviour(TamedEnemyBehaviour behaviour)
    {
        string enemyTypeName = behaviour.Enemy.enemyType.name;

        if (monsterIcon != null) 
            monsterIcon.sprite = LethalMon.monstersSprites.TryGetValue(enemyTypeName.ToLower(), out var sprite) ? sprite : LethalMon.monstersSprites["unknown"];

        if (monsterName != null)
            monsterName.text = Data.CatchableMonsters[enemyTypeName].DisplayName;
        
        // Bind cooldowns
        CooldownNetworkBehaviour[] cooldowns = behaviour.GetComponents<CooldownNetworkBehaviour>();
        if (cooldownCircle1 != null && cooldownTime1 != null && cooldownName1 != null)
        {
            if (cooldowns.Length >= 1)
            {
                cooldownCircle1.gameObject.SetActive(true);
                cooldownTime1.gameObject.SetActive(true);
                cooldownName1.gameObject.SetActive(true);
                cooldowns[0].BindToHUD(cooldownCircle1, cooldownTime1, cooldownName1);
            }
            else
            {
                cooldownCircle1.gameObject.SetActive(false);
                cooldownTime1.gameObject.SetActive(false);
                cooldownName1.gameObject.SetActive(false);
            }
        }

        if (cooldownCircle2 != null && cooldownTime2 != null && cooldownName2 != null)
        {
            if (cooldowns.Length >= 2)
            {
                cooldownCircle2.gameObject.SetActive(true);
                cooldownTime2.gameObject.SetActive(true);
                cooldownName2.gameObject.SetActive(true);
                cooldowns[1].BindToHUD(cooldownCircle2, cooldownTime2, cooldownName2);
            }
            else
            {
                cooldownCircle2.gameObject.SetActive(false);
                cooldownTime2.gameObject.SetActive(false);
                cooldownName2.gameObject.SetActive(false);
            }
        }
    }

    public static void EnableHUD(bool enable)
    {
        if(monsterHudElement != null && monsterHudElement.canvasGroup != null)
            monsterHudElement.canvasGroup.gameObject?.SetActive(enable);
    }

    public static void UpdateTamedMonsterAction(string action)
    {
        if(monsterAction != null)
            monsterAction.text = action;
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(HUDManager), nameof(HUDManager.Awake))]
    static void AwakePostfix(HUDManager __instance)
    {
        if(LethalMon.hudPrefab == null)
        {
            LethalMon.Log("Unable to instantiate HUD, prefab not loaded", LethalMon.LogType.Error);
            return;
        }

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
        
        GameObject monsterHud = Object.Instantiate(LethalMon.hudPrefab, topRightCorner.canvasGroup.transform.parent);
        monsterHudElement = new HUDElement
        {
            canvasGroup = monsterHud.GetComponent<CanvasGroup>(),
            targetAlpha = topRightCorner.targetAlpha,
            fadeCoroutine = topRightCorner.fadeCoroutine
        };
        
        // Init values
        Component[] components = monsterHud.GetComponentsInChildren<Component>();
        monsterIcon = components.First(c => c.name == "Icon").GetComponent<Image>();
        monsterName = components.First(c => c.name == "Name").GetComponent<TextMeshProUGUI>();
        pressKeyTip = components.First(c => c.name == "PressKeyTip").GetComponent<TextMeshProUGUI>();
        monsterAction = components.First(c => c.name == "Action").GetComponent<TextMeshProUGUI>();
        cooldownCircle1 = components.First(c => c.name == "CooldownCircle1").GetComponent<Image>();
        cooldownName1 = components.First(c => c.name == "CooldownName1").GetComponent<TextMeshProUGUI>();
        cooldownTime1 = components.First(c => c.name == "CooldownTime1").GetComponent<TextMeshProUGUI>();
        cooldownCircle2 = components.First(c => c.name == "CooldownCircle2").GetComponent<Image>();
        cooldownName2 = components.First(c => c.name == "CooldownName2").GetComponent<TextMeshProUGUI>();
        cooldownTime2 = components.First(c => c.name == "CooldownTime2").GetComponent<TextMeshProUGUI>();
        UpdatePressKeyTip();
        EnableHUD(false);
        
        hudElementsFieldInfo.SetValue(__instance, new List<HUDElement>(elements) { monsterHudElement }.ToArray());

        topRightCorner.canvasGroup.transform.localPosition -= new Vector3(0, monsterHudElement.canvasGroup.GetComponent<RectTransform>().rect.height, 0);
        
        LethalMon.Log("HUD initialized");
    }
}