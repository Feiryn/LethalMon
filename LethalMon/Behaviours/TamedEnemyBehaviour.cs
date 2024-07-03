using System;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using HarmonyLib;
using LethalLib;
using LethalMon.Items;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace LethalMon.Behaviours;

public class TamedEnemyBehaviour : NetworkBehaviour
{
    // Add your custom behaviour classes here
    internal static readonly Dictionary<Type, Type> BehaviourClassMapping = new Dictionary<Type, Type>
    {
        { typeof(FlowermanAI),      typeof(FlowermanTamedBehaviour) },
        { typeof(RedLocustBees),    typeof(RedLocustBeesTamedBehaviour) },
        { typeof(HoarderBugAI),     typeof(HoarderBugTamedBehaviour) },
        { typeof(PufferAI),         typeof(SporeLizardTamedBehaviour) }
    };

    private EnemyAI? _enemy = null;
    public EnemyAI Enemy
    {
        get
        {
            if (_enemy == null && !gameObject.TryGetComponent(out _enemy))
            {
                LethalMon.Logger.LogError("Unable to get EnemyAI for TamedEnemyBehaviour.");
                _enemy = gameObject.AddComponent<EnemyAI>();
            }

            return _enemy!;
        }
    }

    public PlayerControllerB? ownerPlayer = null;

    public EnemyAI? targetEnemy = null;

    public PlayerControllerB? targetPlayer = null;

    public ulong ownClientId;

    public BallType ballType;

    protected Vector3 previousPosition;

    public int ballValue;

    public bool scrapPersistedThroughRounds;

    public bool alreadyCollectedThisRound;

    private int _lastDefaultBehaviourIndex = -1;
    internal int LastDefaultBehaviourIndex
    {
        get
        {
            if (_lastDefaultBehaviourIndex < 0)
                _lastDefaultBehaviourIndex = LastDefaultBehaviourIndices.GetValueOrDefault(Enemy.GetType(), int.MaxValue);
            return _lastDefaultBehaviourIndex;
        }
    }

    public bool isOutsideOfBall = false;

    #region Behaviours
    public enum TamingBehaviour
    {
        TamedFollowing = 1,
        TamedDefending = 2
    }
    private readonly int tamedBehaviourCount = Enum.GetNames(typeof(TamingBehaviour)).Length - 1;

    private Dictionary<int, Action> CustomBehaviours = new Dictionary<int, Action>(); // List of behaviour state indices and their custom handler

    // Override this to add more custom behaviours to your tamed enemy
    internal virtual List<Tuple<string, Action>>? CustomBehaviourHandler => null;

    public TamingBehaviour? CurrentTamingBehaviour
    {
        get
        {
            var index = Enemy.currentBehaviourStateIndex - LastDefaultBehaviourIndex;
            return Enum.IsDefined(typeof(TamingBehaviour), index) ? (TamingBehaviour)index : null;
        }
    }
    public int? CurrentCustomBehaviour => Enemy.currentBehaviourStateIndex - LastDefaultBehaviourIndex - tamedBehaviourCount;
    public void SwitchToTamingBehaviour(TamingBehaviour behaviour)
    {
        if (CurrentTamingBehaviour == behaviour) return;

        LethalMon.Logger.LogInfo("Switch to taming state: " + behaviour.ToString());
        Enemy.SwitchToBehaviourState(LastDefaultBehaviourIndex + (int)behaviour);
        Enemy.enabled = false;
    }

    public void SwitchToCustomBehaviour(int behaviour)
    {
        if (CurrentCustomBehaviour == behaviour) return;

        LethalMon.Logger.LogInfo("Switch to custom state: " + behaviour);
        Enemy.SwitchToBehaviourState(LastDefaultBehaviourIndex + tamedBehaviourCount + behaviour);
        Enemy.enabled = false;
    }

    public void SwitchToDefaultBehaviour(int behaviour)
    {
        Enemy.SwitchToBehaviourState(behaviour);
        Enemy.enabled = behaviour > LastDefaultBehaviourIndex;
    }

    // The last vanilla behaviour index for each enemy type
    public static Dictionary<Type, int> LastDefaultBehaviourIndices = new Dictionary<Type, int>();

