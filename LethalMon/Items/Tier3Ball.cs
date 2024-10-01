using UnityEngine;

namespace LethalMon.Items;

public class Tier3Ball : BallItem
{
    public static GameObject? SpawnPrefab = null;

    public static Item? BallItem = null;
    
    public static string BallName = "Tier 3 Ball";
    
    public Tier3Ball() : base(BallType.TIER3, 2)
    {
    }
    
    internal static void Setup(AssetBundle assetBundle)
    {
        var ballItem = InitBallPrefab<Tier3Ball>(assetBundle, "Tier3Capsule/Tier3Capsule.asset", ModConfig.Instance.values.Tier3BallSpawnWeight);

        if (ballItem == null) return;
        
        BallItem = ballItem;
        SpawnPrefab = ballItem.spawnPrefab;
        
        if (SpawnPrefab.TryGetComponent(out GrabbableObject grabbable))
        {
            BallName = grabbable.itemProperties.itemName;
            
            LethalLib.Modules.Items.RegisterShopItem(BallItem, price: ModConfig.Instance.values.Tier3BallCost,
                itemInfo: Utils.CreateTerminalNode(grabbable.itemProperties.itemName,
                    "Use it to capture monsters. Third ball tier with a high capture rate."));
            LethalMon.Log(grabbable.itemProperties.itemName + " added to shop");
        }
        else
        {
            LethalMon.Log("Failed to add " + nameof(Tier3Ball) +
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