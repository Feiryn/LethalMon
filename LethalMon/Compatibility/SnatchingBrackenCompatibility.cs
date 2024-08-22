using HarmonyLib;

namespace LethalMon.Compatibility;

public class SnatchingBrackenCompatibility
{
    public const string SnatchingBrackenReferenceChain = "Ovchinikov.SnatchinBracken.Main";

    private static bool? _snatchingBrackenEnabled;

    public static bool Enabled
    {
        get
        {
            if (_snatchingBrackenEnabled == null)
            {
                _snatchingBrackenEnabled = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(SnatchingBrackenReferenceChain);
                LethalMon.Log("SnatchingBracken enabled? " + _snatchingBrackenEnabled);
            }

            return _snatchingBrackenEnabled.Value;
        }
    }
    
    public static void DropPlayer(FlowermanAI flowerman)
    {
        if (!Enabled) return;
        
        // SnatchingBracken drops the player at SetEnemyStunnedPrefix, not matter if the bracken is stunned or not
        LethalMon.Log("Make bracken drop the player (if any)");
        flowerman.SetEnemyStunned(false);
    }
}