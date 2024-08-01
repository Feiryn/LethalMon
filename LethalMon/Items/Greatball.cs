using UnityEngine;

namespace LethalMon.Items;

public class Greatball : PokeballItem
{
    public static GameObject? SpawnPrefab = null;

    public static Item? BallItem = null;
    
    public Greatball() : base(BallType.GREAT_BALL, 1)
    {
    }
    
    internal static void Setup(AssetBundle assetBundle)
    {
        var ballItem = InitBallPrefab<Greatball>(assetBundle, "Greatball/Greatball.asset", ModConfig.Instance.values.Tier2BallSpawnWeight);

        if (ballItem == null) return;
        
        BallItem = ballItem;
        SpawnPrefab = ballItem.spawnPrefab;
        LethalLib.Modules.Items.RegisterShopItem(BallItem, price: ModConfig.Instance.values.Tier2BallCost,
            itemInfo: Utils.CreateTerminalNode("Great ball",
                "Use it to capture monsters. Second ball tier with a medium capture rate."));
        LethalMon.Log("Great ball added to shop");
    }
}