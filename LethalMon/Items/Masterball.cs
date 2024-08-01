using UnityEngine;

namespace LethalMon.Items;

public class Masterball : PokeballItem
{
    public static GameObject? SpawnPrefab = null;

    public static Item? BallItem = null;

    public Masterball() : base(BallType.MASTER_BALL, 3)
    {
    }
    
    internal static void Setup(AssetBundle assetBundle)
    {
        var ballItem = InitBallPrefab<Masterball>(assetBundle, "Masterball/Masterball.asset", ModConfig.Instance.values.Tier4BallSpawnWeight);

        if (ballItem == null) return;

        BallItem = ballItem;
        SpawnPrefab = ballItem.spawnPrefab;
        LethalLib.Modules.Items.RegisterShopItem(BallItem, price: ModConfig.Instance.values.Tier4BallCost,
            itemInfo: Utils.CreateTerminalNode("Master ball",
                "Use it to capture monsters. Fourth ball tier with a 100% capture rate."));
        LethalMon.Log("Master ball added to shop");
    }
}