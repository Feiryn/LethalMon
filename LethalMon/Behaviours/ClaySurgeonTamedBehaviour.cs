using System;
using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using LethalMon.Behaviours.ClaySurgeon;
using UnityEngine;

namespace LethalMon.Behaviours;

internal class ClaySurgeonTamedBehaviour : TamedEnemyBehaviour
{
    private static GameObject? WallCrackPrefab = null;

    #region Properties

    private ClaySurgeonAI? _claySurgeon = null;

    internal ClaySurgeonAI ClaySurgeon
    {
        get
        {
            if (_claySurgeon == null)
                _claySurgeon = (Enemy as ClaySurgeonAI)!;

            return _claySurgeon;
        }
    }

    public override bool CanDefend => false;
    
    internal GameObject? WallCrack = null;
    #endregion

    #region Cooldowns

    private const string CooldownId = "claysurgeon_cutwall";

    public override Cooldown[] Cooldowns => [new Cooldown(CooldownId, "Cut wall", 1f)];

    private CooldownNetworkBehaviour? cooldown;

    #endregion

    #region Custom behaviours

    internal enum CustomBehaviour
    {
        CutWall = 1
    }

    public override List<Tuple<string, string, Action>>? CustomBehaviourHandler =>
    [
        new(CustomBehaviour.CutWall.ToString(), "Is cutting a wall...", OnCutWall)
    ];

    internal void OnCutWall()
    {
    }

    #endregion

    #region Action Keys

    private readonly List<ActionKey> _actionKeys =
    [
        new ActionKey() { Key = ModConfig.Instance.ActionKey1, Description = "Cut wall" }
    ];

    public override List<ActionKey> ActionKeys => _actionKeys;

    public override void ActionKey1Pressed()
    {
        base.ActionKey1Pressed();

        SwitchToCustomBehaviour((int)CustomBehaviour.CutWall);
    }

    #endregion

    #region Base Methods

    public override void Start()
    {
        base.Start();

        cooldown = GetCooldownWithId(CooldownId);
    }

    public override void InitTamingBehaviour(TamingBehaviour behaviour)
    {
        base.InitTamingBehaviour(behaviour);

        if (behaviour == TamingBehaviour.TamedFollowing)
        {
            EnableActionKeyControlTip(ModConfig.Instance.ActionKey1,
                true); // todo montrer que quand le cooldown est terminé
        }
    }

    public override void LeaveTamingBehaviour(TamingBehaviour behaviour)
    {
        base.LeaveTamingBehaviour(behaviour);

        if (behaviour == TamingBehaviour.TamedFollowing)
        {
            EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, false);
        }
    }

    public override void InitCustomBehaviour(int behaviour)
    {
        base.InitCustomBehaviour(behaviour);

        if (behaviour == (int)CustomBehaviour.CutWall)
        {
            StartCoroutine(CutWallCoroutine());
        }
    }

    public override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
    {
        // ANY CLIENT
        base.OnEscapedFromBall(playerWhoThrewBall);
    }

    public override void OnUpdate(bool update = false, bool doAIInterval = true)
    {
        // ANY CLIENT
        base.OnUpdate(update, doAIInterval);
    }

    public override bool CanBeTeleported()
    {
        return CurrentTamingBehaviour == TamingBehaviour.TamedFollowing;
    }

    #endregion

    public override void OnDestroy()
    {
        /*
        if (WallCrack != null)
            Destroy(WallCrack);
            */
        
        base.OnDestroy();
    }

    #region Methods

    private IEnumerator CutWallCoroutine()
    {
        WallCrack = Instantiate(WallCrackPrefab!, ClaySurgeon.transform.position, Quaternion.identity);
        
        Camera gameplayCamera = Utils.CurrentPlayer.gameplayCamera;
        
        Camera cameraA = WallCrack.transform.Find("CrackA").GetComponentInChildren<Camera>();
        Camera cameraB = WallCrack.transform.Find("CrackB").GetComponentInChildren<Camera>();
        
        cameraA.fieldOfView = gameplayCamera.fieldOfView;
        cameraA.nearClipPlane = gameplayCamera.nearClipPlane;
        cameraA.farClipPlane = gameplayCamera.farClipPlane;
        cameraA.cullingMask = gameplayCamera.cullingMask;
        cameraA.targetTexture.Release();
        cameraA.targetTexture.width = gameplayCamera.targetTexture.width;
        cameraA.targetTexture.height = gameplayCamera.targetTexture.height;
        Portal portalA = cameraA.gameObject.AddComponent<Portal>();
        portalA.playerCamera = gameplayCamera;
        portalA.portal = WallCrack.transform.Find("CrackA");
        portalA.otherPortal = WallCrack.transform.Find("CrackB");
        portalA.portalCamera = cameraB;
        

        cameraB.fieldOfView = gameplayCamera.fieldOfView;
        cameraB.nearClipPlane = gameplayCamera.nearClipPlane;
        cameraB.farClipPlane = gameplayCamera.farClipPlane;
        cameraB.cullingMask = gameplayCamera.cullingMask;
        cameraB.targetTexture.Release();
        cameraB.targetTexture.width = Utils.CurrentPlayer.gameplayCamera.targetTexture.width;
        cameraB.targetTexture.height = Utils.CurrentPlayer.gameplayCamera.targetTexture.height;
        Portal portalB = cameraB.gameObject.AddComponent<Portal>();
        portalB.playerCamera = gameplayCamera;
        portalB.portal = WallCrack.transform.Find("CrackB");
        portalB.otherPortal = WallCrack.transform.Find("CrackA");
        portalB.portalCamera = cameraA;
            
        SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
        yield return null;
    }

    internal static void LoadAssets(AssetBundle assetBundle)
    {
        WallCrackPrefab = assetBundle.LoadAsset<GameObject>("Assets/Enemies/Barber/WallCrack.prefab");
    }
    #endregion
}