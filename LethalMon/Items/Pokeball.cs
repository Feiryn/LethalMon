using UnityEngine;

namespace LethalMon.Items;

public class Pokeball : PokeballItem
{
    public static GameObject? SpawnPrefab;

    public Pokeball() : base(BallType.POKEBALL, 0)
    {
    }

    internal static void Setup(AssetBundle assetBundle)
    {
        var ballItem = InitBallPrefab<Pokeball>(assetBundle, "Pokeball/Pokeball.asset", 20); // todo make rarity configurable

        if (ballItem == null) return;
        
        SpawnPrefab = ballItem.spawnPrefab;
        LethalLib.Modules.Items.RegisterShopItem(ballItem, price: 200,
            itemInfo: Utils.CreateTerminalNode("Pokeball",
                "Use it to capture monsters. First ball tier with a low capture rate.")); // todo make price and availability configurable
        LethalMon.Log("Pokeball added to shop");
    }
}