using LethalLib.Modules;
using UnityEngine;

namespace LethalMon.Items;

public class Ultraball : PokeballItem
{
    public static GameObject? ultraBallSpawnPrefab = null;
    public Ultraball() : base(BallType.ULTRA_BALL, 2)
    {
    }

    internal static void Setup(AssetBundle assetBundle)
    {
        Item ultraballItem = assetBundle.LoadAsset<Item>("Assets/Balls/Ultraball/Ultraball.asset");

        Ultraball script = ultraballItem.spawnPrefab.AddComponent<Ultraball>();
        script.itemProperties = ultraballItem;
        script.grabbable = true;
        script.grabbableToEnemies = true;
        NetworkPrefabs.RegisterNetworkPrefab(ultraballItem.spawnPrefab);

        LethalLib.Modules.Items.RegisterScrap(ultraballItem, 6, Levels.LevelTypes.All);

        ultraBallSpawnPrefab = ultraballItem.spawnPrefab;
    }
}