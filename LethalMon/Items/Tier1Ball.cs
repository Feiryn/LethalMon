using UnityEngine;

namespace LethalMon.Items;

public class Tier1Ball : BallItem
{
    public static GameObject? SpawnPrefab;

    public static Item? BallItem = null;
    
    public static string BallName = "Tier 1 Ball";
    
    private static AudioClip spinningSound;
    
    public Tier1Ball() : base(BallType.TIER1, 0)
    {
    }

    internal static void Setup(AssetBundle assetBundle)
    {
        var ballItem = InitBallPrefab<Tier1Ball>(assetBundle, "Tier1Capsule/Tier1Capsule.asset", ModConfig.Instance.values.Tier1BallSpawnWeight);
        spinningSound = assetBundle.LoadAsset<AudioClip>("Assets/Balls/Tier1Capsule/spinning.ogg");

        if (ballItem == null) return;
        
        BallItem = ballItem;
        SpawnPrefab = ballItem.spawnPrefab;
        
        if (SpawnPrefab.TryGetComponent(out GrabbableObject grabbable))
        {
            BallName = grabbable.itemProperties.itemName;
            
            LethalLib.Modules.Items.RegisterShopItem(BallItem, price: ModConfig.Instance.values.Tier1BallCost,
                itemInfo: Utils.CreateTerminalNode(grabbable.itemProperties.itemName,
                    "Use it to capture monsters. First ball tier with a low capture rate."));
            LethalMon.Log(grabbable.itemProperties.itemName + " added to shop");
        }
        else
        {
            LethalMon.Log("Failed to add " + nameof(Tier1Ball) + " to shop: prefab does not have GrabbableObject component");
        }
    }

    #region Animations
    private Animator _animator;
    
    private AudioSource _audioSource;
    
    private AudioClip _captureSuccessSound;
    
    public override void Start()
    {
        base.Start();
        
        _animator = GetComponent<Animator>();
        _audioSource = gameObject.GetComponent<AudioSource>();
        //_captureSuccessSound = Utils.GetTeleportClick();
    }

    protected override void StartThrowAnimation()
    {
        _animator.SetBool("Spinning", true);
        _audioSource.loop = true;
        _audioSource.clip = spinningSound;
        _audioSource.Play();
    }

    protected override void EndThrowAnimation()
    {
        _animator.SetBool("Spinning", false);
        _audioSource.loop = false;
    }

    protected override float StartMonsterGoesInsideBallAnimation()
    {
        return 1;
    }

    protected override void EndMonsterGoesInsideBallAnimation()
    {
        
    }

    protected override float StartCaptureShakeAnimation()
    {
        return 1;
    }

    protected override void EndCaptureShakeAnimation()
    {
        
    }

    protected override float StartCaptureSuccessAnimation()
    {
        return 1;
    }

    protected override void EndCaptureSuccessAnimation()
    {
        
    }

    protected override float StartReleaseAnimation()
    {
        return 1;
    }

    protected override void EndReleaseAnimation()
    {
        
    }
    #endregion
}