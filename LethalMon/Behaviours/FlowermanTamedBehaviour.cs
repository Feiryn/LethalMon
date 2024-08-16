using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GameNetcodeStuff;
using LethalMon.Items;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LethalMon.Behaviours;

public class FlowermanTamedBehaviour : TamedEnemyBehaviour
{
    #region Properties
    private FlowermanAI? _bracken = null;
    internal FlowermanAI Bracken
    {
        get
        {
            if (_bracken == null)
                _bracken = (Enemy as FlowermanAI)!;

            return _bracken;
        }
    }

    private EnemyAI? _grabbedEnemyAi;

    // Left arm
    private Transform? _arm1L;
    
    private Transform? _arm2L;
    
    private Transform? _arm3L;
    
    private Transform? _hand1L;
    
    // Right arm
    private Transform? _arm1R;
    
    private Transform? _arm2R;
    
    private Transform? _arm3R;
    
    private Transform? _hand1R;

    // Grabbed monsters positions (height, distance, rotation)
    private static readonly Dictionary<string, Tuple<float, float, Quaternion>> GrabbedMonstersPositions = new()
    {
        // todo sand spider get teleported once release
        // { nameof(SandSpiderAI), new Tuple<float, float, Quaternion>(2, 1, Quaternion.Euler(-75, 0, 0)) },
        { nameof(SpringManAI), new Tuple<float, float, Quaternion>(0.5f, 0, Quaternion.Euler(15, 0, 0)) },
        { nameof(FlowermanAI), new Tuple<float, float, Quaternion>(0.5f, 0.2f, Quaternion.Euler(15, 0, 0)) },
        { nameof(CrawlerAI), new Tuple<float, float, Quaternion>(2, 1.2f, Quaternion.Euler(-60, 0, 0)) },
        { nameof(HoarderBugAI), new Tuple<float, float, Quaternion>(1.5f, 0.3f, Quaternion.Euler(15, 0, 0)) },
        { nameof(CentipedeAI), new Tuple<float, float, Quaternion>(2.3f, 0.8f, Quaternion.Euler(-75, 0, 0)) },
        { nameof(PufferAI), new Tuple<float, float, Quaternion>(2.3f, 0.1f, Quaternion.Euler(-75, 0, 180)) },
        { nameof(JesterAI), new Tuple<float, float, Quaternion>(0.5f, 0.1f, Quaternion.Euler(15, 0, 0)) },
        { nameof(NutcrackerEnemyAI), new Tuple<float, float, Quaternion>(0.5f, 0.1f, Quaternion.Euler(15, 0, 0)) },
        { nameof(MaskedPlayerEnemy), new Tuple<float, float, Quaternion>(0.5f, 0.1f, Quaternion.Euler(15, 0, 0)) },
        { nameof(ButlerEnemyAI), new Tuple<float, float, Quaternion>(0.5f, 0.5f, Quaternion.Euler(15, 0, 0)) }
    };
    
    // Grabbed monsters before grab functions
    private static readonly Dictionary<string, Action<EnemyAI>> BeforeGrabFunctions = new()
    {
        {
            nameof(HoarderBugAI), (enemyAI) =>
            {
                // Calm down or the position will be too high as it flies
                HoarderBugAI hoarderBugAI = (HoarderBugAI)enemyAI;
                if (hoarderBugAI.isAngry)
                {
                    hoarderBugAI.SwitchToBehaviourState(0);
                    hoarderBugAI.ExitChaseMode();
                }
            }
        },
        {
            nameof(CentipedeAI), (enemyAI) =>
            {
                CentipedeAI centipedeAI = (CentipedeAI)enemyAI;
                if (centipedeAI.clingingToPlayer != null)
                {
                    centipedeAI.StopClingingToPlayer(false);
                }
            }
        }
    };

    private const float MaximumDistanceTowardsOwner = 50f; // Distance, after which the grabbed enemy will be dropped in order to return to the owner

    internal override string DefendingBehaviourDescription => "Saw an enemy to drag away!";

    #endregion
    
    #region Custom behaviours
    internal enum CustomBehaviour
    {
        DragEnemyAway = 1,
        GoesBackToOwner
    }
    
    internal override List<Tuple<string, string, Action>>? CustomBehaviourHandler =>
    [
        new (CustomBehaviour.DragEnemyAway.ToString(), "Drags an enemy away...", OnDragEnemyAway),
        new (CustomBehaviour.GoesBackToOwner.ToString(), "Walks back to you...", OnWalkBackToOwner)
    ];

    public void OnDragEnemyAway()
    {
        if (_grabbedEnemyAi == null)
        {
            SwitchToCustomBehaviour((int) CustomBehaviour.GoesBackToOwner);
            return;
        }
        
        if (Vector3.Distance(Bracken.transform.position, Bracken.destination) < 2f || DistanceToOwner > MaximumDistanceTowardsOwner)
        {
            LethalMon.Log("Enemy brought to destination or far enough away from owner, release it. Distance to owner: " + DistanceToOwner);

            ReleaseEnemy();
            ReleaseEnemyServerRpc();

            SwitchToCustomBehaviour((int) CustomBehaviour.GoesBackToOwner);
        }
        else
        {
            // As the Bracken cannot get too close of the door as it has an enemy in its arm, we also open doors around the grabbed enemy
            Utils.OpenDoorsAsEnemyAroundPosition(_grabbedEnemyAi.transform.position);
        }

        //LethalMon.Log("Enemy already grabbed and moving, skip AI interval");
    }

