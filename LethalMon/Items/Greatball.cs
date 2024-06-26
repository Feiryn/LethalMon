using LethalLib.Modules;
using UnityEngine;

namespace LethalMon.Items;

public class Greatball : PokeballItem
{
    public static GameObject? greatBallSpawnPrefab = null;

    public Greatball() : base(BallType.GREAT_BALL, 1)
    {
    }

    internal static void Setup(AssetBundle assetBundle)
    {
        Item greatballItem = assetBundle.LoadAsset<Item>("Assets/Balls/Greatball/Greatball.asset");

        Greatball script = greatballItem.spawnPrefab.AddComponent<Greatball>();
        script.itemProperties = greatballItem;
        script.grabbable = true;
        script.grabbableToEnemies = true;
        NetworkPrefabs.RegisterNetworkPrefab(greatballItem.spawnPrefab);

        LethalLib.Modules.Items.RegisterScrap(greatballItem, 10, Levels.LevelTypes.All);

        greatBallSpawnPrefab = greatballItem.spawnPrefab;
    }
}