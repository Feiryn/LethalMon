using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using HarmonyLib;
using LethalMon.Items;
using LethalMon.Patches;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LethalMon.Behaviours;

[DisallowMultipleComponent]
public class TamedEnemyBehaviour : NetworkBehaviour
{
    internal virtual bool Controllable => false;

    internal enum TargetType
    {
        Any,
        Alive,
        Dead
    };

    internal virtual TargetType Targets => TargetType.Alive;
    internal virtual float TargetingRange => 10f;

    internal virtual Cooldown[] Cooldowns => [];

    // Add your custom behaviour classes here
    internal static readonly Dictionary<Type, Type> BehaviourClassMapping = new()
    {
        { typeof(FlowermanAI),       typeof(FlowermanTamedBehaviour) },
        { typeof(RedLocustBees),     typeof(RedLocustBeesTamedBehaviour) },
        { typeof(HoarderBugAI),      typeof(HoarderBugTamedBehaviour) },
        { typeof(PufferAI),          typeof(SporeLizardTamedBehaviour) },
        { typeof(MouthDogAI),        typeof(MouthDogTamedBehaviour) },
        { typeof(FlowerSnakeEnemy),  typeof(TulipSnakeTamedBehaviour) },
        { typeof(DressGirlAI),       typeof(GhostGirlTamedBehaviour) },
        { typeof(NutcrackerEnemyAI), typeof(NutcrackerTamedBehaviour) },
        { typeof(ButlerEnemyAI),     typeof(ButlerTamedBehaviour) },
        { typeof(BushWolfEnemy),     typeof(KidnapperFoxTamedBehaviour) },
        { typeof(CrawlerAI),         typeof(CrawlerTamedBehaviour) },
        { typeof(MaskedPlayerEnemy), typeof(MaskedTamedBehaviour) }
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

    private Canvas? _nameCanvas = null;
    private TextMeshProUGUI? _nameText = null;

    public PlayerControllerB? ownerPlayer = null;
    public ulong OwnerID => ownerPlayer != null ? ownerPlayer.playerClientId : ulong.MaxValue;
    public bool IsTamed => ownerPlayer != null;
    public bool IsOwnerPlayer => ownerPlayer == Utils.CurrentPlayer;
    public float DistanceToOwner => ownerPlayer != null ? Vector3.Distance(Enemy.transform.position, ownerPlayer.transform.position) : 0f;


    public EnemyAI? targetEnemy = null;
    public bool HasTargetEnemy => targetEnemy != null && targetEnemy.gameObject.activeSelf;
    public float DistanceToTargetEnemy => HasTargetEnemy ? Vector3.Distance(Enemy.transform.position, targetEnemy!.transform.position) : 0f;


    public PlayerControllerB? targetPlayer = null;
    public float DistanceToTargetPlayer => targetPlayer != null ? Vector3.Distance(Enemy.transform.position, targetPlayer.transform.position) : 0f;

    public BallType ballType;

    public int ballValue;

    public bool scrapPersistedThroughRounds;

    public bool alreadyCollectedThisRound;

    public bool hasBeenRetrieved = false;

    private int _lastDefaultBehaviourIndex = -1;
    internal int LastDefaultBehaviourIndex
    {
        get
        {
            if (_lastDefaultBehaviourIndex < 0)
                _lastDefaultBehaviourIndex = LastDefaultBehaviourIndices.GetValueOrDefault(Enemy.enemyType.enemyPrefab.GetInstanceID(), int.MaxValue);
            return _lastDefaultBehaviourIndex;
        }
    }

    internal float targetNearestEnemyInterval = 0f;

    #region Behaviours
    public enum TamingBehaviour
    {
        TamedFollowing = 1,
        TamedDefending = 2
    }
    public static readonly int TamedBehaviourCount = Enum.GetNames(typeof(TamingBehaviour)).Length;

    private readonly Dictionary<int, Tuple<string, Action>> CustomBehaviours = []; // List of behaviour state indices and their custom handler

    // Override this to add more custom behaviours to your tamed enemy
    internal virtual List<Tuple<string, string, Action>>? CustomBehaviourHandler => null;

    internal virtual string FollowingBehaviourDescription => "Follows you...";
    
    internal virtual string DefendingBehaviourDescription => "Defends you!";

    internal virtual bool CanDefend => true;

    internal virtual float MaxFollowDistance => 30f;

    public TamingBehaviour? CurrentTamingBehaviour
    {
        get
        {
            var index = Enemy.currentBehaviourStateIndex - LastDefaultBehaviourIndex;
            return Enum.IsDefined(typeof(TamingBehaviour), index) ? (TamingBehaviour)index : null;
        }
    }
    public int? CurrentCustomBehaviour => Enemy.currentBehaviourStateIndex - LastDefaultBehaviourIndex - TamedBehaviourCount;
    public void SwitchToTamingBehaviour(TamingBehaviour behaviour)
    {
        if (CurrentTamingBehaviour == behaviour) return;

        LethalMon.Logger.LogInfo("Switch to taming state: " + behaviour.ToString());
        Enemy.SwitchToBehaviourState(LastDefaultBehaviourIndex + (int)behaviour);
        Enemy.enabled = false;
    }
    internal virtual void InitTamingBehaviour(TamingBehaviour behaviour) {}
    internal virtual void LeaveTamingBehaviour(TamingBehaviour behaviour) {}

    public void SwitchToCustomBehaviour(int behaviour)
    {
        if (CurrentCustomBehaviour == behaviour) return;

        LethalMon.Logger.LogInfo("Switch to custom state: " + behaviour);
        Enemy.SwitchToBehaviourState(LastDefaultBehaviourIndex + TamedBehaviourCount + behaviour);
        Enemy.enabled = false;
    }
    internal virtual void InitCustomBehaviour(int behaviour) {}
    internal virtual void LeaveCustomBehaviour(int behaviour) {}

    public void SwitchToDefaultBehaviour(int behaviour)
    {
        Enemy.SwitchToBehaviourState(behaviour);
        Enemy.enabled = behaviour <= LastDefaultBehaviourIndex;
    }

    // The last vanilla behaviour index for each enemy type
    public static Dictionary<int, int> LastDefaultBehaviourIndices = [];

    // Adds the enemy behaviour classes and custom behaviours to each enemy prefab
    [HarmonyPrefix, HarmonyPatch(typeof(GameNetworkManager), "Start")]
    public static void AddTamingBehaviours()
    {
        int addedDefaultCustomBehaviours = 0, addedBehaviours = 0, enemyCount = 0;
        foreach (var enemyType in Utils.EnemyTypes)
        {
            enemyCount++;
            if (enemyType?.enemyPrefab == null || !enemyType.enemyPrefab.TryGetComponent(out EnemyAI enemyAI) || enemyAI == null) continue;

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

            if (tamedBehaviour.Controllable)
                enemyType.enemyPrefab.gameObject.AddComponent<EnemyController>();

            for (int _ = 0; _ < tamedBehaviour.Cooldowns.Length; ++_)
            {
                enemyType.enemyPrefab.gameObject.AddComponent<CooldownNetworkBehaviour>();
            }

            // Behaviour states
            if (enemyAI.enemyBehaviourStates == null)
                enemyAI.enemyBehaviourStates = [];
            if (LastDefaultBehaviourIndices.ContainsKey(enemyType.enemyPrefab.GetInstanceID()))
            {
                LethalMon.Logger.LogWarning("An enemy type (" + enemyType + " with instance ID " + enemyType.enemyPrefab.GetInstanceID() + ") is being registered but already has been registered before.");
            }
            else
            {
                LastDefaultBehaviourIndices.Add(enemyType.enemyPrefab.GetInstanceID(), enemyAI.enemyBehaviourStates.Length - 1);

                var behaviourStateList = enemyAI.enemyBehaviourStates.ToList();

                // Add tamed behaviours
                foreach (var behaviourName in Enum.GetNames(typeof(TamingBehaviour)))
                    behaviourStateList.Add(new EnemyBehaviourState() { name = behaviourName });

                enemyAI.enemyBehaviourStates = behaviourStateList.ToArray();
            }
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
            CustomBehaviours.Add(CustomBehaviours.Count + 1, new Tuple<string, Action>(customBehaviour.Item2, customBehaviour.Item3));
            LethalMon.Log($"Added custom behaviour {CustomBehaviours.Count} with handler {customBehaviour.Item3.Method.Name}");
        }
        Enemy.enemyBehaviourStates = behaviourStateList.ToArray();
    }

    internal virtual void OnTamedFollowing()
    {
        if (StartOfRound.Instance.inShipPhase) return;

        FollowOwner();
    }

    internal virtual void OnTamedDefending()
    {
        if (StartOfRound.Instance.inShipPhase) return;

        if (!HasTargetEnemy && targetPlayer == null) // lost target
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
    }
    
    internal virtual void OnCallFromBall()
    {
        var scanNode = Enemy.GetComponentInChildren<ScanNodeProperties>();
        if (scanNode != null)
        {
            scanNode.headerText = "Tamed " + Enemy.enemyType.enemyName;
            scanNode.subText = "Owner: " + ownerPlayer!.name;
            scanNode.nodeType = 2;
        }
    }

    internal virtual void OnRetrieveInBall()
    {
        if (IsOwnerPlayer)
            EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, false);
    }

