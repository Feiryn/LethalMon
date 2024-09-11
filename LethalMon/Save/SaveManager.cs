namespace LethalMon.Save;

public class SaveManager
{
    private static readonly string SavePath = UnityEngine.Application.persistentDataPath + "/LethalMonSave.json";

    private static Save? _save;

    private static Save Save => _save ??= LoadSave();
    
    private static Save LoadSave()
    {
        if (System.IO.File.Exists(SavePath))
        {
            var json = System.IO.File.ReadAllText(SavePath);
            return UnityEngine.JsonUtility.FromJson<Save>(json);
        }

        return new Save();
    }
    
    private static void WriteSave()
    {
        var json = UnityEngine.JsonUtility.ToJson(Save);
        System.IO.File.WriteAllText(SavePath, json);
    }
    
    public static void UnlockDexEntry(string entry)
    {
        if (!Save.UnlockedDexEntries.Contains(entry))
        {
            Save.UnlockedDexEntries.Add(entry);

            WriteSave();
        }
    }
    
    public static void UnlockDna(string dna)
    {
        if (!Save.UnlockedDna.Contains(dna))
        {
            Save.UnlockedDna.Add(dna);

            WriteSave();
        }
    }
    
    public static bool IsDexEntryUnlocked(string entry)
    {
        return Save.UnlockedDexEntries.Contains(entry);
    }
    
    public static bool IsDnaUnlocked(string dna)
    {
        return Save.UnlockedDna.Contains(dna);
    }
    
    #if DEBUG
    public static void DebugUnlockAll()
    {
        foreach (var entry in Data.CatchableMonsters)
        {
            Save.UnlockedDexEntries.Add(entry.Key);
            Save.UnlockedDna.Add(entry.Key);
        }
    }
    #endif
}