using HarmonyLib;

namespace LethalMon.Patches;

public class TerminalPatch
{
    [HarmonyPatch(typeof(Terminal), nameof(Terminal.Start))]
    [HarmonyPostfix]
    private static void TerminalStartPostfix()
    {
        Utils.UnlockPCIfNotUnlocked();
    }
}