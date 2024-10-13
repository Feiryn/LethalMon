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

/// <summary>
/// A base class for tamed enemy behaviours.
/// </summary>
[DisallowMultipleComponent]
public class TamedEnemyBehaviour : NetworkBehaviour
{
    /// <summary>
    /// Determines whether the enemy can be controlled by the player.
    /// It automatically adds an <see cref="EnemyController"/> component to the enemy if set to <see langword="true"/>.
    /// </summary>
    public virtual bool Controllable => false;

    /// <summary>
    /// The type of targets the enemy can target.
    /// It is used for example by the Butler to target dead enemies.
    /// </summary>
    public enum TargetType
    {
        Any,
        Alive,
        Dead
    };

    /// <summary>
    /// The type of targets the enemy can target.
    /// It is used for example by the Butler to target dead enemies.
    /// </summary>
    public virtual TargetType Targets => TargetType.Alive;
    
    /// <summary>
    /// The range at which the enemy can target enemies.
    /// </summary>
    public virtual float TargetingRange => 10f;
    
    /// <summary>
    /// Determines whether the enemy can only target killable enemies.
    /// </summary>
    public virtual bool TargetOnlyKillableEnemies => false;

    /// <summary>
    /// Determines whether the enemy can block other enemies.
    /// It can be used to make an enemy that can block other enemies from passing through.
    /// </summary>
    public virtual bool CanBlockOtherEnemies => false;

    /// <summary>
    /// The cooldowns for the enemy.
    /// </summary>
    public virtual Cooldown[] Cooldowns => [];
    

    private static bool _alreadyAddedBehaviours = false;

    private EnemyAI? _enemy = null;
    
    /// <summary>
    /// The enemy AI component of the monster.
    /// It is recommended to store this field in a cast variable for easier access.
    /// </summary>
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
    /// <summary>
    /// The player that owns the enemy.
    /// </summary>
    public PlayerControllerB? ownerPlayer = null;
    
    /// <summary>
    /// The ID of the player that owns the enemy.
    /// </summary>
    public ulong OwnerID => ownerPlayer != null ? ownerPlayer.playerClientId : ulong.MaxValue;
    
    /// <summary>
    /// Determines whether the enemy is tamed.
    /// </summary>
    public bool IsTamed => ownerPlayer != null;
    
    /// <summary>
    /// Determines whether the owner of the enemy is the local player.
    /// </summary>
    public bool IsOwnerPlayer => ownerPlayer == Utils.CurrentPlayer;
    
    /// <summary>
    /// The distance to the owner.
    /// </summary>
    public float DistanceToOwner => ownerPlayer != null ? Vector3.Distance(Enemy.transform.position, ownerPlayer.transform.position) : 0f;

    // Target enemy
    /// <summary>
    /// The target enemy of the enemy.
    /// </summary>
    public EnemyAI? targetEnemy = null;
    
    /// <summary>
    /// Determines whether the enemy has a target enemy.
    /// </summary>
    public bool HasTargetEnemy => targetEnemy != null && targetEnemy.gameObject.activeSelf;
    
    /// <summary>
    /// The distance to the target enemy.
    /// </summary>
    public float DistanceToTargetEnemy => HasTargetEnemy ? Vector3.Distance(Enemy.transform.position, targetEnemy!.transform.position) : 0f;
    
    /// <summary>
    /// Determines whether the enemy is colliding with the target enemy.
    /// </summary>
    public bool IsCollidingWithTargetEnemy => HasTargetEnemy ? targetEnemy!.meshRenderers.Any(meshRendererTarget => Enemy.meshRenderers.Any(meshRendererSelf => meshRendererSelf.bounds.Intersects(meshRendererTarget.bounds))) : false;

    // Target player
    /// <summary>
    /// The target player of the enemy.
    /// </summary>
    public PlayerControllerB? targetPlayer = null;
    
