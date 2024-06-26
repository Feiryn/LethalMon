using UnityEngine;

namespace LethalMon.Items;

public class Greatball : PokeballItem
{
    public static GameObject? spawnPrefab = null;

    public Greatball() : base(BallType.GREAT_BALL, 1)
    {
    }

    internal static void Setup(AssetBundle assetBundle)
    {
        spawnPrefab = GetBallPrefab(assetBundle, "Assets/Balls/Greatball/Greatball.asset");
    }
}