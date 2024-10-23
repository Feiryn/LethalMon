using HarmonyLib;

namespace LethalMon.Patches;

internal class TerminalPatch
{
    [HarmonyPatch(typeof(Terminal), nameof(Terminal.Start))]
    [HarmonyPostfix]
    private static void TerminalStartPostfix()
    {
        Utils.UnlockPCIfNotUnlocked();
        
        if (Utils.IsHost)
        {
            Registry.LoadAndCalculateMissingIds();
        }
    }
}