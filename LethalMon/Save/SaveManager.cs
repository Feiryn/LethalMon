using System.Collections.Generic;
using System.IO;

namespace LethalMon.Save;

public class SaveManager
{
    private static readonly string SavePath = UnityEngine.Application.persistentDataPath + Path.DirectorySeparatorChar + "LethalMonSave.json";

    private const string Es3UnlockedDexEntriesKey = "LethalMon_unlockedDexEntries";

    private const string Es3UnlockedDnaKey = "LethalMon_unlockedDna";

    private static Save? _save;

    private static Save Save => _save ??= LoadSave();
    
    private static Save LoadSave()
    {
        if (ModConfig.Instance.values.PcGlobalSave)
        {
            if (File.Exists(SavePath))
            {
                var json = File.ReadAllText(SavePath);
                return UnityEngine.JsonUtility.FromJson<Save>(json);
            }

            return new Save();
        }

        var save = new Save();

        if (Utils.IsHost)
        {
            try
            {
                if (ES3.KeyExists(Es3UnlockedDexEntriesKey, GameNetworkManager.Instance.currentSaveFileName))
                {
                    save.unlockedDexEntries = ES3.Load<List<string>>(Es3UnlockedDexEntriesKey, GameNetworkManager.Instance.currentSaveFileName);
                }

                if (ES3.KeyExists(Es3UnlockedDnaKey, GameNetworkManager.Instance.currentSaveFileName))
                {
                    save.unlockedDna = ES3.Load<List<string>>(Es3UnlockedDnaKey, GameNetworkManager.Instance.currentSaveFileName);
                }
            }
            catch (System.Exception e)
            {
                LethalMon.Log($"Failed to load save: {e}", LethalMon.LogType.Error);
            }
        }

        return save;
    }
    
    private static void WriteSave()
    {
        if (ModConfig.Instance.values.PcGlobalSave)
        {
            var json = UnityEngine.JsonUtility.ToJson(Save);
            File.WriteAllText(SavePath, json);
        }
        else if (Utils.IsHost)
        {
            ES3.Save(Es3UnlockedDexEntriesKey, Save.unlockedDexEntries, GameNetworkManager.Instance.currentSaveFileName);
            ES3.Save(Es3UnlockedDnaKey, Save.unlockedDna, GameNetworkManager.Instance.currentSaveFileName);
        }
    }
    
    public static void UnlockDexEntry(string entry)
    {
        if (!Save.unlockedDexEntries.Contains(entry))
        {
            Save.unlockedDexEntries.Add(entry);

            WriteSave();
        }
    }
    
    public static void UnlockDna(string dna)
    {
        if (!Save.unlockedDna.Contains(dna))
        {
            Save.unlockedDna.Add(dna);

            WriteSave();
        }
    }
    
    public static string[] GetUnlockedDexEntries()
    {
        return Save.unlockedDexEntries.ToArray();
    }
    
    public static string[] GetUnlockedDna()
    {
        return Save.unlockedDna.ToArray();
    }
    
    public static Save GetSave()
    {
        return Save;
    }
    
    public static void SyncSave(Save save)
    {
        _save = save;
    }
    
    public static void ClearSave()
    {
        _save = null;
    }
    
    #if DEBUG
    public static void DebugUnlockAll()
    {
        foreach (var entry in Data.CatchableMonsters)
        {
            Save.unlockedDexEntries.Add(entry.Key);
            Save.unlockedDna.Add(entry.Key);
        }
    }
    #endif
}