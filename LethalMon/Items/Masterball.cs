using UnityEngine;

namespace LethalMon.Items;

public class Masterball : PokeballItem
{
    public static GameObject? spawnPrefab = null;

    public Masterball() : base(BallType.MASTER_BALL, 3)
    {
    }

    internal static void Setup(AssetBundle assetBundle)
    {
        spawnPrefab = InitBallPrefab<Masterball>(assetBundle, "Masterball/Masterball.asset", 2);
    }
}