    /// <summary>
    /// The distance to the target player.
    /// </summary>
    public float DistanceToTargetPlayer => targetPlayer != null ? Vector3.Distance(Enemy.transform.position, targetPlayer.transform.position) : 0f;

    // Ball
    internal BallType BallType;

    internal int BallValue;

    internal bool ScrapPersistedThroughRounds;

    internal bool AlreadyCollectedThisRound;

    internal bool HasBeenRetrieved = false;

    internal bool IsDnaComplete = false;

    private string EnemySkinRegistryId => EnemySkinRegistryCompatibility.Instance.Enabled ? !string.IsNullOrEmpty(ForceEnemySkinRegistryId) ? ForceEnemySkinRegistryId : EnemySkinRegistryCompatibility.GetEnemySkinId(Enemy) : string.Empty;

    internal string ForceEnemySkinRegistryId = string.Empty;

    // Following
    /// <summary>
    /// The time before the enemy uses the entrance.
    /// Used only if the enemy can't be teleported.
    /// </summary>
    public const float TimeBeforeUsingEntrance = 4f;
    
    private float _timeAtEntrance = 0f;
    private bool _usingEntrance = false;
    private bool _followingRequiresEntrance = false;
    internal bool IsOutside = false; // We use our own isOutside because we don't want other mods that messes up with the original isOutside to affect our mod

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
    
    private float _targetNearestEnemyInterval = 0f;
    private bool _foundEnemiesInRangeInLastSearch = false;

    #region Behaviours
    /// <summary>
    /// The base taming behaviour of the enemy.
    /// </summary>
    public enum TamingBehaviour
    {
        TamedFollowing = 1,
        TamedDefending = 2
    }
    
    internal static readonly int TamedBehaviourCount = Enum.GetNames(typeof(TamingBehaviour)).Length;

    private readonly Dictionary<int, Tuple<string, Action>> _customBehaviours = []; // List of behaviour state indices and their custom handler

    /// <summary>
    /// Override this to add more custom behaviours to your tamed enemy.
    /// </summary>
    public virtual List<Tuple<string, string, Action>>? CustomBehaviourHandler => null;

    /// <summary>
    /// The text shown in the HUD when the enemy is following you.
    /// </summary>
    public virtual string FollowingBehaviourDescription => "Follows you...";
    
    /// <summary>
    /// The text shown in the HUD when the enemy is defending you.
    /// </summary>
    public virtual string DefendingBehaviourDescription => "Defends you!";

    /// <summary>
    /// Determines whether the enemy can defend the player.
    /// This value can be the result of a function.
    /// If the enemy can defend, it will switch to the defending state when the owner is attacked.
    /// </summary>
    public virtual bool CanDefend => true;

    /// <summary>
    /// The maximum distance the enemy can follow the owner. If the owner is further away, the enemy will teleport behind the owner.
    /// </summary>
    public virtual float MaxFollowDistance => 30f;

    /// <summary>
    /// Gets the current taming behaviour of the enemy. Returns <see langword="null"/> if the enemy is not in a custom taming state (so following, defending, or a base game behaviour).
    /// </summary>
    public TamingBehaviour? CurrentTamingBehaviour
    {
        get
        {
            var index = Enemy.currentBehaviourStateIndex - LastDefaultBehaviourIndex;
            return Enum.IsDefined(typeof(TamingBehaviour), index) ? (TamingBehaviour)index : null;
        }
    }
    
    /// <summary>
    /// Gets the current custom behaviour of the enemy. Returns a negative value if the enemy is not in a custom state.
    /// </summary>
    public int CurrentCustomBehaviour => Enemy.currentBehaviourStateIndex - LastDefaultBehaviourIndex - TamedBehaviourCount;
    
    /// <summary>
    /// Makes the enemy switch to a taming behaviour.
    /// </summary>
    /// <param name="behaviour">The new behaviour</param>
    public void SwitchToTamingBehaviour(TamingBehaviour behaviour)
    {
        if (CurrentTamingBehaviour == behaviour) return;

        LethalMon.Logger.LogInfo("Switch to taming state: " + behaviour.ToString());
        Enemy.SwitchToBehaviourState(LastDefaultBehaviourIndex + (int)behaviour);
        Enemy.enabled = false;
    }
    
