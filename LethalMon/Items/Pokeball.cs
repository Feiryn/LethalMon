using UnityEngine;

namespace LethalMon.Items;

public class Pokeball : PokeballItem
{
    public static GameObject? spawnPrefab = null;

    public Pokeball() : base(BallType.POKEBALL, 0)
    {
    }

    internal static void Setup(AssetBundle assetBundle)
    {
        spawnPrefab = GetBallPrefab(assetBundle, "Assets/Balls/Pokeball/Pokeball.asset");
    }
}