    // Adds the enemy behaviour classes and custom behaviours to each enemy prefab
    [HarmonyPrefix, HarmonyPatch(typeof(GameNetworkManager), "Start")]
    public static void AddTamingBehaviours()
    {
        int addedDefaultCustomBehaviours = 0, addedBehaviours = 0, enemyCount = 0;
        foreach (var enemyType in Utils.EnemyTypes)
        {
            enemyCount++;
            if (enemyType?.enemyPrefab == null || !enemyType.enemyPrefab.TryGetComponent(out EnemyAI enemyAI)) continue;

            // Behaviour controller
            var tamedBehaviourType = BehaviourClassMapping.GetValueOrDefault(enemyAI.GetType(), typeof(TamedEnemyBehaviour));
            var tamedBehaviour = enemyType.enemyPrefab.gameObject.AddComponent(tamedBehaviourType) as TamedEnemyBehaviour;
            if (tamedBehaviour == null)
            {
                LethalMon.Logger.LogWarning($"TamedEnemyBehaviour-Initialization failed for {enemyType.enemyName}");
                return;
            }

            addedBehaviours++;
            if (tamedBehaviourType == typeof(TamedEnemyBehaviour))
                addedDefaultCustomBehaviours++;
            else
                LethalMon.Logger.LogInfo($"Added {tamedBehaviourType.Name} for {enemyType.enemyName}");

            // Behaviour states
            LastDefaultBehaviourIndices.Add(enemyAI.GetType(), enemyAI.enemyBehaviourStates.Length - 1);

            var behaviourStateList = enemyAI.enemyBehaviourStates.ToList();

            // Add tamed behaviours
            foreach (var behaviourName in Enum.GetNames(typeof(TamingBehaviour)))
                behaviourStateList.Add(new EnemyBehaviourState() { name = behaviourName });

            enemyAI.enemyBehaviourStates = behaviourStateList.ToArray();
        }

        LethalMon.Logger.LogInfo($"Added {addedDefaultCustomBehaviours} more custom default behaviours. {addedBehaviours}/{enemyCount} enemy behaviours were added.");
    }

    internal void AddCustomBehaviours()
    {
        if (CustomBehaviourHandler == null || CustomBehaviourHandler.Count == 0) return;

        var behaviourStateList = Enemy.enemyBehaviourStates.ToList();
        foreach (var customBehaviour in CustomBehaviourHandler)
        {
            behaviourStateList.Add(new EnemyBehaviourState() { name = customBehaviour.Item1 });
            CustomBehaviours.Add(CustomBehaviours.Count + 1, customBehaviour.Item2);
            LethalMon.Log($"Added custom behaviour {CustomBehaviours.Count} with handler {customBehaviour.Item2.Method.Name}");
        }
        Enemy.enemyBehaviourStates = behaviourStateList.ToArray();
    }

    internal virtual void OnTamedFollowing()
    {
        FollowOwner();
    }

    internal virtual void OnTamedDefending() {
        if (targetEnemy == null && targetPlayer == null) // lost target
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
    }

