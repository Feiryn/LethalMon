using System;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using HarmonyLib;
using LethalMon.Items;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace LethalMon.AI;

public class TamedEnemyBehaviour : NetworkBehaviour
{
    internal static readonly Dictionary<Type, Type> BehaviourClassMapping = new Dictionary<Type, Type>
    {
        { typeof(FlowermanAI),      typeof(FlowermanTamedBehaviour) },
        { typeof(RedLocustBees),    typeof(RedLocustBeesTamedBehaviour) },
        { typeof(HoarderBugAI),     typeof(HoarderBugTamedBehaviour) }
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

    internal int LastDefaultBehaviourIndex = 0;

    public bool isOutsideOfBall = false;

    #region CustomBehaviour
    public enum TamingBehaviour
    {
        TamedFollowing = 1,
        TamedDefending = 2,
        EscapedFromBall = 3
    }
    private readonly int tamedBehaviourCount = Enum.GetNames(typeof(TamingBehaviour)).Length - 1;

    internal Dictionary<int, Action> CustomBehaviours = new Dictionary<int, Action>();

    internal virtual List<Tuple<string, Action>>? CustomBehaviourHandler => null;

    public void SwitchToTamingBehaviour(TamingBehaviour behaviour)
    {
        Enemy.SwitchToBehaviourState(LastDefaultBehaviourIndex + (int)behaviour);
        Enemy.enabled = false;
    }

    public void SwitchToCustomBehaviour(int behaviour)
    {
        Enemy.SwitchToBehaviourState(LastDefaultBehaviourIndex + tamedBehaviourCount + behaviour);
        Enemy.enabled = false;
    }

    public void SwitchToDefaultBehaviour(int behaviour)
    {
        Enemy.SwitchToBehaviourState(behaviour);
        Enemy.enabled = behaviour > LastDefaultBehaviourIndex;
    }

    public static Dictionary<Type, int> LastDefaultBehaviourIndices = new Dictionary<Type, int>();

    [HarmonyPrefix, HarmonyPatch(typeof(GameNetworkManager), "Start")]
    public static void AddCustomBehaviours()
    {
        int addedDefaultCustomBehaviours = 0, addedBehaviours = 0, enemyCount = 0;
        foreach (var enemyType in Utils.EnemyTypes)
        {
            enemyCount++;
            if (enemyType?.enemyPrefab == null || !enemyType.enemyPrefab.TryGetComponent(out EnemyAI enemyAI)) continue;

            // Behaviour controller
            var aiType = BehaviourClassMapping.GetValueOrDefault(enemyAI.GetType(), typeof(TamedEnemyBehaviour));
            var tamedBehaviour = enemyType.enemyPrefab.gameObject.AddComponent(aiType) as TamedEnemyBehaviour;
            if (tamedBehaviour == null)
            {
                LethalMon.Logger.LogWarning($"TamedEnemyBehaviour-Initialization failed for {enemyType.enemyName}");
                return;
            }

            addedBehaviours++;
            if (aiType == typeof(TamedEnemyBehaviour))
                addedDefaultCustomBehaviours++;
            else
                LethalMon.Logger.LogInfo($"Added {aiType.Name} for {enemyType.enemyName}");

            // Behaviour states
            LastDefaultBehaviourIndices.Add(enemyAI.GetType(), enemyAI.enemyBehaviourStates.Length - 1);

            var behaviourStateList = enemyAI.enemyBehaviourStates.ToList();

            // Add tamed behaviours
            foreach(var behaviourName in Enum.GetNames(typeof(TamingBehaviour)))
                behaviourStateList.Add(new EnemyBehaviourState() { name = behaviourName });

            // Add custom behaviours
            if (tamedBehaviour.CustomBehaviourHandler != null)
            {
                foreach (var customBehaviour in tamedBehaviour.CustomBehaviourHandler)
                {
                    behaviourStateList.Add(new EnemyBehaviourState() { name = customBehaviour.Item1 });
                    tamedBehaviour.CustomBehaviours.Add(behaviourStateList.Count - 1, customBehaviour.Item2);
                }
            }

            enemyAI.enemyBehaviourStates = behaviourStateList.ToArray();
        }

        LethalMon.Logger.LogInfo($"Added {addedDefaultCustomBehaviours} more custom default behaviours. {addedBehaviours}/{enemyCount} enemy behaviours were added.");
    }

    public void FollowOwner()
    {
        if (ownerPlayer == null) return;

        LethalMon.Logger.LogInfo("Follow owner");
        if (Vector3.Distance(Enemy.transform.position, ownerPlayer.transform.position) > 30f)
        {
            Enemy.agent.enabled = false;
            Enemy.transform.position = Utils.GetPositionBehindPlayer(ownerPlayer);
            Enemy.agent.enabled = true;
        }
        else if (FindRaySphereIntersections(Enemy.transform.position, (ownerPlayer.transform.position - Enemy.transform.position).normalized, ownerPlayer.transform.position, 8f,
                out Vector3 potentialPosition1,
                out Vector3 potentialPosition2))
        {
            var position = Enemy.transform.position;
            float distance1 = Vector3.Distance(position, potentialPosition1);
            float distance2 = Vector3.Distance(position, potentialPosition2);

            if (distance1 > 4f && distance2 > 4f)
            {
                LethalMon.Logger.LogInfo("Following to destination");
                previousPosition = Enemy.transform.position;
                Enemy.SetDestinationToPosition(distance1 < distance2 ? potentialPosition1 : potentialPosition2);


                if (Enemy.moveTowardsDestination)
                {
                    Enemy.agent.SetDestination(Enemy.destination);
                }
                Enemy.SyncPositionToClients();
            }
        }

        // todo else turn in the direction of the owner
    }

    internal virtual void OnTamedFollowing()
    {
        FollowOwner();
    }

    internal virtual void OnTamedDefending() {
        if (targetEnemy == null && targetEnemy == null) // lost target
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
    }

    internal virtual void OnEscapedFromBall() { }
    #endregion

    public void Update()
    {
        var customBehaviour = Enemy.currentBehaviourStateIndex - LastDefaultBehaviourIndex;
        if (customBehaviour <= 0) return;

        LethalMon.Logger.LogInfo($"TamedEnemyBehaviour.Update for {Enemy.name} -> {customBehaviour}");
        OnUpdate();

        if (customBehaviour > tamedBehaviourCount)
        {
            customBehaviour -= tamedBehaviourCount;
            if (CustomBehaviours.ContainsKey(customBehaviour) && CustomBehaviours[customBehaviour] != null)
                CustomBehaviours[customBehaviour]();
            else
                LethalMon.Logger.LogWarning($"Custom state {customBehaviour} has no handler.");
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
            case TamingBehaviour.EscapedFromBall:
                OnEscapedFromBall();
                break;
            default: break;
        }
    }
    public virtual void OnUpdate() // override this if you don't want the original Update() to be called
    {
        //Enemy.Update();
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

    public virtual void Start()
    {
        LastDefaultBehaviourIndex = LastDefaultBehaviourIndices.GetValueOrDefault(Enemy.GetType(), int.MaxValue);
        LethalMon.Logger.LogInfo($"LastDefaultBehaviourIndex for {Enemy.name} is {LastDefaultBehaviourIndex}");

        try
        {
            LethalMon.Logger.LogInfo("Set enemy variables for " + GetType().Name);

            LastDefaultBehaviourIndex = LastDefaultBehaviourIndices[Enemy.GetType()];
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
            Debug.LogError($"Error when initializing enemy variables for {base.gameObject.name} : {arg}");
            Destroy(this);
        }
    }

    public virtual void DoAIInterval()
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
                    Debug.Log("Tamed enemy opens door");
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
	    GameObject ball;
	    switch (ballType)
	    {
		    case BallType.GREAT_BALL:
			    ball = Instantiate(LethalMon.greatBallSpawnPrefab, position, Quaternion.Euler(new Vector3(0, 0f, 0f)));
			    break;
		    case BallType.ULTRA_BALL:
			    ball = Instantiate(LethalMon.ultraBallSpawnPrefab, position, Quaternion.Euler(new Vector3(0, 0f, 0f)));
			    break;
		    case BallType.MASTER_BALL:
			    ball = Instantiate(LethalMon.masterBallSpawnPrefab, position, Quaternion.Euler(new Vector3(0, 0f, 0f)));
			    break;
		    default:
			    ball = Instantiate(LethalMon.pokeballSpawnPrefab, position, Quaternion.Euler(new Vector3(0, 0f, 0f)));
			    break;
	    }

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
}