using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace LethalMon.Save;

[Serializable]
public class Save
{
    [FormerlySerializedAs("UnlockedDexEntries")] public List<string> unlockedDexEntries = [];
    
    [FormerlySerializedAs("UnlockedDna")] public List<string> unlockedDna = [];
}