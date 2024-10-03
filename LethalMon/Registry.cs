using System;
using System.Collections.Generic;
using System.Linq;
using LethalMon.Behaviours;
using LethalMon.CatchableEnemy;
using UnityEngine;

namespace LethalMon;

public static class Registry
{
    private static readonly string EnemyTypesIdsKey = "LethalMon_EnemyTypesIds";
    
    private static Dictionary<string, int> _enemiesTypesIds = new();
    
    internal static readonly Dictionary<string, CatchableEnemy.CatchableEnemy> CatchableEnemies = new();
    
    private static readonly Dictionary<string, Sprite> EnemiesSprites = new();
    
    // Add your custom behaviour classes here
    private static readonly Dictionary<Type, Type> EnemiesTamedBehaviours = new();
    
    internal static Sprite FallbackSprite = null!;
    
    private static int _currentId;
    
    public static int? GetEnemyTypeId(string enemyType)
    {
        return _enemiesTypesIds.GetValueOrDefault(enemyType);
    }
    
    public static Sprite GetEnemySprite(string enemyType)
    {
        return EnemiesSprites.GetValueOrDefault(enemyType, FallbackSprite);
    }
    
    internal static Type GetTamedBehaviour(Type enemyType)
    {
        return EnemiesTamedBehaviours.GetValueOrDefault(enemyType, typeof(TamedEnemyBehaviour));
    }
    
    public static void RegisterEnemy(string enemyTypeName, CatchableEnemy.CatchableEnemy catchableEnemy, Type originalEnemyAiType, Type tamedBehaviourClassType, Sprite sprite)
    {
        if (CatchableEnemies.ContainsKey(enemyTypeName))
        {
            LethalMon.Log($"Enemy type {enemyTypeName} is already registered", LethalMon.LogType.Warning);
            return;
        }
        
        if (catchableEnemy.CatchDifficulty < 0 || catchableEnemy.CatchDifficulty >= Data.CaptureProbabilities[0].Length)
        {
            LethalMon.Log($"Catch difficulty for enemy type {enemyTypeName} is invalid. It must be between 0 and {Data.CaptureProbabilities[0].Length - 1}", LethalMon.LogType.Error);
            return;
        }
        
        if (tamedBehaviourClassType.IsSubclassOf(typeof(TamedEnemyBehaviour)))
        {
            CatchableEnemies[enemyTypeName] = catchableEnemy;
            EnemiesSprites[enemyTypeName] = sprite;
            EnemiesTamedBehaviours[originalEnemyAiType] = tamedBehaviourClassType;
        }
        else
        {
            LethalMon.Log($"Tamed behaviour class {tamedBehaviourClassType} is not a subclass of TamedEnemyBehaviour", LethalMon.LogType.Error);
        }
    }
    
    public static bool IsEnemyRegistered(string enemyTypeName)
    {
        return CatchableEnemies.ContainsKey(enemyTypeName);
    }
    
    public static CatchableEnemy.CatchableEnemy? GetCatchableEnemy(string enemyTypeName)
    {
        return CatchableEnemies.GetValueOrDefault(enemyTypeName);
    }
    
    internal static CatchableEnemy.CatchableEnemy? GetCatchableEnemy(int enemyTypeId)
    {
        return CatchableEnemies.FirstOrDefault(cm => _enemiesTypesIds[cm.Key] == enemyTypeId).Value;
    }
    
    internal static string GetEnemyTypeName(int enemyTypeId)
    {
        return CatchableEnemies.FirstOrDefault(cm => _enemiesTypesIds[cm.Key] == enemyTypeId).Key;
    }
    
    public static void RemoveEnemy(string enemyTypeName)
    {
        if (CatchableEnemies.ContainsKey(enemyTypeName))
            CatchableEnemies.Remove(enemyTypeName);
        
        if (EnemiesSprites.ContainsKey(enemyTypeName))
            EnemiesSprites.Remove(enemyTypeName);
    }

