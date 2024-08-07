using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using LethalMon.Behaviours;
using LethalMon.Items;
using LethalMon.Patches;
using UnityEngine;

namespace LethalMon;

[BepInDependency("com.rune580.LethalCompanyInputUtils", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("atomic.terminalapi", BepInDependency.DependencyFlags.SoftDependency)]
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class LethalMon : BaseUnityPlugin
{
    public static LethalMon Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }

    internal static AudioClip HoardingBugFlySfx;

    internal static GameObject hudPrefab;

    internal static Dictionary<string, Sprite> monstersSprites = new();

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        ModConfig.Instance.Setup();
        LoadAssetBundle();
        NetcodePatching();
        ApplyHarmonyPatches();

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
    }

    private void NetcodePatching()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes();
        foreach (var type in types)
        {
            var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var method in methods)
            {
                var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                if (attributes.Length > 0)
                {
                    method.Invoke(null, null);
                }
            }
        }
    }

    private void LoadAssetBundle()
    {
        AssetBundle assetBundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lethalmon"));

        Pokeball.Setup(assetBundle);
        Greatball.Setup(assetBundle);
        Ultraball.Setup(assetBundle);
        Masterball.Setup(assetBundle);
        Utils.LoadSeeThroughShader(assetBundle);
        Utils.LoadWireframeMaterial(assetBundle);
        HoardingBugFlySfx = assetBundle.LoadAsset<AudioClip>("Assets/HoardingBug_Fly.ogg");
        hudPrefab = assetBundle.LoadAsset<GameObject>("Assets/UI/MonsterInfo.prefab");

        // Load monsters sprites
        const string monstersIconsNamePath = "assets/ui/monstersicons/";
        monstersSprites = assetBundle.GetAllAssetNames()
            .Where(assetName => assetName.StartsWith(monstersIconsNamePath) && assetName.EndsWith(".png"))
            .ToDictionary(
                assetName => assetName.Substring(monstersIconsNamePath.Length, assetName.Length - monstersIconsNamePath.Length - 4),
                assetName => assetBundle.LoadAsset<Sprite>(assetName)
            );
    }

    private void ApplyHarmonyPatches()
    {
        Harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

        Harmony.PatchAll(typeof(PlayerControllerBPatch));
        Harmony.PatchAll(typeof(RedLocustBeesPatch));
        Harmony.PatchAll(typeof(StartOfRoundPatch));
        Harmony.PatchAll(typeof(ModConfig.SyncHandshake));
        Harmony.PatchAll(typeof(DebugPatches));
        Harmony.PatchAll(typeof(TamedEnemyBehaviour));
        Harmony.PatchAll(typeof(KidnapperFoxTamedBehaviour));
        Harmony.PatchAll(typeof(MouthDogPatch));
        Harmony.PatchAll(typeof(EnemyAIPatch));
        Harmony.PatchAll(typeof(HUDManagerPatch));
        Harmony.PatchAll(typeof(FlowermanAIPatch));
        Harmony.PatchAll(typeof(BushWolfEnemyPatch));
        Harmony.PatchAll(typeof(RoundManagerPatch));

        // Static RPCs
        PlayerControllerBPatch.InitializeRPCS();
    }

    private static void Unpatch()
    {
        Logger.LogDebug("Unpatching...");

        Harmony?.UnpatchSelf();

        Logger.LogDebug("Finished unpatching!");
    }

    #region Logging
    public enum LogType
    {
        Message,
        Warning,
        Error,
        Fatal,
        Debug
    }

    internal static void Log(string message, LogType type = LogType.Debug)
    {
#if !DEBUG
            if (type == LogType.Debug /*&& !ModConfig.DebugLog*/)
                return;
#endif

        switch (type)
        {
            case LogType.Warning: Logger.LogWarning(message); break;
            case LogType.Error: Logger.LogError(message); break;
            case LogType.Fatal: Logger.LogFatal(message); break;
            default: Logger.LogMessage(message); break;
        }
    }
    #endregion
}