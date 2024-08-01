using UnityEngine;

namespace LethalMon.Items;

public class Pokeball : PokeballItem
{
    public static GameObject? SpawnPrefab;

    public static Item? BallItem = null;
    
    public Pokeball() : base(BallType.POKEBALL, 0)
    {
    }

    internal static void Setup(AssetBundle assetBundle)
    {
        var ballItem = InitBallPrefab<Pokeball>(assetBundle, "Pokeball/Pokeball.asset", ModConfig.Instance.values.Tier1BallSpawnWeight);

        if (ballItem == null) return;
        
        BallItem = ballItem;
        SpawnPrefab = ballItem.spawnPrefab;
        LethalLib.Modules.Items.RegisterShopItem(BallItem, price: ModConfig.Instance.values.Tier1BallCost,
            itemInfo: Utils.CreateTerminalNode("Pokeball",
                "Use it to capture monsters. First ball tier with a low capture rate."));
        LethalMon.Log("Pokeball added to shop");
    }
}