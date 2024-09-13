using System.IO;

namespace LethalMon.Save;

public class SaveManager
{
    private static readonly string SavePath = UnityEngine.Application.persistentDataPath + Path.DirectorySeparatorChar + "LethalMonSave.json";

    private static Save? _save;

    private static Save Save => _save ??= LoadSave();
    
    private static Save LoadSave()
    {
        if (File.Exists(SavePath))
        {
            var json = File.ReadAllText(SavePath);
            return UnityEngine.JsonUtility.FromJson<Save>(json);
        }

        return new Save();
    }
    
    private static void WriteSave()
    {
        var json = UnityEngine.JsonUtility.ToJson(Save);
        File.WriteAllText(SavePath, json);
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
    
    public static bool IsDexEntryUnlocked(string entry)
    {
        return Save.unlockedDexEntries.Contains(entry);
    }
    
    public static bool IsDnaUnlocked(string dna)
    {
        return Save.unlockedDna.Contains(dna);
    }
    
    public static string[] GetUnlockedDexEntries()
    {
        return Save.unlockedDexEntries.ToArray();
    }
    
    public static string[] GetUnlockedDna()
    {
        return Save.unlockedDna.ToArray();
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