    public void OnWalkBackToOwner()
    {
        if (ownerPlayer == null || ownerPlayer.isPlayerDead
                                || DistanceToOwner < 8f // Reached owner
                                || !ownerPlayer.isInsideFactory)
        {
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
            return;
        }
        
        Bracken.SetDestinationToPosition(ownerPlayer.transform.position);
    }
    
    internal override void InitCustomBehaviour(int behaviour)
    {
        base.InitCustomBehaviour(behaviour);

        if((CustomBehaviour)behaviour == CustomBehaviour.GoesBackToOwner)
            CalmDown();
    }
    #endregion
    
    #region Cooldowns

    private const string GrabCooldownId = "Bracken_grab";
    
    internal override Cooldown[] Cooldowns => [new Cooldown(GrabCooldownId, "Grab enemy", ModConfig.Instance.values.BrackenGrabCooldown)];

    private readonly CooldownNetworkBehaviour grabCooldown;

    internal override bool CanDefend => grabCooldown.IsFinished();
    #endregion

    #region Base Methods
    FlowermanTamedBehaviour() => grabCooldown = GetCooldownWithId(GrabCooldownId);
    internal override void Start()
    {
        base.Start();
        
        if (IsTamed)
        {
            Bracken.creatureAnimator.SetBool("sneak", value: true);
            Bracken.creatureAnimator.Play("Base Layer.CreepForward");
        }

        var torso3 = Bracken.gameObject.transform
            .Find("FlowermanModel")?
            .Find("AnimContainer")?
            .Find("metarig")?
            .Find("Torso1")?
            .Find("Torso2")?
            .Find("Torso3");

        if (torso3 != null)
        {
            _arm1L = torso3.Find("Arm1.L");
            _arm2L = _arm1L.Find("Arm2.L");
            _arm3L = _arm2L.Find("Arm3.L");
            _hand1L = _arm3L.Find("Hand1.L");

            _arm1R = torso3.Find("Arm1.R");
            _arm2R = _arm1R.Find("Arm2.R");
            _arm3R = _arm2R.Find("Arm3.R");
            _hand1R = _arm3R.Find("Hand1.R");
        }
    }

    internal override void LateUpdate()
    {
        base.LateUpdate();

        if (_grabbedEnemyAi != null)
            SetArmsInHoldPosition();
    }

    internal override void OnTamedFollowing()
    {
        base.OnTamedFollowing();

        if (grabCooldown.IsFinished())
            TargetNearestEnemy();
    }

    internal override void OnTamedDefending()
    {
        if (!HasTargetEnemy)
        {
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
            return;
        }
        
        if (targetEnemy!.isEnemyDead)
        {
            LethalMon.Log("Target is dead, stop targeting it");
            targetEnemy = null;
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);

            return;
        }

