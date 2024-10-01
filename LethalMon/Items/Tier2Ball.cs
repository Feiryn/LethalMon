using UnityEngine;

namespace LethalMon.Items;

public class Tier2Ball : BallItem
{
    public static GameObject? SpawnPrefab = null;

    public static Item? BallItem = null;
    
    public static string BallName = "Tier 2 Ball";
    
    public Tier2Ball() : base(BallType.TIER2, 1)
    {
    }

    internal static void Setup(AssetBundle assetBundle)
    {
        var ballItem = InitBallPrefab<Tier2Ball>(assetBundle, "Tier2Capsule/Tier2Capsule.asset",
            ModConfig.Instance.values.Tier2BallSpawnWeight);

        if (ballItem == null) return;

        BallItem = ballItem;
        SpawnPrefab = ballItem.spawnPrefab;

        if (SpawnPrefab.TryGetComponent(out GrabbableObject grabbable))
        {
            BallName = grabbable.itemProperties.itemName;
            
            LethalLib.Modules.Items.RegisterShopItem(BallItem, price: ModConfig.Instance.values.Tier2BallCost,
                itemInfo: Utils.CreateTerminalNode(grabbable.itemProperties.itemName,
                    "Use it to capture monsters. Second ball tier with a medium capture rate."));
            LethalMon.Log(grabbable.itemProperties.itemName + " added to shop");
        }
        else
        {
            LethalMon.Log("Failed to add " + nameof(Tier2Ball) +
                          " to shop: prefab does not have GrabbableObject component");
        }
    }

    #region Animations
    protected override void StartThrowAnimation()
    {
        throw new System.NotImplementedException();
    }

    protected override void EndThrowAnimation()
    {
        throw new System.NotImplementedException();
    }

    protected override float StartMonsterGoesInsideBallAnimation()
    {
        throw new System.NotImplementedException();
    }

    protected override void EndMonsterGoesInsideBallAnimation()
    {
        throw new System.NotImplementedException();
    }

    protected override float StartCaptureShakeAnimation()
    {
        throw new System.NotImplementedException();
    }

    protected override void EndCaptureShakeAnimation()
    {
        throw new System.NotImplementedException();
    }

    protected override float StartCaptureSuccessAnimation()
    {
        throw new System.NotImplementedException();
    }

    protected override void EndCaptureSuccessAnimation()
    {
        throw new System.NotImplementedException();
    }

    protected override float StartReleaseAnimation()
    {
        throw new System.NotImplementedException();
    }

    protected override void EndReleaseAnimation()
    {
        throw new System.NotImplementedException();
    }
    #endregion
}