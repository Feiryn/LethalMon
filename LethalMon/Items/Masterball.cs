using LethalLib.Modules;
using UnityEngine;

namespace LethalMon.Items;

public class Masterball : PokeballItem
{
    public static GameObject? masterBallSpawnPrefab = null;
    public Masterball() : base(BallType.MASTER_BALL, 3)
    {
    }

    internal static void Setup(AssetBundle assetBundle)
    {
        Item masterballItem = assetBundle.LoadAsset<Item>("Assets/Balls/Masterball/Masterball.asset");

        Masterball script = masterballItem.spawnPrefab.AddComponent<Masterball>();
        script.itemProperties = masterballItem;
        script.grabbable = true;
        script.grabbableToEnemies = true;
        NetworkPrefabs.RegisterNetworkPrefab(masterballItem.spawnPrefab);

        LethalLib.Modules.Items.RegisterScrap(masterballItem, 2, Levels.LevelTypes.All);

        masterBallSpawnPrefab = masterballItem.spawnPrefab;
    }
}