    /// <summary>
    /// Function called when the enemy switches to a taming behaviour.
    /// </summary>
    /// <param name="behaviour">The new behaviour (following or defending)</param>
    public virtual void InitTamingBehaviour(TamingBehaviour behaviour) {}
    
    /// <summary>
    /// Function called when the enemy leaves a taming behaviour.
    /// </summary>
    /// <param name="behaviour">The old behaviour  (following or defending)</param>
    public virtual void LeaveTamingBehaviour(TamingBehaviour behaviour) {}

    /// <summary>
    /// Makes the enemy switch to a custom behaviour.
    /// </summary>
    /// <param name="behaviour">Custom behaviour index</param>
    public void SwitchToCustomBehaviour(int behaviour)
    {
        if (CurrentCustomBehaviour == behaviour) return;

        LethalMon.Logger.LogInfo("Switch to custom state: " + behaviour);
        Enemy.SwitchToBehaviourState(LastDefaultBehaviourIndex + TamedBehaviourCount + behaviour);
        Enemy.enabled = false;
    }
    
    /// <summary>
    /// Function called when the enemy switches to a custom behaviour.
    /// </summary>
    /// <param name="behaviour">The new custom behaviour index</param>
    public virtual void InitCustomBehaviour(int behaviour) {}
    
    /// <summary>
    /// Function called when the enemy leaves a custom behaviour.
    /// </summary>
    /// <param name="behaviour">The old custom behaviour index</param>
    public virtual void LeaveCustomBehaviour(int behaviour) {}

    /// <summary>
    /// Makes the enemy switch to a default behaviour.
    /// </summary>
    /// <param name="behaviour"></param>
    public void SwitchToDefaultBehaviour(int behaviour)
    {
        Enemy.SwitchToBehaviourState(behaviour);
        Enemy.enabled = behaviour <= LastDefaultBehaviourIndex;
    }

    // The last vanilla behaviour index for each enemy type
    private static readonly Dictionary<int, int> LastDefaultBehaviourIndices = [];

