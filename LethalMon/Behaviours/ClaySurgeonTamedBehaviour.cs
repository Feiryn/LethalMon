using System;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.Behaviours;

internal class ClaySurgeonTamedBehaviour : TamedEnemyBehaviour
{
    private static GameObject? _wallCrackPrefab = null;
    
    private static AudioClip? _teleportSfx = null;
    
    internal static List<Tuple<Vector3, Quaternion>>? WallPositions = null;
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
    
    internal GameObject? WallCrackA = null;
    
    internal GameObject? WallCrackB = null;
    
    private Vector3? _cutWallPosition;
    
    private Tuple<Vector3, Quaternion>? _cutWallClosest;
    
    private Tuple<Vector3, Quaternion>? _cutWallRandom;
    
    private static readonly int Snip = Animator.StringToHash("snip");
    #endregion

    #region Cooldowns

    private const string CooldownId = "claysurgeon_cutwall";

    public override Cooldown[] Cooldowns => [new Cooldown(CooldownId, "Cut wall", ModConfig.Instance.values.BarberCutWallCooldown)];

    private CooldownNetworkBehaviour? _cooldown;
    #endregion

    #region Custom behaviours

    internal enum CustomBehaviour
    {
        CutWall = 1
    }

    public override List<Tuple<string, string, Action>> CustomBehaviourHandler =>
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
        new ActionKey { Key = ModConfig.Instance.ActionKey1, Description = "Cut wall" }
    ];

    public override List<ActionKey> ActionKeys => _actionKeys;

    public override void ActionKey1Pressed()
    {
        base.ActionKey1Pressed();

        if (!_cooldown!.IsFinished() || CurrentTamingBehaviour != TamingBehaviour.TamedFollowing || WallPositions == null || WallPositions.Count == 0)
            return;
        
        _cutWallClosest = WallPositions!.OrderBy(wallPosition => Vector3.Distance(wallPosition.Item1, ownerPlayer!.transform.position)).First();
        _cutWallRandom = WallPositions!.OrderBy(_ => UnityEngine.Random.value).First();
        
        _cutWallPosition = RoundManager.Instance.GetNavMeshPosition( _cutWallClosest.Item1);
        MoveTowards(_cutWallPosition!.Value);
        
        SwitchToCustomBehaviour((int)CustomBehaviour.CutWall);
    }

    #endregion

    #region Base Methods

    public override void Start()
    {
        base.Start();

        _cooldown = GetCooldownWithId(CooldownId);

        if (IsTamed)
        {
            // ClaySurgeon doesn't have any behaviour state, so it doesn't switch to following when invoked, and the AI is disabled by SwitchToTamingBehaviour
            // So it needs to be done manually
            ClaySurgeon.enabled = false;
            ClaySurgeon.agent.speed = 0f;
            ClaySurgeon.transform.localScale *= 0.8f;
        }
        else
        {
            // The collider is only on the scissors, so a new one is added
            // Collision detection is on the mesh container, so it can be added to the root object
            var boxCollider = ClaySurgeon.gameObject.AddComponent<BoxCollider>();
            boxCollider.center = Vector3.up * 1.7f;
            boxCollider.size = new Vector3(2.24f, 3.4f, 1.66f);
            boxCollider.isTrigger = true;
            boxCollider.providesContacts = false;
            
            // A rigid must also be added to the root object to detect collisions
            var rigidBody = ClaySurgeon.gameObject.AddComponent<Rigidbody>();
            rigidBody.isKinematic = false;
            rigidBody.useGravity = false;
            rigidBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            
            // Set the door speed to 0, so it can't open doors with its new rigidbody
            ClaySurgeon.openDoorSpeedMultiplier = 0f;
        }
    }

    public override void InitTamingBehaviour(TamingBehaviour behaviour)
    {
        base.InitTamingBehaviour(behaviour);

        if (behaviour == TamingBehaviour.TamedFollowing)
        {
            EnableActionKeyControlTip(ModConfig.Instance.ActionKey1,
                true);
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

    public override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
    {
        // ANY CLIENT
        base.OnEscapedFromBall(playerWhoThrewBall);

        foreach (var claySurgeonAI in FindObjectsByType<ClaySurgeonAI>(FindObjectsSortMode.None))
        {
            claySurgeonAI.currentInterval = claySurgeonAI.endingInterval / 3f;
        }
    }

    public override void OnUpdate(bool update = false, bool doAIInterval = true)
    {
        base.OnUpdate(update, false);
        
        if (ClaySurgeon.isJumping)
        {
            ClaySurgeon.jumpTimer -= Time.deltaTime;
            if (ClaySurgeon.jumpTimer <= 0f)
            {
                ClaySurgeon.isJumping = false;
                ClaySurgeon.agent.speed = 0f;
            }
            else
            {
                ClaySurgeon.agent.speed = ClaySurgeon.jumpSpeed;
            }
        }
        
        if (ClaySurgeon.beatTimer <= 0f)
        {
            ClaySurgeon.beatTimer = ClaySurgeon.endingInterval; // Changed from vanilla => always max speed
            
            if (ClaySurgeon.agent.destination != ClaySurgeon.transform.position)
            {
                ClaySurgeon.DanceBeat();
            }
        }
        else
        {
            ClaySurgeon.beatTimer -= Time.deltaTime;
        }
        
        if (Utils.IsHost && CurrentCustomBehaviour == (int)CustomBehaviour.CutWall)
        {
            if (_cutWallPosition == null)
            {
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                return;
            }
            
            if (Vector3.Distance(ClaySurgeon.transform.position, _cutWallPosition.Value) < 3f)
            {
                CutWallServerRpc(_cutWallClosest!.Item1, _cutWallClosest!.Item2, _cutWallRandom!.Item1, _cutWallRandom!.Item2);
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
            }
        }
    }

    public override bool CanBeTeleported()
    {
        return CurrentTamingBehaviour == TamingBehaviour.TamedFollowing;
    }

    #endregion

    public override void OnDestroy()
    {
        if (WallCrackA != null)
            Destroy(WallCrackA);
        
        if (WallCrackB != null)
            Destroy(WallCrackB);
        
        base.OnDestroy();
    }

    #region Methods
    private void CutWalls(Vector3 positionA, Quaternion rotationA, Vector3 positionB, Quaternion rotationB)
    {
        if (Utils.IsHost)
            _cooldown!.Reset();
        
        ClaySurgeon.creatureAnimator.SetTrigger(Snip);
        ClaySurgeon.creatureSFX.PlayOneShot(ClaySurgeon.snipScissors);
        
        if (WallCrackA != null)
            Destroy(WallCrackA);
        if (WallCrackB != null)
            Destroy(WallCrackB);
        
        WallCrackA = Instantiate(_wallCrackPrefab!, positionA - Vector3.up, rotationA);
        WallCrackB = Instantiate(_wallCrackPrefab!, positionB- Vector3.up, rotationB);

        var wallCrackAScript = WallCrackA.AddComponent<WallCrack>();
        var wallCrackBScript = WallCrackB.AddComponent<WallCrack>();
        
        wallCrackAScript.OtherWallCrack = wallCrackBScript;
        wallCrackBScript.OtherWallCrack = wallCrackAScript;
    }

    internal static void LoadAssets(AssetBundle assetBundle)
    {
        _wallCrackPrefab = assetBundle.LoadAsset<GameObject>("Assets/Enemies/Barber/WallCrack.prefab");
        _teleportSfx = assetBundle.LoadAsset<AudioClip>("Assets/Enemies/Barber/TeleportSound.ogg");
    }
    #endregion
    
    #region RPCs

    [ServerRpc(RequireOwnership = false)]
    private void CutWallServerRpc(Vector3 positionA, Quaternion rotationA, Vector3 positionB, Quaternion rotationB)
    {
        CutWallClientRpc(positionA, rotationA, positionB, rotationB);
    }
    
    [ClientRpc]
    private void CutWallClientRpc(Vector3 positionA, Quaternion rotationA, Vector3 positionB, Quaternion rotationB)
    {
        CutWalls(positionA, rotationA, positionB, rotationB);
    }
    #endregion

    internal class WallCrack : MonoBehaviour
    {
        internal WallCrack? OtherWallCrack;
        
        private AudioSource? _audioSource;
        
        private readonly HashSet<int> _preventEnemiesTp = [];
        
        private bool _preventPlayerTp = false;

        private void Start()
        {
            _audioSource = gameObject.GetComponent<AudioSource>();
        }

        private void PlayTeleportSound()
        {
            _audioSource!.PlayOneShot(_teleportSfx);
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (OtherWallCrack == null)
                return;
            
            PlayerControllerB? player = Cache.GetPlayerFromCollider(other);
            if (player != null && player == Utils.CurrentPlayer && !_preventPlayerTp)
            {
                OtherWallCrack._preventPlayerTp = true;
                player.TeleportPlayer(OtherWallCrack.transform.position + OtherWallCrack.transform.forward, true,
                    OtherWallCrack.transform.eulerAngles.y);
                PlayTeleportSound();
                OtherWallCrack.PlayTeleportSound();
                StartCoroutine(Utils.CallAfterTimeCoroutine(() => OtherWallCrack._preventPlayerTp = false, 1f));
                return;
            }

            if (Utils.IsHost)
            {
                EnemyAI? enemy = Cache.GetEnemyFromCollider(other);
                if (enemy != null && Cache.GetTamedEnemyBehaviour(enemy)?.IsTamed != true && !_preventEnemiesTp.Contains(enemy.GetInstanceID()))
                {
                    OtherWallCrack._preventEnemiesTp.Add(enemy.GetInstanceID());
                    TeleportEnemy(enemy,
                        OtherWallCrack.transform.position + OtherWallCrack.transform.forward, true, true);
                    PlayTeleportSound();
                    OtherWallCrack.PlayTeleportSound();
                    StartCoroutine(Utils.CallAfterTimeCoroutine(() => OtherWallCrack._preventEnemiesTp.Remove(enemy.GetInstanceID()), 1f));
                }
            }
        }
    }
    
    // todo test tp sound range
    // todo fix jump syncing between clients
    // todo fix standing jumps on clients
    // todo sync tp sounds between all clients
    // todo fix switch from cut wall to following on clients (because of destination = null on host)
}