    internal virtual void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall) { } // Host only

    internal string GetCurrentStateDescription()
    {
        if (Enemy.currentBehaviourStateIndex <= LastDefaultBehaviourIndex)
        {
            return "Base behaviour " + Enemy.currentBehaviourStateIndex + " (not implemented)";
        }

        if (Enemy.currentBehaviourStateIndex == LastDefaultBehaviourIndex + (int) TamingBehaviour.TamedFollowing)
        {
            return FollowingBehaviourDescription;
        }
        
        if (Enemy.currentBehaviourStateIndex == LastDefaultBehaviourIndex + (int) TamingBehaviour.TamedDefending)
        {
            return DefendingBehaviourDescription;
        }
        
        if (Enemy.currentBehaviourStateIndex <= LastDefaultBehaviourIndex + TamedBehaviourCount + CustomBehaviours.Count)
        {
            return CustomBehaviours[Enemy.currentBehaviourStateIndex - LastDefaultBehaviourIndex - TamedBehaviourCount].Item1;
        }

        return "Unknown behaviour";
    }
    #endregion

    #region ActionKeys
    internal virtual void ActionKey1Pressed() { }

    internal class ActionKey
    {
        internal InputAction? Key { get; set; } = null;
        internal string Description { get; set; } = "";
        internal bool Visible { get; set; } = false;

        internal string Control => Key == null ? "" : Key.bindings[StartOfRound.Instance.localPlayerUsingController ? 1 : 0].path.Split("/").Last();
        internal string ControlTip => $"{Description}: [{Control}]";
    }

    internal virtual List<ActionKey> ActionKeys => [];

    internal void EnableActionKeyControlTip(InputAction actionKey, bool enable = true)
    {
        var keys = ActionKeys.Where((ak) => ak.Key == actionKey);
        if (keys.Any())
        {
            keys.First().Visible = enable;
            ShowVisibleActionKeyControlTips();
        }
    }

    internal void ShowVisibleActionKeyControlTips()
    {
        HUDManager.Instance.ClearControlTips();

        var controlTips = ActionKeys.Where((ak) => ak.Visible).Select((ak) => ak.ControlTip).ToArray();
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

        if (Enemy.IsOwner)
        {
            if (customBehaviour > TamedBehaviourCount)
            {
                customBehaviour -= TamedBehaviourCount;
                if (CustomBehaviours.ContainsKey(customBehaviour) && CustomBehaviours[customBehaviour] != null)
                    CustomBehaviours[customBehaviour].Item2();
                else
                {
                    LethalMon.Logger.LogWarning($"Custom state {customBehaviour} has no handler.");
                    foreach (var b in CustomBehaviours)
                        LethalMon.Log($"Behaviour found {b.Key} with handler {b.Value.Item2.Method.Name}");
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
    }

    internal virtual void OnUpdate(bool update = false, bool doAIInterval = true)
    {
        if (StartOfRound.Instance.inShipPhase) return;

        if (update)
            Enemy.Update();
        else
        {
            if (!Enemy.IsOwner)
            {
                Enemy.SetClientCalculatingAI(enable: false);
                if (!Enemy.inSpecialAnimation)
                {
                    base.transform.position = Vector3.SmoothDamp(base.transform.position, Enemy.serverPosition, ref Enemy.tempVelocity, Enemy.syncMovementSpeed);
                    base.transform.eulerAngles = new Vector3(base.transform.eulerAngles.x, Mathf.LerpAngle(base.transform.eulerAngles.y, Enemy.targetYRotation, 15f * Time.deltaTime), base.transform.eulerAngles.z);
                }
                Enemy.timeSinceSpawn += Time.deltaTime;
                return;
            }
            
            if (Enemy.movingTowardsTargetPlayer && targetPlayer != null)
            {
                if (Enemy.setDestinationToPlayerInterval <= 0f)
                {
                    Enemy.setDestinationToPlayerInterval = 0.25f;
                    Enemy.destination = RoundManager.Instance.GetNavMeshPosition(targetPlayer.transform.position, RoundManager.Instance.navHit, 2.7f);
                    Debug.Log("Set destination to target player A");
                }
                else
                {
                    Enemy.destination = new Vector3(targetPlayer.transform.position.x, Enemy.destination.y, targetPlayer.transform.position.z);
                    Debug.Log("Set destination to target player B");
                    Enemy.setDestinationToPlayerInterval -= Time.deltaTime;
                }
                if (Enemy.addPlayerVelocityToDestination > 0f)
                {
                    if (targetPlayer == GameNetworkManager.Instance.localPlayerController)
                    {
                        Enemy.destination += Vector3.Normalize(targetPlayer.thisController.velocity * 100f) * Enemy.addPlayerVelocityToDestination;
                    }
                    else if (targetPlayer.timeSincePlayerMoving < 0.25f)
                    {
                        Enemy.destination += Vector3.Normalize((targetPlayer.serverPlayerPosition - targetPlayer.oldPlayerPosition) * 100f) * Enemy.addPlayerVelocityToDestination;
                    }
                }
            }
            if (Enemy.inSpecialAnimation)
            {
                return;
            }
            if (Enemy.updateDestinationInterval >= 0f)
            {
                Enemy.updateDestinationInterval -= Time.deltaTime;
            }
            else
            {
                if (Enemy.currentBehaviourStateIndex > LastDefaultBehaviourIndex)
                {
                    if (Enemy.openDoorSpeedMultiplier > 0f)
                    {
                        Utils.OpenDoorsAsEnemyAroundPosition(Enemy.transform.position);
                    }
                }

                if (Enemy.updateDestinationInterval >= 0f)
                {
                    Enemy.updateDestinationInterval -= Time.deltaTime;
                }
                else
                {
                    if (doAIInterval)
                    {
                        DoAIInterval();
                    }
                    else
                    {
                        if (Enemy.moveTowardsDestination)
                        {
                            Enemy.agent.SetDestination(Enemy.destination);
                        }
                        Enemy.SyncPositionToClients();
                    }
                    Enemy.updateDestinationInterval = Enemy.AIIntervalTime;
                }
                Enemy.updateDestinationInterval = Enemy.AIIntervalTime;
            }
            if (Mathf.Abs(Enemy.previousYRotation - base.transform.eulerAngles.y) > 6f)
            {
                Enemy.previousYRotation = base.transform.eulerAngles.y;
                Enemy.targetYRotation = Enemy.previousYRotation;
                if (base.IsServer)
                {
                    Enemy.UpdateEnemyRotationClientRpc((short)Enemy.previousYRotation);
                }
                else
                {
                    Enemy.UpdateEnemyRotationServerRpc((short)Enemy.previousYRotation);
                }
            }
        }
    }

    internal virtual void LateUpdate()
    {
        if (IsTamed)
            UpdateNameTag();
    }

    internal virtual void Awake()
    {
        CooldownNetworkBehaviour[] cooldownComponents = GetComponents<CooldownNetworkBehaviour>();
        if (cooldownComponents.Length != Cooldowns.Length)
        {
            LethalMon.Log("Parameterized cooldowns count (" + Cooldowns.Length + ") doesn't match cooldowns network behaviour count (" + cooldownComponents.Length + ")", LethalMon.LogType.Error);
        }
        else
        {
            for (int i = 0; i < Cooldowns.Length; ++i)
            {
                cooldownComponents[i].Setup(Cooldowns[i]);
            }
        }
    }

    internal virtual void Start()
    {
        //LethalMon.Logger.LogInfo($"LastDefaultBehaviourIndex for {Enemy.name} is {LastDefaultBehaviourIndex}");
        AddCustomBehaviours();

        if (IsTamed)
        {
            Enemy.Start();
            Enemy.creatureAnimator?.SetBool("inSpawningAnimation", value: false);

            CreateNameTag();
        }
        else if (Enum.TryParse(Enemy.enemyType.name, out Utils.Enemy _))
        {
            ScanNodeProperties scanNode = Enemy.GetComponentInChildren<ScanNodeProperties>();
            if (scanNode != null)
            {
                scanNode.subText = Data.CatchableMonsters.ContainsKey(Enemy.enemyType.name)
                    ? "Catchable"
                    : "Not catchable";
            }
        }

        if (IsOwnerPlayer)
        {
            HUDManagerPatch.EnableHUD(true);
            HUDManagerPatch.ChangeToTamedBehaviour(this);
        }
    }

    internal virtual void DoAIInterval()
    {
        Enemy.DoAIInterval();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        foreach (var cooldown in GetComponents<CooldownNetworkBehaviour>())
            cooldown.OnDestroy();

        if (IsOwnerPlayer)
            HUDManagerPatch.EnableHUD(false);
    }

    internal virtual void TurnTowardsPosition(Vector3 position)
    {
        Transform enemyTransform = Enemy.transform;
        Vector3 direction = position - enemyTransform.position;
        direction.y = 0f;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        enemyTransform.rotation = Quaternion.Slerp(enemyTransform.rotation, targetRotation, Time.deltaTime);
    }

    public virtual void MoveTowards(Vector3 position)
    {
        Enemy.SetDestinationToPosition(position);
    }

    public virtual PokeballItem? RetrieveInBall(Vector3 position)
    {
        hasBeenRetrieved = true;
        
        GameObject? spawnPrefab = BallTypeMethods.GetPrefab(ballType);
        if (spawnPrefab == null)
        {
            LethalMon.Log("Pokeball prefabs not loaded correctly.", LethalMon.LogType.Error);
            return null;
        }

        var ball = Instantiate(spawnPrefab, position, Quaternion.Euler(new Vector3(0, 0f, 0f)));

        PokeballItem pokeballItem = ball.GetComponent<PokeballItem>();
        DateTime now = SystemClock.now;
        var cooldowns = GetComponents<CooldownNetworkBehaviour>().Where(item => item?.Id != null);
        if(cooldowns.Any())
            pokeballItem.cooldowns = cooldowns.ToDictionary(item => item.Id!.Value.Value, item => new Tuple<float, DateTime>(item.CurrentTimer, now));
        pokeballItem.fallTime = 0f;
        pokeballItem.scrapPersistedThroughRounds = scrapPersistedThroughRounds || alreadyCollectedThisRound;
        pokeballItem.SetScrapValue(ballValue);
        ball.GetComponent<NetworkObject>().Spawn(false);
        pokeballItem.SetCaughtEnemyServerRpc(Enemy.enemyType.name);
        pokeballItem.FallToGround();

        OnRetrieveInBall();
        
        Enemy.GetComponent<NetworkObject>().Despawn(true);

        return pokeballItem;
    }
    #endregion

    #region Methods
    private void CreateNameTag()
    {
        var nameCanvasObject = Instantiate(Utils.CurrentPlayer.usernameCanvas.gameObject, transform.position, Quaternion.identity);
        if (nameCanvasObject == null)
        {
            LethalMon.Log("Unable to create name tooltip for tamed enemy " + Enemy.enemyType.name);
            return;
        }

        _nameText = nameCanvasObject.GetComponentInChildren<TextMeshProUGUI>();
        if (_nameText == null)
        {
            LethalMon.Log("No text object found in name tag.", LethalMon.LogType.Error);
            return;
        }

        _nameText.enabled = true;
        _nameText.text = $"{ownerPlayer!.playerUsername}'s\n{Data.CatchableMonsters[Enemy.enemyType.name].DisplayName}";
        _nameText.fontSize = 100;
        _nameText.fontSizeMin = 100;
        _nameText.fontSizeMax = 100;
        _nameText.autoSizeTextContainer = false;
        _nameText.enableWordWrapping = false;

        if (nameCanvasObject.TryGetComponent(out _nameCanvas) && _nameCanvas != null)
            _nameCanvas.gameObject.SetActive(true);

        var position = Enemy.transform.position;
        if (Utils.TryGetRealEnemyBounds(Enemy, out Bounds bounds))
            position.y = bounds.max.y;
        else
        {
            LethalMon.Log("Unable to load enemy bounds. Using default height for name canvas.", LethalMon.LogType.Error);
            position.y += 2f;
        }

        nameCanvasObject.transform.localPosition = Vector3.zero;
        nameCanvasObject.transform.position = position;
        nameCanvasObject.transform.SetParent(Enemy.transform, true); // todo: not working for non-owners so far
    }

    private void UpdateNameTag()
    {
        if (_nameCanvas?.gameObject == null) return;

        var distance = Vector3.Distance(Enemy.transform.position, Utils.CurrentPlayer.transform.position);
        var minNameTagVisibility = 3f;
        var maxNameTagVisibility = 6f;
        var alpha = 1f - (Mathf.Clamp(distance, minNameTagVisibility, maxNameTagVisibility) - minNameTagVisibility) / (maxNameTagVisibility - minNameTagVisibility);
        UpdateNameTagVisibility(alpha);
        if (alpha > 0f)
            UpdateNameTagRotation();
    }

    private void UpdateNameTagVisibility(float alpha)
    {
        if (_nameText != null)
            _nameText.alpha = alpha;
    }

    private void UpdateNameTagRotation()
    {
        if (_nameCanvas?.gameObject == null) return;

        RoundManager.Instance.tempTransform.position = Enemy.transform.position;
        RoundManager.Instance.tempTransform.LookAt(Utils.CurrentPlayer.transform.position);

        _nameCanvas.gameObject.transform.rotation = RoundManager.Instance.tempTransform.rotation;
    }

    public void FollowPosition(Vector3 targetPosition)
    {
        if (Vector3.Distance(Enemy.destination, targetPosition) > 2f)
        {
            //LethalMon.Logger.LogInfo("Follow owner");
            var enemyPosition = Enemy.transform.position;
            if (Vector3.Distance(enemyPosition, targetPosition) > MaxFollowDistance)
            {
                TeleportBehindOwner();
                return;
            }

            if (Vector3.Distance(targetPosition, enemyPosition) > 4f && FindRaySphereIntersections(enemyPosition, (targetPosition - enemyPosition).normalized, targetPosition, 4f,
                    out Vector3 potentialPosition1,
                    out Vector3 potentialPosition2))
            {
                var position = enemyPosition;
                float distance1 = Vector3.Distance(position, potentialPosition1);
                float distance2 = Vector3.Distance(position, potentialPosition2);

                if (distance1 > 4f && distance2 > 4f)
                {
                    if (Enemy.agent != null && !Enemy.agent.isOnNavMesh)
                    {
                        LethalMon.Log("Enemy not on valid navMesh. Recalculating.");
                        Enemy.agent.enabled = false;
                        Enemy.agent.enabled = true;
                    }

                    MoveTowards(distance1 < distance2 ? potentialPosition1 : potentialPosition2);
                }
            }
        }

        // Turn in the direction of the owner gradually
        TurnTowardsPosition(targetPosition);
    }
    
    public void FollowOwner()
    {
        if (ownerPlayer == null) return;
        
        FollowPosition(ownerPlayer.transform.position);
    }

    private void TeleportBehindOwner()
    {
        if (ownerPlayer == null) return;

        Enemy.agent.enabled = false;
        Enemy.transform.position = Utils.GetPositionBehindPlayer(ownerPlayer);
        Enemy.agent.enabled = true;
    }

    internal virtual bool EnemyMeetsTargetingConditions(EnemyAI enemyAI)
    {
        if (Targets == TargetType.Dead && !enemyAI.isEnemyDead || Targets == TargetType.Alive && enemyAI.isEnemyDead) return false;

        return enemyAI.gameObject.activeSelf && enemyAI.gameObject.layer != (int)Utils.LayerMasks.Mask.EnemiesNotRendered &&
            !(enemyAI.TryGetComponent(out TamedEnemyBehaviour tamedBehaviour) && tamedBehaviour.IsOwnedByAPlayer());
    }

    internal virtual void OnFoundTarget() => SwitchToTamingBehaviour(TamingBehaviour.TamedDefending);

    internal void TargetNearestEnemy(bool requireLOS = true, bool fromOwnerPerspective = true)
    {
        targetNearestEnemyInterval -= Time.deltaTime;
        if (targetNearestEnemyInterval > 0)
            return;

        targetNearestEnemyInterval = 1f;
        
        var target = NearestEnemy(requireLOS, fromOwnerPerspective);
        if(target != null)
        {
            targetEnemy = target;
            OnFoundTarget();
            LethalMon.Log("Targeting " + targetEnemy.enemyType.name);
        }
    }

    internal EnemyAI? NearestEnemy(bool requireLOS = true, bool fromOwnerPerspective = true)
    {
        const int layerMask = 1 << (int) Utils.LayerMasks.Mask.Enemies;
        EnemyAI? target = null;
        float distance = float.MaxValue;

        if (fromOwnerPerspective && ownerPlayer == null) return null;

        var startPosition = fromOwnerPerspective ? ownerPlayer!.transform.position : Enemy.transform.position;
        var enemiesInRange = Physics.OverlapSphere(startPosition, TargetingRange, layerMask, QueryTriggerInteraction.Collide);
        foreach (var enemyHit in enemiesInRange)
        {
            var enemyInRange = enemyHit?.GetComponentInParent<EnemyAI>();
            var tamedBehaviour = enemyHit?.GetComponentInParent<TamedEnemyBehaviour>();
            if (enemyInRange?.transform == null || tamedBehaviour == null || tamedBehaviour.IsTamed) continue;

            if (enemyInRange == Enemy || !EnemyMeetsTargetingConditions(enemyInRange)) continue;

            if (requireLOS && !enemyInRange.CheckLineOfSightForPosition(startPosition, 180f, 10)) continue;

            float distanceTowardsEnemy = Vector3.Distance(startPosition, enemyInRange.transform.position);
            if (distanceTowardsEnemy < distance)
            {
                distance = distanceTowardsEnemy;
                target = enemyInRange;
            }
        }

        return target;
    }

    public static bool FindRaySphereIntersections(Vector3 rayOrigin, Vector3 rayDirection, Vector3 sphereCenter, float sphereRadius, out Vector3 intersection1, out Vector3 intersection2)
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

    internal void DropBlood(Vector3 position, int minAmount = 3, int maxAmount = 7)
    {
        if (ownerPlayer == null) return;

        var amount = UnityEngine.Random.Range(minAmount, maxAmount);
        while (amount > 0)
        {
            amount--;
            ownerPlayer.currentBloodIndex = (ownerPlayer.currentBloodIndex + 1) % ownerPlayer.playerBloodPooledObjects.Count;
            var bloodObject = ownerPlayer.playerBloodPooledObjects[ownerPlayer.currentBloodIndex];
            if (bloodObject == null) continue;

            bloodObject.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.up);
            bloodObject.transform.SetParent(ownerPlayer.isInElevator ? StartOfRound.Instance.elevatorTransform : StartOfRound.Instance.bloodObjectsContainer);

            var randomDirection = new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), UnityEngine.Random.Range(-1f, -0.5f), UnityEngine.Random.Range(-0.5f, 0.5f));
            var interactRay = new Ray(position + Vector3.up * 2f, randomDirection);
            if (Physics.Raycast(interactRay, out RaycastHit hit, 6f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
            {
                bloodObject.transform.position = hit.point - Vector3.down * 0.45f;
                ownerPlayer.RandomizeBloodRotationAndScale(bloodObject.transform);
                bloodObject.transform.gameObject.SetActive(value: true);
            }
        }
    }

    public void SetCooldownTimers(Dictionary<string, Tuple<float, DateTime>> cooldownsTimers)
    {
        DateTime now = SystemClock.now;
        foreach (KeyValuePair<string, Tuple<float, DateTime>> cooldownTimer in cooldownsTimers)
        {
            GetComponents<CooldownNetworkBehaviour>().FirstOrDefault(cooldown => cooldown.Id != null && cooldown.Id.Value.Value == cooldownTimer.Key)?.InitTimer(cooldownTimer.Value.Item1 + (float) (now - cooldownTimer.Value.Item2).TotalSeconds);
        }
    }

    public CooldownNetworkBehaviour GetCooldownWithId(string id)
    {
        return GetComponents<CooldownNetworkBehaviour>().FirstOrDefault(cooldown => cooldown.Id != null && cooldown.Id.Value.Value == id);
    }
    
    public bool IsOwnedByAPlayer()
    {
        return ownerPlayer != null;
    }

    public bool IsCurrentBehaviourTaming(TamingBehaviour behaviour)
    {
        return Enemy.currentBehaviourStateIndex == LastDefaultBehaviourIndex + (int) behaviour;
    }
    #endregion

    #region Teleporting
    // Actions after teleporting an enemy
    private static readonly Dictionary<string, Action<EnemyAI, Vector3>> afterTeleportFunctions = new()
    {
        {
            nameof(SandSpiderAI), (enemyAI, position) =>
            {
                var spider = (enemyAI as SandSpiderAI)!;
                spider.meshContainerPosition = position;
                spider.meshContainerTarget = position;
            }
        },
        {
            nameof(BlobAI), (enemyAI, position) => (enemyAI as BlobAI)!.centerPoint.position = position
        }
    };

    public void TeleportEnemy(EnemyAI enemyAI, Vector3 position)
    {
        if (!Utils.IsHost || enemyAI?.agent == null) return;

        if (enemyAI.agent.enabled)
            enemyAI.agent.Warp(position);
        else
            enemyAI.transform.position = position;
        enemyAI.serverPosition = position;

        //enemyAI.SyncPositionToClients();

        if (afterTeleportFunctions.TryGetValue(enemyAI.GetType().Name, out var afterTeleportFunction))
            afterTeleportFunction.Invoke(enemyAI, position);
    }

    public virtual bool CanBeTeleported()
    {
        return true;
    }
    #endregion

#if DEBUG
    #region DEBUG
    internal void SetTamedByHost_DEBUG()
    {
        ownerPlayer = Utils.AllPlayers.Where((p) => p.playerClientId == 0ul).First();
        SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);

        if(Controllable && TryGetComponent(out EnemyController controller))
        {
            controller.AddTrigger();
            controller.SetControlTriggerVisible(true);
        }
    }
    #endregion
#endif
}