    // Adds the enemy behaviour classes and custom behaviours to each enemy prefab
    [HarmonyPostfix, HarmonyPatch(typeof(Terminal), nameof(Terminal.Awake))]
    internal static void AddTamingBehaviours()
    {
        if (_alreadyAddedBehaviours) return;
        _alreadyAddedBehaviours = true;
        
        int addedDefaultCustomBehaviours = 0, addedBehaviours = 0, enemyCount = 0;
        foreach (var enemyType in Utils.EnemyTypes)
        {
            enemyCount++;
            if (enemyType?.enemyPrefab == null || !enemyType.enemyPrefab.TryGetComponent(out EnemyAI enemyAI) || enemyAI == null) continue;

            // Behaviour controller
            var tamedBehaviourType = Registry.GetTamedBehaviour(enemyAI.GetType());
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

    private void AddCustomBehaviours()
    {
        if (CustomBehaviourHandler == null || CustomBehaviourHandler.Count == 0) return;

        var behaviourStateList = Enemy.enemyBehaviourStates.ToList();
        foreach (var customBehaviour in CustomBehaviourHandler)
        {
            behaviourStateList.Add(new EnemyBehaviourState() { name = customBehaviour.Item1 });
            _customBehaviours.Add(_customBehaviours.Count + 1, new Tuple<string, Action>(customBehaviour.Item2, customBehaviour.Item3));
            LethalMon.Log($"Added custom behaviour {_customBehaviours.Count} with handler {customBehaviour.Item3.Method.Name}");
        }
        Enemy.enemyBehaviourStates = behaviourStateList.ToArray();
    }

    /// <summary>
    /// Function called at each update when the enemy is following the owner.
    /// By default, it makes the enemy follow the owner by calling <see cref="FollowOwner"/>.
    /// </summary>
    public virtual void OnTamedFollowing()
    {
        if (StartOfRound.Instance.inShipPhase) return;

        FollowOwner();
    }

    /// <summary>
    /// Function called at each update when the enemy is defending the owner.
    /// By default, it only makes the enemy switch to the following state if it lost its target (<see cref="HasTargetEnemy"/> is false or <see cref="targetPlayer"/> is null).
    /// </summary>
    public virtual void OnTamedDefending()
    {
        if (StartOfRound.Instance.inShipPhase) return;

        if (!HasTargetEnemy && targetPlayer == null) // lost target
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
    }
    
    /// <summary>
    /// Function called when the enemy is called from the ball.
    /// </summary>
    public virtual void OnCallFromBall()
    {
        var scanNode = Enemy.GetComponentInChildren<ScanNodeProperties>();
        if (scanNode != null)
        {
            scanNode.headerText = "Tamed " + Enemy.enemyType.enemyName;
            scanNode.subText = "Owner: " + ownerPlayer!.playerUsername;
            scanNode.nodeType = 2;
        }
    }

    /// <summary>
    /// Function called when the enemy is retrieved in the ball.
    /// </summary>
    public virtual void OnRetrieveInBall()
    {
        if (IsOwnerPlayer)
            EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, false);

        HideNameTag();
    }

    /// <summary>
    /// Function called when the capture of the enemy fails. It basically makes the enemy aggressive.
    /// </summary>
    /// <param name="playerWhoThrewBall">The player who threw the ball</param>
    public virtual void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall) { } // Host only

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
        
        if (Enemy.currentBehaviourStateIndex <= LastDefaultBehaviourIndex + TamedBehaviourCount + _customBehaviours.Count)
        {
            return _customBehaviours[Enemy.currentBehaviourStateIndex - LastDefaultBehaviourIndex - TamedBehaviourCount].Item1;
        }

