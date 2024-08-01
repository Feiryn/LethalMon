using UnityEngine;

namespace LethalMon.Items;

public class Ultraball : PokeballItem
{
    public static GameObject? SpawnPrefab = null;

    public static Item? BallItem = null;
    
    public Ultraball() : base(BallType.ULTRA_BALL, 2)
    {
    }
    
    internal static void Setup(AssetBundle assetBundle)
    {
        var ballItem = InitBallPrefab<Ultraball>(assetBundle, "Ultraball/Ultraball.asset", ModConfig.Instance.values.Tier3BallSpawnWeight);

        if (ballItem == null) return;
        
        BallItem = ballItem;
        SpawnPrefab = ballItem.spawnPrefab;
        LethalLib.Modules.Items.RegisterShopItem(BallItem, price: ModConfig.Instance.values.Tier3BallCost,
            itemInfo: Utils.CreateTerminalNode("Ultra ball",
                "Use it to capture monsters. Third ball tier with a high capture rate."));
        LethalMon.Log("Ultra ball added to shop");
    }
}