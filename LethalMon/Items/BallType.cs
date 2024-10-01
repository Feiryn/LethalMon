using UnityEngine;

namespace LethalMon.Items;

public enum BallType
{
    TIER1,
    TIER2,
    TIER3,
    TIER4
}

public class BallTypeMethods
{
    public static GameObject? GetPrefab(BallType ballType)
    {
        switch (ballType)
        {
            case BallType.TIER2:
                return Tier2Ball.SpawnPrefab;
            case BallType.TIER3:
                return Tier3Ball.SpawnPrefab;
            case BallType.TIER4:
                return Tier4Ball.SpawnPrefab;
            default:
                return Tier1Ball.SpawnPrefab;
        }
    }
}