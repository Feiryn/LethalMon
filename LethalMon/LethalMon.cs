using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using LethalLib.Modules;
using LethalMon.AI;
using LethalMon.Items;
using LethalMon.Patches;
using LethalMon.Throw;
using UnityEngine;

namespace LethalMon;

[BepInDependency("com.rune580.LethalCompanyInputUtils", BepInDependency.DependencyFlags.SoftDependency)]
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class LethalMon : BaseUnityPlugin
{
    public static LethalMon Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger { get; private set; } = null!;
    internal static Harmony? Harmony { get; set; }

    public static GameObject pokeballSpawnPrefab;
    
    public static GameObject greatBallSpawnPrefab;
    
    public static GameObject ultraBallSpawnPrefab;
    
    public static GameObject masterBallSpawnPrefab;
    
    private void Awake()
    {
        Logger = base.Logger;
        Instance = this;

        ModConfig.Instance.Setup();

        AssetBundle assetBundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "lethalmon"));

        this.SetupPokeball(assetBundle);
        this.SetupGreatball(assetBundle);
        this.SetupUltraball(assetBundle);
        this.SetupMasterball(assetBundle);

        Harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        Harmony.PatchAll(typeof(PlayerControllerBPatch));
        Harmony.PatchAll(typeof(RedLocustBeesPatch));
        Harmony.PatchAll(typeof(StartOfRoundPatch));
        Harmony.PatchAll(typeof(ModConfig.SyncHandshake));
        PokeballItem.InitializeRPCS();
        HoarderBugCustomAI.InitializeRPCS();
        PlayerControllerBPatch.InitializeRPCS();
        ThrowableItem.InitializeRPCS();
        RedLocustBeesCustomAI.InitializeRPCS();

        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
    }

    private void SetupPokeball(AssetBundle assetBundle)
    {
        Item pokeballItem = assetBundle.LoadAsset<Item>("Assets/Balls/Pokeball/Pokeball.asset");
        
        Pokeball script = pokeballItem.spawnPrefab.AddComponent<Pokeball>();
        script.itemProperties = pokeballItem;
        script.grabbable = true;
        script.grabbableToEnemies = true;
        NetworkPrefabs.RegisterNetworkPrefab(pokeballItem.spawnPrefab);
        
        LethalLib.Modules.Items.RegisterScrap(pokeballItem, 20, Levels.LevelTypes.All);

        LethalMon.pokeballSpawnPrefab = pokeballItem.spawnPrefab;
    }
    
    private void SetupGreatball(AssetBundle assetBundle)
    {
        Item greatballItem = assetBundle.LoadAsset<Item>("Assets/Balls/Greatball/Greatball.asset");
        
        Greatball script = greatballItem.spawnPrefab.AddComponent<Greatball>();
        script.itemProperties = greatballItem;
        script.grabbable = true;
        script.grabbableToEnemies = true;
        NetworkPrefabs.RegisterNetworkPrefab(greatballItem.spawnPrefab);
        
        LethalLib.Modules.Items.RegisterScrap(greatballItem, 10, Levels.LevelTypes.All);

        LethalMon.greatBallSpawnPrefab = greatballItem.spawnPrefab;
    }
    
    private void SetupUltraball(AssetBundle assetBundle)
    {
        Item ultraballItem = assetBundle.LoadAsset<Item>("Assets/Balls/Ultraball/Ultraball.asset");
        
        Ultraball script = ultraballItem.spawnPrefab.AddComponent<Ultraball>();
        script.itemProperties = ultraballItem;
        script.grabbable = true;
        script.grabbableToEnemies = true;
        NetworkPrefabs.RegisterNetworkPrefab(ultraballItem.spawnPrefab);
        
        LethalLib.Modules.Items.RegisterScrap(ultraballItem, 6, Levels.LevelTypes.All);

        LethalMon.ultraBallSpawnPrefab = ultraballItem.spawnPrefab;
    }
    
    private void SetupMasterball(AssetBundle assetBundle)
    {
        Item masterballItem = assetBundle.LoadAsset<Item>("Assets/Balls/Masterball/Masterball.asset");
        
        Masterball script = masterballItem.spawnPrefab.AddComponent<Masterball>();
        script.itemProperties = masterballItem;
        script.grabbable = true;
        script.grabbableToEnemies = true;
        NetworkPrefabs.RegisterNetworkPrefab(masterballItem.spawnPrefab);
        
        LethalLib.Modules.Items.RegisterScrap(masterballItem, 2, Levels.LevelTypes.All);
        
        LethalMon.masterBallSpawnPrefab = masterballItem.spawnPrefab;
    }
    
    internal static void Unpatch()
    {
        Logger.LogDebug("Unpatching...");

        Harmony?.UnpatchSelf();

        Logger.LogDebug("Finished unpatching!");
    }
}