        return "Unknown behaviour";
    }
    #endregion

    #region ActionKeys
    /// <summary>
    /// Function called when the action key 1 is pressed.
    /// </summary>
    public virtual void ActionKey1Pressed() { }

    /// <summary>
    /// Action key class.
    /// </summary>
    public class ActionKey
    {
        /// <summary>
        /// Action key to bind.
        /// </summary>
        public InputAction? Key { get; set; } = null;
        
        /// <summary>
        /// Description of the action key.
        /// </summary>
        public string Description { get; set; } = "";
        
        /// <summary>
        /// Determines whether the action key control tip is visible.
        /// </summary>
        public bool Visible { get; set; } = false;

        /// <summary>
        /// The name of the control.
        /// </summary>
        public string Control => Key == null ? "" : Key.bindings[StartOfRound.Instance.localPlayerUsingController ? 1 : 0].path.Split("/").Last();
        
        /// <summary>
        /// The control tip to show (description + control).
        /// </summary>
        public string ControlTip => $"{Description}: [{Control}]";
    }

    /// <summary>
    /// The action keys of the enemy.
    /// </summary>
    public virtual List<ActionKey> ActionKeys => [];

    /// <summary>
    /// Enables or disables the action key control tip.
    /// </summary>
    /// <param name="actionKey">The action key to enable or disable</param>
    /// <param name="enable">Whether to enable or disable the action key control tip</param>
    public void EnableActionKeyControlTip(InputAction actionKey, bool enable = true)
    {
        var keys = ActionKeys.Where((ak) => ak.Key == actionKey);
        if (keys.Any())
        {
            keys.First().Visible = enable;
            ShowVisibleActionKeyControlTips();
        }
    }

    /// <summary>
    /// Make the actions keys control tip visible.
    /// </summary>
    public void ShowVisibleActionKeyControlTips()
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
                if (_customBehaviours.ContainsKey(customBehaviour) && _customBehaviours[customBehaviour] != null)
                    _customBehaviours[customBehaviour].Item2();
                else
                {
                    LethalMon.Logger.LogWarning($"Custom state {customBehaviour} has no handler.");
                    foreach (var b in _customBehaviours)
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

    /// <summary>
    /// Function called at each update.
    /// This function calculates movement and rotation, opens doors, syncs positions etc.
    /// </summary>
    /// <param name="update">Determines whether the base enemy AI Update() should be called</param>
    /// <param name="doAIInterval">Determines whether the base enemy AI DoAIInterval() should be called</param>
    public virtual void OnUpdate(bool update = false, bool doAIInterval = true)
    {
        if (StartOfRound.Instance.inShipPhase) return;

        if (update)
            Enemy.Update();
        else
        {
            if (!Enemy.IsOwner)
            {
                if (Enemy.agent != null)
                {
                    Enemy.SetClientCalculatingAI(enable: false);
                }

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

    public virtual void LateUpdate()
    {
        if (IsTamed)
            UpdateNameTag();
    }

    public virtual void Awake()
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

    public virtual void Start()
    {
        //LethalMon.Logger.LogInfo($"LastDefaultBehaviourIndex for {Enemy.name} is {LastDefaultBehaviourIndex}");
        AddCustomBehaviours();

        if (IsTamed)
        {
            Enemy.Start();
            IsOutside = Utils.IsEnemyOutside(Enemy);
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
                scanNode.subText = Registry.IsEnemyRegistered(Enemy.enemyType.name)
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

    public virtual void DoAIInterval()
    {
        Enemy.DoAIInterval();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (ownerPlayer != null)
            Cache.RemovePlayerPet(ownerPlayer);
        
        foreach (var cooldown in GetComponents<CooldownNetworkBehaviour>())
            cooldown.OnDestroy();

        if (IsOwnerPlayer)
            HUDManagerPatch.EnableHUD(false);

        if (IsTamed && !base.IsServer) // Counter to EnemyAI.Start()
            RoundManager.Instance.SpawnedEnemies.Remove(Enemy);

        HideNameTag();
    }

    /// <summary>
    /// Makes the enemy turn progressively towards a position.
    /// </summary>
    /// <param name="position">The position to turn towards</param>
    public virtual void TurnTowardsPosition(Vector3 position)
    {
        Transform enemyTransform = Enemy.transform;
        Vector3 direction = position - enemyTransform.position;
        direction.y = 0f;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        enemyTransform.rotation = Quaternion.Slerp(enemyTransform.rotation, targetRotation, Time.deltaTime);
    }

    /// <summary>
    /// Makes the enemy move towards a position.
    /// </summary>
    /// <param name="position">The position to move towards</param>
    public virtual void MoveTowards(Vector3 position)
    {
        Enemy.SetDestinationToPosition(position);
    }

    /// <summary>
    /// Function called when the enemy is retrieved in the ball.
    /// </summary>
    /// <param name="position">The position to spawn the ball</param>
    /// <returns>The ball item</returns>
    public virtual PokeballItem? RetrieveInBall(Vector3 position)
    {
        HasBeenRetrieved = true;
        
        GameObject? spawnPrefab = BallTypeMethods.GetPrefab(BallType);
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
        pokeballItem.scrapPersistedThroughRounds = ScrapPersistedThroughRounds || AlreadyCollectedThisRound;
        pokeballItem.SetScrapValue(BallValue);
        ball.GetComponent<NetworkObject>().Spawn(false);
        if (EnemySkinRegistryCompatibility.Instance.Enabled)
            pokeballItem.enemySkinRegistryId = EnemySkinRegistryId;
        pokeballItem.SetCaughtEnemyServerRpc(Enemy.enemyType.name, pokeballItem.enemySkinRegistryId);
        pokeballItem.isDnaComplete = IsDnaComplete;
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
        _nameText.text = $"{ownerPlayer!.playerUsername}'s\n{Registry.GetCatchableEnemy(Enemy.enemyType.name)?.DisplayName}";
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
    /// <summary>
    /// Makes the enemy follow a position.
    /// </summary>
    /// <param name="targetPosition">The position to follow</param>
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

    /// <summary>
    /// Places the enemy on the closer navMesh.
    /// </summary>
    public void PlaceOnNavMesh() => PlaceEnemyOnNavMesh(Enemy);

    
    private static void PlaceEnemyOnNavMesh(EnemyAI enemyAI)
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

    /// <summary>
    /// Makes the enemy follow the owner.
    /// </summary>
    public void FollowOwner()
    {
        if (ownerPlayer == null) return;
        
        var entranceTeleportRequired = ownerPlayer.isInsideFactory == IsOutside;
        if(entranceTeleportRequired != _followingRequiresEntrance)
        {
            _followingRequiresEntrance = entranceTeleportRequired;
            HUDManagerPatch.UpdateTamedMonsterAction(GetCurrentStateDescription());
        }

        if(ownerPlayer.isInsideFactory == IsOutside)
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

    /// <summary>
    /// Checks if the enemy meets the targeting conditions towards the specified enemy.
    /// This function is used to determine if the enemy can switch to the defending state by targeting the specified enemy.
    /// </summary>
    /// <param name="enemyAI">The enemy to check</param>
    /// <returns>Whether the enemy meets the targeting conditions</returns>
    public virtual bool EnemyMeetsTargetingConditions(EnemyAI enemyAI)
    {
        if (Targets == TargetType.Dead && !enemyAI.isEnemyDead || Targets == TargetType.Alive && enemyAI.isEnemyDead) return false;

        if(TargetOnlyKillableEnemies && !enemyAI.enemyType.canDie) return false;

        return enemyAI.gameObject.activeSelf && enemyAI.gameObject.layer != (int)Utils.LayerMasks.Mask.EnemiesNotRendered &&
            !(enemyAI.TryGetComponent(out TamedEnemyBehaviour tamedBehaviour) && tamedBehaviour.IsOwnedByAPlayer());
    }

    /// <summary>
    /// Function called when the enemy finds a target.
    /// By default, it makes the enemy switch to the defending state.
    /// </summary>
    public virtual void OnFoundTarget() => SwitchToTamingBehaviour(TamingBehaviour.TamedDefending);

    /// <summary>
    /// Targets the nearest enemy if one is found.
    /// This function is skipped if called too frequently, to avoid performance issues.
    /// It is called every 0.5 second if an enemy was found in the last search, otherwise every 1 second.
    /// </summary>
    /// <param name="requireLOS">If true, the enemy must be in line of sight to be targeted</param>
    /// <param name="fromOwnerPerspective">If true, the search is done from the owner's perspective</param>
    /// <param name="angle">The angle in which the enemy must be in line of sight</param>
    public void TargetNearestEnemy(bool requireLOS = true, bool fromOwnerPerspective = true, float angle = 180f)
    {
        _targetNearestEnemyInterval -= Time.deltaTime;
        if (_targetNearestEnemyInterval > 0)
            return;

        _targetNearestEnemyInterval = _foundEnemiesInRangeInLastSearch ? 0.5f : 1f; // More frequent search if enemy that met the conditions was in range
        
        var target = NearestEnemy(requireLOS, fromOwnerPerspective, angle);
        if(target != null)
        {
            targetEnemy = target;
            OnFoundTarget();
            LethalMon.Log("Targeting " + targetEnemy.enemyType.name);
        }
    }

    /// <summary>
    /// Get the nearest targetable enemy.
    /// </summary>
    /// <param name="requireLOS">If true, the enemy must be in line of sight to be targeted</param>
    /// <param name="fromOwnerPerspective">If true, the search is done from the owner's perspective</param>
    /// <param name="angle">The angle in which the enemy must be in line of sight</param>
    /// <returns>The nearest targetable enemy</returns>
    public EnemyAI? NearestEnemy(bool requireLOS = true, bool fromOwnerPerspective = true, float angle = 180f)
    {
        _foundEnemiesInRangeInLastSearch = false;
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

            _foundEnemiesInRangeInLastSearch = true;
            
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
    
    private static bool OptimizedCheckLineOfSightForPosition(Vector3 startPosition, Vector3 targetPosition, Vector3 forward, float angle)
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

    /// <summary>
    /// Drops a random blood amount at the specified position.
    /// </summary>
    /// <param name="position">The position to drop blood</param>
    /// <param name="minAmount">The minimum amount of blood to drop</param>
    /// <param name="maxAmount">The maximum amount of blood to drop</param>
    public void DropBlood(Vector3 position, int minAmount = 3, int maxAmount = 7)
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

    internal void SetCooldownTimers(Dictionary<string, Tuple<float, DateTime>> cooldownsTimers)
    {
        DateTime now = SystemClock.now;
        foreach (KeyValuePair<string, Tuple<float, DateTime>> cooldownTimer in cooldownsTimers)
        {
            GetComponents<CooldownNetworkBehaviour>().FirstOrDefault(cooldown => cooldown.Id != null && cooldown.Id.Value.Value == cooldownTimer.Key)?.InitTimer(cooldownTimer.Value.Item1 + (float) (now - cooldownTimer.Value.Item2).TotalSeconds);
        }
    }

    /// <summary>
    /// Gets a <see cref="CooldownNetworkBehaviour"/> from its id.
    /// </summary>
    /// <param name="id">The id of the cooldown</param>
    /// <returns>The cooldown network behaviour</returns>
    public CooldownNetworkBehaviour GetCooldownWithId(string id)
    {
        return GetComponents<CooldownNetworkBehaviour>().FirstOrDefault(cooldown => cooldown.Id != null && cooldown.Id.Value.Value == id);
    }
    
    /// <summary>
    /// Check if the enemy is owned by a player (= tamed).
    /// </summary>
    /// <returns>Whether the enemy is owned by a player</returns>
    public bool IsOwnedByAPlayer()
    {
        return ownerPlayer != null;
    }

    /// <summary>
    /// Check if the current behaviour is the specified taming behaviour.
    /// </summary>
    /// <param name="behaviour">The taming behaviour to check</param>
    /// <returns>Whether the current behaviour is the specified taming behaviour</returns>
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

    /// <summary>
    /// Teleports the enemy to the specified position.
    /// </summary>
    /// <param name="position">The position to teleport the enemy</param>
    /// <param name="placeOnNavMesh">Whether to place the enemy on the navMesh</param>
    /// <param name="syncPosition">Whether to sync the position to clients</param>
    public void Teleport(Vector3 position, bool placeOnNavMesh = false, bool syncPosition = false) => TeleportEnemy(Enemy, position, placeOnNavMesh, syncPosition);

    /// <summary>
    /// Teleports the specified enemy to the specified position.
    /// </summary>
    /// <param name="enemyAI">The enemy to teleport</param>
    /// <param name="position">The position to teleport the enemy</param>
    /// <param name="placeOnNavMesh">Whether to place the enemy on the navMesh</param>
    /// <param name="syncPosition">Whether to sync the position to clients</param>
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
            tamedBehaviour.IsOutside = Utils.IsEnemyOutside(enemyAI);
        }
    }

    /// <summary>
    /// Function called to check if the enemy can be teleported.
    /// </summary>
    /// <returns>Whether the enemy can be teleported</returns>
    public virtual bool CanBeTeleported()
    {
        return true;
    }

    /// <summary>
    /// Function called when an enemy collides with this tamed enemy.
    /// </summary>
    /// <param name="other">The collider of the enemy</param>
    /// <param name="collidedEnemy">The enemy that collided with this tamed enemy</param>
    public virtual void OnCollideWithEnemy(Collider other, EnemyAI collidedEnemy)
    {
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