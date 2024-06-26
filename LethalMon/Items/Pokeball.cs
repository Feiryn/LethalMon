using LethalLib.Modules;
using UnityEngine;

namespace LethalMon.Items;

public class Pokeball : PokeballItem
{
    public static GameObject? pokeballSpawnPrefab = null;

    public Pokeball() : base(BallType.POKEBALL, 0)
    {
    }

    internal static void Setup(AssetBundle assetBundle)
    {
        Item pokeballItem = assetBundle.LoadAsset<Item>("Assets/Balls/Pokeball/Pokeball.asset");

        Pokeball script = pokeballItem.spawnPrefab.AddComponent<Pokeball>();
        script.itemProperties = pokeballItem;
        script.grabbable = true;
        script.grabbableToEnemies = true;
        NetworkPrefabs.RegisterNetworkPrefab(pokeballItem.spawnPrefab);

        LethalLib.Modules.Items.RegisterScrap(pokeballItem, 20, Levels.LevelTypes.All);

        pokeballSpawnPrefab = pokeballItem.spawnPrefab;
    }
}