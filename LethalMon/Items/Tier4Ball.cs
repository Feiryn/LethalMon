using UnityEngine;

namespace LethalMon.Items;

public class Tier4Ball : BallItem
{
    public static GameObject? SpawnPrefab = null;

    public static Item? BallItem = null;
    
    public static string BallName = "Tier 4 Ball";

    public Tier4Ball() : base(BallType.TIER4, 3)
    {
    }
    
    internal static void Setup(AssetBundle assetBundle)
    {
        var ballItem = InitBallPrefab<Tier4Ball>(assetBundle, "Tier4Capsule/Tier4Capsule.asset", ModConfig.Instance.values.Tier4BallSpawnWeight);

        if (ballItem == null) return;

        BallItem = ballItem;
        SpawnPrefab = ballItem.spawnPrefab;
        
        if (SpawnPrefab.TryGetComponent(out GrabbableObject grabbable))
        {
            BallName = grabbable.itemProperties.itemName;
            
            LethalLib.Modules.Items.RegisterShopItem(BallItem, price: ModConfig.Instance.values.Tier4BallCost,
                itemInfo: Utils.CreateTerminalNode(grabbable.itemProperties.itemName,
                    "Use it to capture monsters. Fourth ball tier with a 100% capture rate."));
            LethalMon.Log(grabbable.itemProperties.itemName + " added to shop");
        }
        else
        {
            LethalMon.Log("Failed to add " + nameof(Tier4Ball) +
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