using HarmonyLib;

namespace LethalMon.Patches;

public class TerminalPatch
{
    [HarmonyPatch(typeof(Terminal), nameof(Terminal.Start))]
    [HarmonyPostfix]
    private static void TerminalStartPostfix()
    {
        try
        {
            if (!Utils.IsHost)
                return;
            
            if (PC.PC.pcPrefab == null)
            {
                LethalMon.Log("PC prefab is null, returning", LethalMon.LogType.Error);
                return;
            }

            var placeableShipObject = PC.PC.pcPrefab.GetComponentInChildren<PlaceableShipObject>();

            if (placeableShipObject == null)
            {
                LethalMon.Log("PC PlaceableShipObject is null, returning", LethalMon.LogType.Error);
                return;
            }

            var unlockable =
                StartOfRound.Instance.unlockablesList.unlockables.Find(u => u.prefabObject == PC.PC.pcPrefab);

            if (unlockable == null)
            {
                LethalMon.Log("PC Unlockable not found, returning", LethalMon.LogType.Error);
                return;
            }

            LethalMon.Log("Unlockable ID: " + placeableShipObject.unlockableID);
            LethalMon.Log("Unlockable has been unlocked: " + unlockable.hasBeenUnlockedByPlayer);

            if (!unlockable.hasBeenUnlockedByPlayer)
                StartOfRound.Instance.UnlockShipObject(placeableShipObject.unlockableID);
        }
        catch (System.Exception e)
        {
            LethalMon.Log(e.ToString(), LethalMon.LogType.Error);
        }
    }
}