    internal static void RegisterVanillaEnemies(AssetBundle assetBundle)
    {
        RegisterEnemy(Utils.Enemy.BaboonHawk.ToString(), new CatchableBaboonHawk(), typeof(BaboonBirdAI), typeof(BaboonHawkTamedBehaviour), assetBundle.LoadAsset<Sprite>("assets/ui/monstersicons/BaboonHawk.png"));
        RegisterEnemy(Utils.Enemy.Blob.ToString(), new CatchableBlob(), typeof(BlobAI), typeof(BlobTamedBehaviour), assetBundle.LoadAsset<Sprite>("assets/ui/monstersicons/Blob.png"));
        RegisterEnemy(Utils.Enemy.Butler.ToString(), new CatchableButler(), typeof(ButlerEnemyAI), typeof(ButlerTamedBehaviour), assetBundle.LoadAsset<Sprite>("assets/ui/monstersicons/Butler.png"));
        RegisterEnemy(Utils.Enemy.Crawler.ToString(), new CatchableCrawler(), typeof(CrawlerAI), typeof(CrawlerTamedBehaviour), assetBundle.LoadAsset<Sprite>("assets/ui/monstersicons/Crawler.png"));
        RegisterEnemy(Utils.Enemy.Flowerman.ToString(), new CatchableFlowerman(), typeof(FlowermanAI), typeof(FlowermanTamedBehaviour), assetBundle.LoadAsset<Sprite>("assets/ui/monstersicons/Flowerman.png"));
        RegisterEnemy(Utils.Enemy.DressGirl.ToString(), new CatchableGhostGirl(), typeof(DressGirlAI), typeof(GhostGirlTamedBehaviour), assetBundle.LoadAsset<Sprite>("assets/ui/monstersicons/DressGirl.png"));
        RegisterEnemy(Utils.Enemy.HoarderBug.ToString(), new CatchableHoarderBug(), typeof(HoarderBugAI), typeof(HoarderBugTamedBehaviour), assetBundle.LoadAsset<Sprite>("assets/ui/monstersicons/HoarderBug.png"));
        RegisterEnemy(Utils.Enemy.MaskedPlayerEnemy.ToString(), new CatchableMasked(), typeof(MaskedPlayerEnemy), typeof(MaskedTamedBehaviour), assetBundle.LoadAsset<Sprite>("assets/ui/monstersicons/MaskedPlayerEnemy.png"));
        RegisterEnemy(Utils.Enemy.MouthDog.ToString(), new CatchableMouthDog(), typeof(MouthDogAI), typeof(MouthDogTamedBehaviour), assetBundle.LoadAsset<Sprite>("assets/ui/monstersicons/MouthDog.png"));
        RegisterEnemy(Utils.Enemy.Nutcracker.ToString(), new CatchableNutcracker(), typeof(NutcrackerEnemyAI), typeof(NutcrackerTamedBehaviour), assetBundle.LoadAsset<Sprite>("assets/ui/monstersicons/Nutcracker.png"));
        RegisterEnemy(Utils.Enemy.RedLocustBees.ToString(), new CatchableRedLocustBees(), typeof(RedLocustBees), typeof(RedLocustBeesTamedBehaviour), assetBundle.LoadAsset<Sprite>("assets/ui/monstersicons/RedLocustBees.png"));
        RegisterEnemy(Utils.Enemy.SandSpider.ToString(), new CatchableSpider(), typeof(SandSpiderAI), typeof(SpiderTamedBehaviour), assetBundle.LoadAsset<Sprite>("assets/ui/monstersicons/SandSpider.png"));
        RegisterEnemy(Utils.Enemy.Puffer.ToString(), new CatchableSporeLizard(), typeof(PufferAI), typeof(SporeLizardTamedBehaviour), assetBundle.LoadAsset<Sprite>("assets/ui/monstersicons/Puffer.png"));
        RegisterEnemy(Utils.Enemy.FlowerSnake.ToString(), new CatchableTulipSnake(), typeof(FlowerSnakeEnemy), typeof(TulipSnakeTamedBehaviour), assetBundle.LoadAsset<Sprite>("assets/ui/monstersicons/FlowerSnake.png"));
    }
    
    private static int GetNextId()
    {
        return _currentId++;
    }

    internal static void LoadAndCalculateMissingIds()
    {
        if (ES3.KeyExists(EnemyTypesIdsKey, GameNetworkManager.Instance.currentSaveFileName))
        {
            _enemiesTypesIds.Clear();
            _enemiesTypesIds = ES3.Load<Dictionary<string, int>>(EnemyTypesIdsKey, GameNetworkManager.Instance.currentSaveFileName);
        }

        _currentId = _enemiesTypesIds.Count == 0 ? 1 : _enemiesTypesIds.Values.Max();
        
        foreach (KeyValuePair<string, CatchableEnemy.CatchableEnemy> catchableEnemy in CatchableEnemies)
        {
            if (!_enemiesTypesIds.ContainsKey(catchableEnemy.Key))
            {
                _enemiesTypesIds[catchableEnemy.Key] = GetNextId();
            }
        }
        
        ES3.Save(EnemyTypesIdsKey, _enemiesTypesIds, GameNetworkManager.Instance.currentSaveFileName);
    }
}