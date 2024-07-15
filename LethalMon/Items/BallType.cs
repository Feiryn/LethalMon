using UnityEngine;

namespace LethalMon.Items;

public enum BallType
{
    POKEBALL,
    GREAT_BALL,
    ULTRA_BALL,
    MASTER_BALL
}

public class BallTypeMethods
{
    public static GameObject? GetPrefab(BallType ballType)
    {
        switch (ballType)
        {
            case BallType.GREAT_BALL:
                return Greatball.SpawnPrefab;
            case BallType.ULTRA_BALL:
                return Ultraball.SpawnPrefab;
            case BallType.MASTER_BALL:
                return Masterball.SpawnPrefab;
            default:
                return Pokeball.SpawnPrefab;
        }
    }
}