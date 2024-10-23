using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using LethalMon.Behaviours;
using LethalMon.Compatibility;
using LethalMon.Items;
using LethalMon.Patches;
using LethalMon.Save;
using LethalMon.Throw;
using UnityEngine;

namespace LethalMon;

[BepInDependency("com.rune580.LethalCompanyInputUtils", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("atomic.terminalapi", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("Mirage", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("Ovchinikov.SnatchinBracken.Main", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("meow.ModelReplacementAPI", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("antlershed.lethalcompany.enemyskinregistry", BepInDependency.DependencyFlags.SoftDependency)]
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class LethalMon : BaseUnityPlugin
{
    public static LethalMon Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }

    internal static GameObject? hudPrefab = null;

    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        ModConfig.Instance.Setup();
        LoadAssetBundle();
        NetcodePatching();
        ApplyHarmonyPatches();

        // Static RPCs
        PlayerControllerBPatch.InitializeRPCS();

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
    }

    private void NetcodePatching()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes();
        foreach (var type in types)
        {
            try
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
            catch (FileNotFoundException)
            {
                // Ignore
            }
        }
    }

    private void LoadAssetBundle()
    {
        AssetBundle assetBundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lethalmon"));

        Tier1Ball.Setup(assetBundle);
        Tier2Ball.Setup(assetBundle);
        Tier3Ball.Setup(assetBundle);
        Tier4Ball.Setup(assetBundle);

        Utils.LoadSeeThroughShader(assetBundle);
        Utils.LoadWireframeMaterial(assetBundle);

        ClaySurgeonTamedBehaviour.LoadAssets(assetBundle);
        MaskedTamedBehaviour.LoadGhostAudio(assetBundle);
        BaboonHawkTamedBehaviour.LoadAudio(assetBundle);
        HoarderBugTamedBehaviour.LoadAudio(assetBundle);
        SpiderTamedBehaviour.LoadAudio(assetBundle);
        
        PC.PC.LoadAssets(assetBundle);

        hudPrefab = assetBundle.LoadAsset<GameObject>("Assets/UI/MonsterInfo.prefab");

        // Load fallback monster sprite
        Registry.FallbackSprite = assetBundle.LoadAsset<Sprite>("assets/ui/monstersicons/Unknown.png");

        // Load vanilla monsters
        Registry.RegisterVanillaEnemies(assetBundle);
    }

    private void ApplyHarmonyPatches()
    {
        Harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

        // LethalMon specific
        Harmony.PatchAll(typeof(DebugPatches));
        Harmony.PatchAll(typeof(ModConfig.SyncHandshake));

        // Misc
        Harmony.PatchAll(typeof(PlayerControllerBPatch));
        Harmony.PatchAll(typeof(StartOfRoundPatch));
        Harmony.PatchAll(typeof(RoundManagerPatch));
        Harmony.PatchAll(typeof(HUDManagerPatch));
        Harmony.PatchAll(typeof(GameNetworkManagerPatch));
        Harmony.PatchAll(typeof(TerminalPatch));
        Harmony.PatchAll(typeof(AdvancedSavePatch));
        Harmony.PatchAll(typeof(DungeonGeneratorPatch));

        // Enemies
        Harmony.PatchAll(typeof(EnemyAIPatch));
        Harmony.PatchAll(typeof(RedLocustBeesPatch));
        Harmony.PatchAll(typeof(MouthDogPatch));
        Harmony.PatchAll(typeof(FlowermanAIPatch));
        Harmony.PatchAll(typeof(BushWolfEnemyPatch));
        Harmony.PatchAll(typeof(MaskedPlayerEnemyPatch));
        Harmony.PatchAll(typeof(BaboonBirdAIPatch));
        Harmony.PatchAll(typeof(SandSpiderAIPatch));

        // Enemy behaviours
        Harmony.PatchAll(typeof(TamedEnemyBehaviour));
        Harmony.PatchAll(typeof(KidnapperFoxTamedBehaviour));
        if (MirageCompatibility.Instance.Enabled)
            Harmony.PatchAll(typeof(MirageCompatibility));
        if (SnatchingBrackenCompatibility.Instance.Enabled)
            Harmony.PatchAll(typeof(SnatchingBrackenCompatibility));
        if (EnemySkinRegistryCompatibility.Instance.Enabled)
            Harmony.PatchAll(typeof(EnemySkinRegistryCompatibility));
        
        // Items
        Harmony.PatchAll(typeof(ThrowableItem));
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