    internal virtual void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall) { }
    #endregion

    #region ActionKeys
    internal virtual void ActionKey1Pressed() { }

    internal class ActionKey
    {
        internal InputAction? actionKey { get; set; } = null;
        internal string description { get; set; } = "";
        internal bool visible { get; set; } = false;

        internal string Control => actionKey == null ? "" : actionKey.bindings[StartOfRound.Instance.localPlayerUsingController ? 1 : 0].path.Split("/").Last();
        internal string ControlTip => $"{description}: [{Control}]";
    }

    /* TEMPLATE
    private List<ActionKey> _actionKeys = new List<ActionKey>()
    {
        new ActionKey() { actionKey = ModConfig.Instance.ActionKey1, description = "Event one" }
    };
    internal override List<ActionKey> ActionKeys => _actionKeys;*/
    internal virtual List<ActionKey> ActionKeys => [];

    internal void EnableActionKeyControlTip(InputAction actionKey, bool enable = true)
    {
        var keys = ActionKeys.Where((ak) => ak.actionKey == actionKey);
        if (keys.Any())
        {
            keys.First().visible = enable;
            ShowVisibleActionKeyControlTips();
        }
    }

    internal void ShowVisibleActionKeyControlTips()
    {
        HUDManager.Instance.ClearControlTips();

        var controlTips = ActionKeys.Where((ak) => ak.visible).Select((ak) => ak.ControlTip).ToArray();
        HUDManager.Instance.ChangeControlTipMultiple(
                controlTips,
                holdingItem: Utils.CurrentPlayer.currentlyHeldObjectServer != null,
                Utils.CurrentPlayer.currentlyHeldObjectServer?.itemProperties);
    }
    #endregion

    #region Base Methods
    public void Update()
    {
        var customBehaviour = Enemy.currentBehaviourStateIndex - LastDefaultBehaviourIndex;
        if (customBehaviour <= 0) return;

        //LethalMon.Logger.LogInfo($"TamedEnemyBehaviour.Update for {Enemy.name} -> {customBehaviour}");
        OnUpdate();

        if (customBehaviour > tamedBehaviourCount)
        {
            customBehaviour -= tamedBehaviourCount;
            if (CustomBehaviours.ContainsKey(customBehaviour) && CustomBehaviours[customBehaviour] != null)
                CustomBehaviours[customBehaviour]();
            else
            {
                LethalMon.Logger.LogWarning($"Custom state {customBehaviour} has no handler.");
                foreach (var b in CustomBehaviours)
                    LethalMon.Log($"Behaviour found {b.Key} with handler {b.Value.Method.Name}");
            }
            return;
        }

        switch ((TamingBehaviour)customBehaviour)
        {
            case TamingBehaviour.TamedFollowing:
                OnTamedFollowing();
                break;
            case TamingBehaviour.TamedDefending:
                OnTamedDefending();
                break;
            default: break;
        }
    }

    internal virtual void OnUpdate(bool update = false, bool doAIInterval = true)
    {
        if (StartOfRound.Instance.inShipPhase) return;

        if (update)
            Enemy.Update();

        if (doAIInterval)
        {
            if (Enemy.updateDestinationInterval >= 0f)
            {
                Enemy.updateDestinationInterval -= Time.deltaTime;
            }
            else
            {
                DoAIInterval();
                Enemy.updateDestinationInterval = Enemy.AIIntervalTime;
            }
        }
    }

    internal virtual void LateUpdate() { }

    internal virtual void Start()
    {
        LethalMon.Logger.LogInfo($"LastDefaultBehaviourIndex for {Enemy.name} is {LastDefaultBehaviourIndex}");
        AddCustomBehaviours();

        try
        {
            LethalMon.Logger.LogInfo("Set enemy variables for " + GetType().Name);
            Enemy.agent = base.gameObject.GetComponentInChildren<NavMeshAgent>();

            // todo: check if all these are needed
            Enemy.skinnedMeshRenderers = base.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            Enemy.meshRenderers = base.gameObject.GetComponentsInChildren<MeshRenderer>();
            Enemy.creatureAnimator = base.gameObject.GetComponentInChildren<Animator>();
            Enemy.thisNetworkObject = base.gameObject.GetComponentInChildren<NetworkObject>();
            Enemy.allAINodes = GameObject.FindGameObjectsWithTag("AINode");
            Enemy.path1 = new NavMeshPath();
            Enemy.openDoorSpeedMultiplier = Enemy.enemyType.doorSpeedMultiplier;
            Enemy.serverPosition = base.transform.position;
            previousPosition = base.transform.position;
            if (base.IsOwner)
            {
                Enemy.SyncPositionToClients();
            }
            else
            {
                Enemy.SetClientCalculatingAI(enable: false);
            }

            if (Enemy.creatureAnimator != null)
            {
                Enemy.creatureAnimator.SetBool("inSpawningAnimation", value: false);
            }
        }
        catch (Exception arg)
        {
            LethalMon.Log($"Error when initializing enemy variables for {base.gameObject.name} : {arg}", LethalMon.LogType.Error);
            Destroy(this);
        }
    }

    internal virtual void DoAIInterval()
    {
        if (Enemy.currentBehaviourStateIndex <= LastDefaultBehaviourIndex) return;

        if (Enemy.openDoorSpeedMultiplier > 0f)
        {
            Collider[] colliders = Physics.OverlapSphere(Enemy.transform.position, 0.5f);
            foreach (Collider collider in colliders)
            {
                DoorLock doorLock = collider.GetComponentInParent<DoorLock>();
                if (doorLock != null && !doorLock.isDoorOpened && !doorLock.isLocked)
                {
                    LethalMon.Log("Tamed enemy opens door");
                    if (doorLock.gameObject.TryGetComponent(out AnimatedObjectTrigger trigger))
                    {
                        trigger.TriggerAnimationNonPlayer(false, true, false);
                    }
                    doorLock.OpenDoorAsEnemyServerRpc();
                }
            }
        }

        Enemy.DoAIInterval();
    }
    #endregion

    #region Methods
    public void FollowOwner()
    {
        if (ownerPlayer == null) return;

        if (Vector3.Distance(Enemy.destination, ownerPlayer.transform.position) < 2f) return;

        //LethalMon.Logger.LogInfo("Follow owner");
        if (Vector3.Distance(Enemy.transform.position, ownerPlayer.transform.position) > 30f)
            TeleportBehindOwner();

        else if (FindRaySphereIntersections(Enemy.transform.position, (ownerPlayer.transform.position - Enemy.transform.position).normalized, ownerPlayer.transform.position, 5f,
                out Vector3 potentialPosition1,
                out Vector3 potentialPosition2))
        {
            var position = Enemy.transform.position;
            float distance1 = Vector3.Distance(position, potentialPosition1);
            float distance2 = Vector3.Distance(position, potentialPosition2);

            if (distance1 > 4f && distance2 > 4f)
            {
                //LethalMon.Logger.LogInfo("Following to destination");
                previousPosition = Enemy.transform.position;
                Enemy.SetDestinationToPosition(distance1 < distance2 ? potentialPosition1 : potentialPosition2);

                if (Enemy.moveTowardsDestination)
                    Enemy.agent.SetDestination(Enemy.destination);

                Enemy.SyncPositionToClients();
            }
        }

        // todo else turn in the direction of the owner
    }

    private void TeleportBehindOwner()
    {
        if (ownerPlayer == null) return;

        Enemy.agent.enabled = false;
        Enemy.transform.position = Utils.GetPositionBehindPlayer(ownerPlayer);
        Enemy.agent.enabled = true;
    }

    private static bool FindRaySphereIntersections(Vector3 rayOrigin, Vector3 rayDirection, Vector3 sphereCenter, float sphereRadius, out Vector3 intersection1, out Vector3 intersection2)
    {
        intersection1 = Vector3.zero;
        intersection2 = Vector3.zero;

        Vector3 oc = rayOrigin - sphereCenter;
        float a = Vector3.Dot(rayDirection, rayDirection);
        float b = 2.0f * Vector3.Dot(oc, rayDirection);
        float c = Vector3.Dot(oc, oc) - sphereRadius * sphereRadius;
        float discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
        {
            return false; // No intersection
        }
        else
        {
            float sqrtDiscriminant = Mathf.Sqrt(discriminant);
            float t1 = (-b - sqrtDiscriminant) / (2.0f * a);
            float t2 = (-b + sqrtDiscriminant) / (2.0f * a);

            intersection1 = rayOrigin + t1 * rayDirection;
            intersection2 = rayOrigin + t2 * rayDirection;

            return true;
        }
    }

    public virtual PokeballItem RetrieveInBall(Vector3 position)
    {
        GameObject? spawnPrefab = null;
        LethalMon.Log("balltype: " + ballType.ToString());
        switch (this.ballType)
        {
            case BallType.GREAT_BALL:
                spawnPrefab = Greatball.spawnPrefab;
                break;
            case BallType.ULTRA_BALL:
                spawnPrefab = Ultraball.spawnPrefab;
                break;
            case BallType.MASTER_BALL:
                spawnPrefab = Masterball.spawnPrefab;
                break;
            default:
                spawnPrefab = Pokeball.spawnPrefab;
                break;
        }

        if (spawnPrefab == null)
        {
            LethalMon.Log("Pokeball prefabs not loaded correctly.", LethalMon.LogType.Error);
            return null;
        }

        var ball = Instantiate(spawnPrefab, position, Quaternion.Euler(new Vector3(0, 0f, 0f)));

        PokeballItem pokeballItem = ball.GetComponent<PokeballItem>();
	    pokeballItem.fallTime = 0f;
	    pokeballItem.scrapPersistedThroughRounds = scrapPersistedThroughRounds || alreadyCollectedThisRound;
	    pokeballItem.SetScrapValue(ballValue);
	    ball.GetComponent<NetworkObject>().Spawn(false);
	    pokeballItem.SetCaughtEnemy(Enemy.enemyType);
	    pokeballItem.FallToGround();

		Enemy.GetComponent<NetworkObject>().Despawn(true);

        isOutsideOfBall = false;

        return pokeballItem;
    }
    #endregion
}