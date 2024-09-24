namespace LethalMon.Compatibility;

public class SnatchingBrackenCompatibility() : ModCompatibility("Ovchinikov.SnatchinBracken.Main")
{
    public static SnatchingBrackenCompatibility Instance { get; } = new();

    public static void DropPlayer(FlowermanAI flowerman)
    {
        if (!Instance.Enabled) return;
        
        // SnatchingBracken drops the player at SetEnemyStunnedPrefix, not matter if the bracken is stunned or not
        LethalMon.Log("Make bracken drop the player (if any)");
        flowerman.SetEnemyStunned(false);
    }
}