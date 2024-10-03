using System.Collections.Generic;
using System.Linq;
using LethalMon.Behaviours;
using UnityEngine;
using UnityEngine.Animations.Rigging;

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

    private Transform _coneTransform;

    private GameObject? _currentEnemyModel;
    
    public override void Start()
    {
        base.Start();
        
        _animator = GetComponent<Animator>();
        _audioSource = gameObject.GetComponent<AudioSource>();
        //_captureSuccessSound = Utils.GetTeleportClick();
        _coneTransform = transform.Find("model/Armature/root/cone");
    }

    private void InstantiateEnemyModel(string enemyType)
    {
        if (_currentEnemyModel != null)
        {
            Destroy(_currentEnemyModel);
        }
        
        GameObject? originalPrefab = Utils.GetEnemyPrefab(enemyType);
        
        if (originalPrefab == null)
        {
            LethalMon.Log("Failed to get enemy prefab for " + enemyType, LethalMon.LogType.Error);
            return;
        }
        
        this.gameObject.SetActive(false);
        
        _currentEnemyModel = Object.Instantiate(originalPrefab, this._coneTransform);
        
        var components = _currentEnemyModel.GetComponentsInChildren<Component>().ToList();
        foreach (var component in components)
        {
            if (component is Transform or MeshRenderer or MeshFilter or SkinnedMeshRenderer or Animator or AudioSource)
            {
                continue;
            }

            Destroy(component);
        }
        
        this.gameObject.SetActive(true);
        
        _currentEnemyModel.transform.localPosition = Vector3.up * 0.9f; 
        _currentEnemyModel.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        _currentEnemyModel.transform.localScale = Vector3.one * 0.075f;
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

    public override void SetCaughtEnemy(EnemyType enemyType, string enemySkinRegistryId)
    {
        base.SetCaughtEnemy(enemyType, enemySkinRegistryId);
        
        InstantiateEnemyModel(enemyType.name);
    }

    #endregion
}