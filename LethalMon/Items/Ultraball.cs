using UnityEngine;

namespace LethalMon.Items;

public class Ultraball : PokeballItem
{
    public static GameObject? spawnPrefab = null;

    public Ultraball() : base(BallType.ULTRA_BALL, 2)
    {
    }

    internal static void Setup(AssetBundle assetBundle)
    {
        spawnPrefab = InitBallPrefab<Pokeball>(assetBundle, "Ultraball/Ultraball.asset", 6);
    }
}