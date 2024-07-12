using UnityEngine;

namespace LethalMon.Items;

public class Masterball : PokeballItem
{
    public static GameObject? SpawnPrefab = null;

    public Masterball() : base(BallType.MASTER_BALL, 3)
    {
    }
    
    internal static void Setup(AssetBundle assetBundle)
    {
        var ballItem = InitBallPrefab<Masterball>(assetBundle, "Masterball/Masterball.asset", 2); // todo make rarity configurable

        if (ballItem == null) return;
        
        SpawnPrefab = ballItem.spawnPrefab;
        LethalLib.Modules.Items.RegisterShopItem(ballItem, price: 1200,
            itemInfo: Utils.CreateTerminalNode("Master ball",
                "Use it to capture monsters. Fourth ball tier with a 100% capture rate.")); // todo make price and availability configurable
        LethalMon.Log("Master ball added to shop");
    }
}