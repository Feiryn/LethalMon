using UnityEngine;

namespace LethalMon.Items;

public class Ultraball : PokeballItem
{
    public static GameObject? SpawnPrefab = null;

    public Ultraball() : base(BallType.ULTRA_BALL, 2)
    {
    }
    
    internal static void Setup(AssetBundle assetBundle)
    {
        var ballItem = InitBallPrefab<Ultraball>(assetBundle, "Ultraball/Ultraball.asset", 6); // todo make rarity configurable

        if (ballItem == null) return;
        
        SpawnPrefab = ballItem.spawnPrefab;
        LethalLib.Modules.Items.RegisterShopItem(ballItem, price: 375,
            itemInfo: Utils.CreateTerminalNode("Ultra ball",
                "Use it to capture monsters. Third ball tier with a high capture rate.")); // todo make price and availability configurable
        LethalMon.Log("Ultra ball added to shop");
    }
}