        if (targetEnemy.meshRenderers.Any(meshRendererTarget => Bracken.meshRenderers.Any(meshRendererSelf => meshRendererSelf.bounds.Intersects(meshRendererTarget.bounds))))
        {
            LethalMon.Log("Collided with target, grab it");

            EnemyAI enemyToGrab = targetEnemy;
            GrabEnemy(enemyToGrab);
            StandUp();
            
            GrabEnemyServerRpc(enemyToGrab.GetComponent<NetworkObject>());
        }
        else
        {
            LethalMon.Log("Moving to target");

            Bracken.SetDestinationToPosition(targetEnemy.transform.position);
        }
        
    }

    internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
    {
        base.OnEscapedFromBall(playerWhoThrewBall);

        if (Utils.IsHost)
            Bracken.AddToAngerMeter(float.MaxValue);
    }

    internal override void DoAIInterval()
    {
        base.DoAIInterval();
    }

    internal override void OnUpdate(bool update = false, bool doAIInterval = true)
    {
        base.OnUpdate(update, false);

        Bracken.creatureAnimator.SetFloat("speedMultiplier", Vector3.ClampMagnitude(Bracken.transform.position - Bracken.previousPosition, 1f).sqrMagnitude / (Time.deltaTime / 4f));
        Bracken.CalculateAnimationDirection();
    }

    public override void OnDestroy()
    {
        ReleaseEnemy();
        
        base.OnDestroy();
    }

    public override bool CanBeTeleported() => _grabbedEnemyAi == null;

    #endregion

    #region Methods
    internal override void InitTamingBehaviour(TamingBehaviour behaviour)
    {
        base.InitTamingBehaviour(behaviour);

        switch(behaviour)
        {
            case TamingBehaviour.TamedFollowing:
                CalmDown();
                break;
            case TamingBehaviour.TamedDefending:
                StandUp();
                break;
            default: break;
        }
    }

    public void StandUp()
    {
        Bracken.creatureAngerVoice.Play();
        Bracken.creatureAngerVoice.pitch = Random.Range(0.9f, 1.3f);
        Bracken.creatureAnimator.SetBool("anger", true);
        Bracken.creatureAnimator.SetBool("sneak", false);
    }

    public void CalmDown()
    {
        if (Bracken != null)
        {
            Bracken.creatureAngerVoice.Stop();
            Bracken.creatureAnimator.SetBool("sneak", true);
            Bracken.creatureAnimator.SetBool("anger", false);
        }
    }

    public void GrabEnemy(EnemyAI enemyAI)
    {
        if (_grabbedEnemyAi != null)
        {
            this.ReleaseEnemy();
            ReleaseEnemyServerRpc();
        }

        Bracken.creatureAngerVoice.Stop();

        if (BeforeGrabFunctions.TryGetValue(enemyAI.GetType().Name, out var beforeGrabFunction))
        {
            beforeGrabFunction.Invoke(enemyAI);
        }

        PlaceEnemyAiInBrackenHands(enemyAI);
        targetEnemy = null;

        var farthestPosition = Bracken.ChooseFarthestNodeFromPosition(enemyAI.transform.position).position;
        Bracken.SetDestinationToPosition(farthestPosition);
        grabCooldown.Reset();
        grabCooldown.Pause();
        LethalMon.Log("Moving to " + farthestPosition);
        
        SwitchToCustomBehaviour((int) CustomBehaviour.DragEnemyAway);
    }

    public void PlaceEnemyAiInBrackenHands(EnemyAI enemyAI)
    {
        enemyAI.enabled = false;
        enemyAI.agent.enabled = false;
        var enemyAiTransform = enemyAI.transform;
        var flowermanTransform = Bracken.transform;
        enemyAiTransform.transform.SetParent(flowermanTransform);

        if (GrabbedMonstersPositions.TryGetValue(enemyAI.GetType().Name, out var monsterPositions))
        {
            enemyAiTransform.localPosition = Vector3.up * monsterPositions.Item1 + Vector3.forward * monsterPositions.Item2;
            enemyAiTransform.localRotation = monsterPositions.Item3;
        }

        _grabbedEnemyAi = enemyAI;
    }

    public void ReleaseEnemy()
    {
        grabCooldown.Resume();
        
        if (_grabbedEnemyAi == null) return;

        Transform enemyAiTransform = _grabbedEnemyAi.transform;
        enemyAiTransform.SetParent(null);
        var selfTransform = Bracken.transform;
        enemyAiTransform.localPosition = selfTransform.localPosition;
        enemyAiTransform.position = selfTransform.position;
        enemyAiTransform.rotation = selfTransform.rotation;
        enemyAiTransform.localRotation = selfTransform.localRotation;
        _grabbedEnemyAi.enabled = true;
        _grabbedEnemyAi.agent.enabled = true;
        TeleportEnemy(_grabbedEnemyAi, selfTransform.position);
        _grabbedEnemyAi = null;
            
        LethalMon.Log("Enemy release");
    }

    private void SetArmsInHoldPosition()
    {
        if (_arm1L != null) _arm1L.localRotation = Quaternion.Euler(-115.4f, -103.6f, -162.8f);
        if (_arm2L != null) _arm2L.localRotation = Quaternion.Euler(-15.3f, 0.4f, 37.87f);
        if (_arm3L != null) _arm3L.localRotation = Quaternion.Euler(-88.09f, 93.4f, 8.3f);
        if (_hand1L != null) _hand1L.localRotation = Quaternion.Euler(-22.3f, 0f, 0f);

        if (_arm1R != null) _arm1R.localRotation = Quaternion.Euler(-81.5f, 88.9f, -553.6f);
        if (_arm2R != null) _arm2R.localRotation = Quaternion.Euler(-50.7f, -92.46f, 6f);
        if (_arm3R != null) _arm3R.localRotation = Quaternion.Euler(-50.6f, 5.84f, 0f);
        if (_hand1R != null) _hand1R.localRotation = Quaternion.Euler(-69.2f, 0f, 0f);
    }
    #endregion
    
    #region RPCs
    [ServerRpc(RequireOwnership = false)]
    public void GrabEnemyServerRpc(NetworkObjectReference enemyAiRef)
    {
        GrabEnemyClientRpc(enemyAiRef);
    }
    
    [ClientRpc]
    public void GrabEnemyClientRpc(NetworkObjectReference enemyAiRef)
    {
        if (!enemyAiRef.TryGet(out var networkObject))
        {
            LethalMon.Log(base.gameObject.name + ": Failed to get network object from network object reference (Grab item RPC)", LethalMon.LogType.Error);
            return;
        }
        
        PlaceEnemyAiInBrackenHands(networkObject.GetComponent<EnemyAI>());
        StandUp();
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void ReleaseEnemyServerRpc()
    {
        ReleaseEnemyClientRpc();
    }
    
    [ClientRpc]
    public void ReleaseEnemyClientRpc()
    {
        ReleaseEnemy();
    }
    #endregion
}