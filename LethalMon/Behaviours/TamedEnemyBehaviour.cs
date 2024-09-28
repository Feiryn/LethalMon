using System;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using HarmonyLib;
using LethalMon.Compatibility;
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
    internal virtual bool TargetOnlyKillableEnemies => false;

    internal virtual bool CanBlockOtherEnemies => false;

    internal virtual Cooldown[] Cooldowns => [];
    
    internal static bool AlreadyAddedBehaviours = false;

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
        //{ typeof(BushWolfEnemy),     typeof(KidnapperFoxTamedBehaviour) },
        { typeof(CrawlerAI),         typeof(CrawlerTamedBehaviour) },
        { typeof(MaskedPlayerEnemy), typeof(MaskedTamedBehaviour) },
        { typeof(BaboonBirdAI),      typeof(BaboonHawkTamedBehaviour) },
        { typeof(SandSpiderAI),      typeof(SpiderTamedBehaviour) },
        { typeof(BlobAI),            typeof(BlobTamedBehaviour) }
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

    // Name Tag
    private Canvas? _nameCanvas = null;
    private TextMeshProUGUI? _nameText = null;
    private float _nameCanvasYOffset = 2f;

    private bool IsNameTagVisible => _nameText != null ? _nameText.alpha > 0f : false;

    private const float MinNameTagRange = 3f;
    private const float MaxNameTagRange = 6f;

    private float _timeSinceNameTagVisible = 0f;

    // Owner
    public PlayerControllerB? ownerPlayer = null;
    public ulong OwnerID => ownerPlayer != null ? ownerPlayer.playerClientId : ulong.MaxValue;
    public bool IsTamed => ownerPlayer != null;
    public bool IsOwnerPlayer => ownerPlayer == Utils.CurrentPlayer;
    public float DistanceToOwner => ownerPlayer != null ? Vector3.Distance(Enemy.transform.position, ownerPlayer.transform.position) : 0f;

    // Target enemy
    public EnemyAI? targetEnemy = null;
    public bool HasTargetEnemy => targetEnemy != null && targetEnemy.gameObject.activeSelf;
    public float DistanceToTargetEnemy => HasTargetEnemy ? Vector3.Distance(Enemy.transform.position, targetEnemy!.transform.position) : 0f;
    public bool IsCollidingWithTargetEnemy => HasTargetEnemy ? targetEnemy!.meshRenderers.Any(meshRendererTarget => Enemy.meshRenderers.Any(meshRendererSelf => meshRendererSelf.bounds.Intersects(meshRendererTarget.bounds))) : false;

    // Target player
    public PlayerControllerB? targetPlayer = null;
    public float DistanceToTargetPlayer => targetPlayer != null ? Vector3.Distance(Enemy.transform.position, targetPlayer.transform.position) : 0f;

    // Ball
    public BallType ballType;

    public int ballValue;

    public bool scrapPersistedThroughRounds;

    public bool alreadyCollectedThisRound;

    public bool hasBeenRetrieved = false;

    public bool isDnaComplete = false;
    
    internal string EnemySkinRegistryId => EnemySkinRegistryCompatibility.Instance.Enabled ? !string.IsNullOrEmpty(ForceEnemySkinRegistryId) ? ForceEnemySkinRegistryId : EnemySkinRegistryCompatibility.GetEnemySkinId(Enemy) : string.Empty;

    internal string ForceEnemySkinRegistryId = string.Empty;

    // Following
    internal const float TimeBeforeUsingEntrance = 4f;
    private float _timeAtEntrance = 0f;
    private bool _usingEntrance = false;
    private bool _followingRequiresEntrance = false;
    internal bool isOutside = false; // We use our own isOutside because we don't want other mods that messes up with the original isOutside to affect our mod

    // Behaviour
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
    internal bool foundEnemiesInRangeInLastSearch = false;

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
    public int CurrentCustomBehaviour => Enemy.currentBehaviourStateIndex - LastDefaultBehaviourIndex - TamedBehaviourCount;
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
    [HarmonyPostfix, HarmonyPatch(typeof(Terminal), nameof(Terminal.Awake))]
    public static void AddTamingBehaviours()
    {
        if (AlreadyAddedBehaviours) return;
        AlreadyAddedBehaviours = true;
        
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
            scanNode.subText = "Owner: " + ownerPlayer!.playerUsername;
            scanNode.nodeType = 2;
        }
    }

    internal virtual void OnRetrieveInBall()
    {
        if (IsOwnerPlayer)
            EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, false);

        HideNameTag();
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
            if (_usingEntrance)
                return "Using entrance...";
            else if (_followingRequiresEntrance)
                return "Going to entrance...";
            else
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
            isOutside = Utils.IsEnemyOutside(Enemy);
            Enemy.creatureAnimator?.SetBool("inSpawningAnimation", value: false);

            if (!CanBlockOtherEnemies && Enemy.agent != null)
            {
                Enemy.agent.radius = 0.1f;                    // Enemy goes "mostly" through this one
                Enemy.agent.avoidancePriority = int.MaxValue; // Lower priority pushes the higher one
            }

            Utils.CallNextFrame(CreateNameTag);
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

        if (IsTamed && !base.IsServer) // Counter to EnemyAI.Start()
            RoundManager.Instance.SpawnedEnemies.Remove(Enemy);

        HideNameTag();
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
        if (EnemySkinRegistryCompatibility.Instance.Enabled)
            pokeballItem.enemySkinRegistryId = EnemySkinRegistryId;
        pokeballItem.SetCaughtEnemyServerRpc(Enemy.enemyType.name, pokeballItem.enemySkinRegistryId);
        pokeballItem.isDnaComplete = isDnaComplete;
        pokeballItem.FallToGround();

        OnRetrieveInBall();
        
        Enemy.GetComponent<NetworkObject>().Despawn(true);

        return pokeballItem;
    }
    #endregion

    #region NameTag
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
        UpdateNameTagFontSize();
        _nameText.autoSizeTextContainer = false;
        _nameText.enableWordWrapping = false;
        _nameText.alpha = 0f;

        if (nameCanvasObject.TryGetComponent(out _nameCanvas) && _nameCanvas != null)
            _nameCanvas.gameObject.SetActive(true);
        
        if (Utils.TryGetRealEnemyBounds(Enemy, out Bounds bounds))
            _nameCanvasYOffset = bounds.max.y - Enemy.transform.position.y;
        else
            LethalMon.Log("Unable to load enemy bounds. Using default height for name canvas.", LethalMon.LogType.Error);
        LethalMon.Log("offset y: " + _nameCanvasYOffset, LethalMon.LogType.Warning);
    }

    private void HideNameTag()
    {
        if (IsNameTagVisible)
            _nameText!.alpha = 0f;
    }

    private void UpdateNameTag()
    {
        if (_nameCanvas?.gameObject == null) return;

        if(ModConfig.Instance.values.TamedNameFontSize == 0f)
        {
            if(_nameText != null)
                _nameText.alpha = 0f;
            return;
        }

        UpdateNameTagVisibility();

        if (IsNameTagVisible)
            UpdateNameTagPositionAndRotation();
    }

    private void UpdateNameTagFontSize()
    {
        if (_nameText == null) return;

        var fontSize = ModConfig.Instance.values.TamedNameFontSize * 10f;
        _nameText.fontSize = fontSize;
        _nameText.fontSizeMin = fontSize;
        _nameText.fontSizeMax = fontSize;
    }

    private void UpdateNameTagVisibility()
    {
        if (_nameText == null) return;

        float expectedAlpha;
        var distance = Vector3.Distance(Enemy.transform.position, Utils.CurrentPlayer.transform.position);
        if (distance < 2f)
            expectedAlpha = Mathf.Max(distance - 1.5f, 0f);
        else
            expectedAlpha = 1f - (Mathf.Clamp(distance, MinNameTagRange, MaxNameTagRange) - MinNameTagRange) / (MaxNameTagRange - MinNameTagRange);

        if (expectedAlpha > 0f)
            _timeSinceNameTagVisible += Time.deltaTime;
        else
            _timeSinceNameTagVisible = 0f;

        if (_timeSinceNameTagVisible > 4f)
            _nameText.alpha = Mathf.Lerp(_nameText.alpha, 0f, Time.deltaTime * 4f);
        else
            _nameText.alpha = Mathf.Lerp(_nameText.alpha, expectedAlpha, Time.deltaTime * 10f);
    }

    private void UpdateNameTagPositionAndRotation()
    {
        if (_nameCanvas?.gameObject == null) return;

        var pos = Enemy.transform.position;
        pos.y += _nameCanvasYOffset;
        _nameCanvas.gameObject.transform.position = pos;

        pos.y = Utils.CurrentPlayer.transform.position.y; // Make the name tag not not rotate up/down
        RoundManager.Instance.tempTransform.position = pos;
        RoundManager.Instance.tempTransform.LookAt(Utils.CurrentPlayer.transform.position);

        _nameCanvas.gameObject.transform.rotation = RoundManager.Instance.tempTransform.rotation;
    }
    #endregion

    #region Methods
    public void FollowPosition(Vector3 targetPosition)
    {
        if (Vector3.Distance(Enemy.destination, targetPosition) > 2f)
        {
            //LethalMon.Logger.LogInfo("Follow owner");
            var enemyPosition = Enemy.transform.position;
            if (Vector3.Distance(enemyPosition, targetPosition) > MaxFollowDistance && CanBeTeleported())
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
                    PlaceOnNavMesh();
                    MoveTowards(distance1 < distance2 ? potentialPosition1 : potentialPosition2);
                }
            }
        }

        // Turn in the direction of the owner gradually
        TurnTowardsPosition(targetPosition);
    }

    internal void PlaceOnNavMesh() => PlaceEnemyOnNavMesh(Enemy);

    internal static void PlaceEnemyOnNavMesh(EnemyAI enemyAI)
    {
        if (enemyAI.agent != null && enemyAI.agent.enabled && !enemyAI.agent.isOnNavMesh)
        {
            LethalMon.Log("Enemy not on a valid navMesh. Repositioning.", LethalMon.LogType.Warning);
            var location = RoundManager.Instance.GetNavMeshPosition(enemyAI.transform.position);
            enemyAI.agent.Warp(location);

            enemyAI.agent.enabled = false;
            enemyAI.agent.enabled = true;
        }
    }

    public void FollowOwner()
    {
        if (ownerPlayer == null) return;
        
        var entranceTeleportRequired = ownerPlayer.isInsideFactory == isOutside;
        if(entranceTeleportRequired != _followingRequiresEntrance)
        {
            _followingRequiresEntrance = entranceTeleportRequired;
            HUDManagerPatch.UpdateTamedMonsterAction(GetCurrentStateDescription());
        }

        if(ownerPlayer.isInsideFactory == isOutside)
        {
            if(CanBeTeleported() || !EntranceTeleportPatch.HasTeleported)
            {
                if (!EntranceTeleportPatch.HasTeleported)
                    LethalMon.Log("Teleporting behind owner because the monster hasn't been teleported yet");

                TeleportBehindOwner();
                return;
            }

            Vector3 destination = EntranceTeleportPatch.lastEntranceTeleportFrom!.Value;
            if(_timeAtEntrance > 0f || Vector3.Distance(Enemy.transform.position, destination) < 5f)
            {
                if(_timeAtEntrance == 0f)
                {
                    _usingEntrance = true;
                    HUDManagerPatch.UpdateTamedMonsterAction(GetCurrentStateDescription());
                }

                _timeAtEntrance += Time.deltaTime;
                if (_timeAtEntrance >= TimeBeforeUsingEntrance)
                {
                    _usingEntrance = false;
                    _timeAtEntrance = 0f;
                    Teleport(EntranceTeleportPatch.lastEntranceTeleportTo!.Value, true, true);
                }
            }
            else
            {
                PlaceOnNavMesh();
                MoveTowards(destination);
            }
            return;
        }
        
        FollowPosition(ownerPlayer.transform.position);
    }

    private void TeleportBehindOwner()
    {
        if (ownerPlayer == null || !CanBeTeleported()) return;

        Teleport(Utils.GetPositionBehindPlayer(ownerPlayer), true, true);
    }

    internal virtual bool EnemyMeetsTargetingConditions(EnemyAI enemyAI)
    {
        if (Targets == TargetType.Dead && !enemyAI.isEnemyDead || Targets == TargetType.Alive && enemyAI.isEnemyDead) return false;

        if(TargetOnlyKillableEnemies && !enemyAI.enemyType.canDie) return false;

        return enemyAI.gameObject.activeSelf && enemyAI.gameObject.layer != (int)Utils.LayerMasks.Mask.EnemiesNotRendered &&
            !(enemyAI.TryGetComponent(out TamedEnemyBehaviour tamedBehaviour) && tamedBehaviour.IsOwnedByAPlayer());
    }

    internal virtual void OnFoundTarget() => SwitchToTamingBehaviour(TamingBehaviour.TamedDefending);

    internal void TargetNearestEnemy(bool requireLOS = true, bool fromOwnerPerspective = true, float angle = 180f)
    {
        targetNearestEnemyInterval -= Time.deltaTime;
        if (targetNearestEnemyInterval > 0)
            return;

        targetNearestEnemyInterval = foundEnemiesInRangeInLastSearch ? 0.5f : 1f; // More frequent search if enemy that met the conditions was in range
        
        var target = NearestEnemy(requireLOS, fromOwnerPerspective, angle);
        if(target != null)
        {
            targetEnemy = target;
            OnFoundTarget();
            LethalMon.Log("Targeting " + targetEnemy.enemyType.name);
        }
    }

    internal EnemyAI? NearestEnemy(bool requireLOS = true, bool fromOwnerPerspective = true, float angle = 180f)
    {
        foundEnemiesInRangeInLastSearch = false;
        const int layerMask = 1 << (int) Utils.LayerMasks.Mask.Enemies;
        EnemyAI? target = null;
        float distance = float.MaxValue;

        if (fromOwnerPerspective && ownerPlayer == null) return null;

        var startTransform = fromOwnerPerspective ? ownerPlayer!.playerEye.transform : Enemy.transform;
        var enemiesInRange = Physics.OverlapSphere(startTransform.position, TargetingRange, layerMask, QueryTriggerInteraction.Collide);
        foreach (var enemyHit in enemiesInRange)
        {
            var enemyInRange = enemyHit?.GetComponentInParent<EnemyAI>();
            var tamedBehaviour = enemyHit?.GetComponentInParent<TamedEnemyBehaviour>();
            if (enemyInRange?.transform == null || tamedBehaviour == null || tamedBehaviour.IsTamed) continue;

            if (enemyInRange == Enemy || !EnemyMeetsTargetingConditions(enemyInRange)) continue;

            foundEnemiesInRangeInLastSearch = true;
            
            if (requireLOS && !OptimizedCheckLineOfSightForPosition(startTransform.position, enemyInRange.transform.position, startTransform.forward, angle)) continue;

            float distanceTowardsEnemy = Vector3.Distance(startTransform.position, enemyInRange.transform.position);
            if (distanceTowardsEnemy < distance)
            {
                distance = distanceTowardsEnemy;
                target = enemyInRange;
            }
        }

        return target;
    }
    
    internal static bool OptimizedCheckLineOfSightForPosition(Vector3 startPosition, Vector3 targetPosition, Vector3 forward, float angle)
    {
        if (!Physics.Linecast(startPosition, targetPosition, out var _, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
        {
            Vector3 to = targetPosition - startPosition;
            if (Vector3.Angle(forward, to) < angle)
            {
                return true;
            }
        }

        return false;
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

    public void Teleport(Vector3 position, bool placeOnNavMesh = false, bool syncPosition = false) => TeleportEnemy(Enemy, position, placeOnNavMesh, syncPosition);

    public static void TeleportEnemy(EnemyAI enemyAI, Vector3 position, bool placeOnNavMesh = false, bool syncPosition = false)
    {
        if (!(Utils.IsHost || enemyAI.IsOwner) || enemyAI?.agent == null) return;

        if (enemyAI.agent.enabled)
            enemyAI.agent.Warp(position);
        else
            enemyAI.transform.position = position;

        if (placeOnNavMesh)
            PlaceEnemyOnNavMesh(enemyAI);

        if (syncPosition)
            enemyAI.SyncPositionToClients();
        else
            enemyAI.serverPosition = position;

        if (afterTeleportFunctions.TryGetValue(enemyAI.GetType().Name, out var afterTeleportFunction))
            afterTeleportFunction.Invoke(enemyAI, position);
        
        if (enemyAI.TryGetComponent(out TamedEnemyBehaviour tamedBehaviour))
        {
            tamedBehaviour.isOutside = Utils.IsEnemyOutside(enemyAI);
        }
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
    }

    internal void SetControllable_DEBUG()
    {
        if (Controllable && TryGetComponent(out EnemyController controller))
        {
            Utils.CallNextFrame(() =>
            {
                controller.AddTrigger();
                controller.SetControlTriggerVisible(true);
            });
        }
    }
    #endregion
#endif
}