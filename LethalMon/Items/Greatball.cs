using UnityEngine;

namespace LethalMon.Items;

public class Greatball : PokeballItem
{
    public static GameObject? SpawnPrefab = null;

    public Greatball() : base(BallType.GREAT_BALL, 1)
    {
    }
    
    internal static void Setup(AssetBundle assetBundle)
    {
        var ballItem = InitBallPrefab<Greatball>(assetBundle, "Greatball/Greatball.asset", 10); // todo make rarity configurable

        if (ballItem == null) return;
        
        SpawnPrefab = ballItem.spawnPrefab;
        LethalLib.Modules.Items.RegisterShopItem(ballItem, price: 500,
            itemInfo: Utils.CreateTerminalNode("Great ball",
                "Use it to capture monsters. Second ball tier with a medium capture rate.")); // todo make price and availability configurable
        LethalMon.Log("Great ball added to shop");
    }
}