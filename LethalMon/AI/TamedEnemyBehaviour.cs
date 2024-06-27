using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using HarmonyLib;
using LethalMon.Items;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace LethalMon.AI;

public class TamedEnemyBehaviour : NetworkBehaviour
{
	internal static readonly Dictionary<Type, Type> BehaviourClassMapping = new Dictionary<Type, Type>
    {
        { typeof(FlowermanAI),		typeof(FlowermanTamedBehaviour) },
        { typeof(RedLocustBees),	typeof(RedLocustBeesTamedBehaviour) },
        { typeof(HoarderBugAI),		typeof(HoarderBugTamedBehaviour) }
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

    public ulong ownClientId;

    public BallType ballType;
    
    protected Vector3 previousPosition;

    public int ballValue;

    public bool scrapPersistedThroughRounds;

    public bool alreadyCollectedThisRound;

	public NetworkVariable<bool> IsEnabled = new NetworkVariable<bool>(false);

	private int LastDefaultBehaviourIndex = 0;

	#region CustomBehaviour
	public enum CustomBehaviour
	{
		TamedFollowing = 1,
		TamedDefending = 2,
		EscapedFromBall = 3
    }

    public void SwitchToCustomBehaviour(CustomBehaviour behaviour)
    {
        Enemy.SwitchToBehaviourState(LastDefaultBehaviourIndices[Enemy.GetType()] + (int)behaviour);
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
		foreach(var enemyType in Utils.EnemyTypes)
		{
			enemyCount++;
			if (enemyType?.enemyPrefab == null || !enemyType.enemyPrefab.TryGetComponent(out EnemyAI enemyAI)) continue;

            LastDefaultBehaviourIndices.Add(enemyAI.GetType(), enemyAI.enemyBehaviourStates.Length - 1);

            // Behaviour states
            enemyAI.enemyBehaviourStates = new List<EnemyBehaviourState>(enemyAI.enemyBehaviourStates)
            {
                new EnemyBehaviourState() { name = "TamedFollowing" },
                new EnemyBehaviourState() { name = "TamedDefending" },
                new EnemyBehaviourState() { name = "EscapedFromBall" }
            }.ToArray();

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
        }

        LethalMon.Logger.LogInfo($"Added {addedDefaultCustomBehaviours} more custom default behaviours. {addedBehaviours}/{enemyCount} enemy behaviours were added.");
    }

	// Skip original code if using enemy is at a custom behaviour
	internal static bool IsUsingCustomBehaviour(EnemyAI enemyAI) => enemyAI.currentBehaviourStateIndex > LastDefaultBehaviourIndices.GetValueOrDefault(enemyAI.GetType(), int.MaxValue);

	[HarmonyPrefix, HarmonyPatch(typeof(RedLocustBees), nameof(RedLocustBees.Update))]
	public static void UpdateBeesPrefix(RedLocustBees __instance)
	{
		LethalMon.Logger.LogInfo("RedLocustBees.Update");
	}

    /*[HarmonyPrefix, HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.Update))]
    public static bool UpdatePrefix(EnemyAI __instance) => IsUsingCustomBehaviour(__instance);

    [HarmonyPrefix, HarmonyPatch(typeof(EnemyAI), "LateUpdate")]
    public static bool LateUpdatePrefix(EnemyAI __instance) => IsUsingCustomBehaviour(__instance);

	[HarmonyPrefix, HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.DoAIInterval))]
	public static bool DoAIIntervalPrefix(EnemyAI __instance)
	{
		if(IsUsingCustomBehaviour(__instance))
		{
			if (__instance.gameObject.TryGetComponent(out TamedEnemyBehaviour tamedBehaviour))
                tamedBehaviour.DoAIInterval();

            return false;
		}
		return true;
	}*/

    internal virtual void OnTamedFollowing() { }
    internal virtual void OnTamedDefending() { }
    internal virtual void OnEscapedFromBall() { }
    #endregion

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

    public void FollowOwner()
    {
        if (ownerPlayer == null) return;

	    if (Vector3.Distance(Enemy.transform.position, ownerPlayer.transform.position) > 30f)
	    {
            Enemy.agent.enabled = false;
            Enemy.transform.position = Utils.GetPositionBehindPlayer(ownerPlayer);
            Enemy.agent.enabled = true;
	    }
	    else if (FindRaySphereIntersections(Enemy.transform.position,  (ownerPlayer.transform.position - Enemy.transform.position).normalized, ownerPlayer.transform.position, 8f,
		        out Vector3 potentialPosition1,
		        out Vector3 potentialPosition2))
	    {
		    var position = Enemy.transform.position;
		    float distance1 = Vector3.Distance(position, potentialPosition1);
		    float distance2 = Vector3.Distance(position, potentialPosition2);

		    if (distance1 > 4f && distance2 > 4f)
		    {
			    previousPosition = base.transform.position;
			    Enemy.SetDestinationToPosition(distance1 < distance2 ? potentialPosition1 : potentialPosition2);   
		    }
	    }
	    
	    // todo else turn in the direction of the owner
    }

    public virtual void DoAIInterval()
    {
		if(!IsUsingCustomBehaviour(Enemy)) return; // TODO: rework using switch-case

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

    public virtual void Update()
    {
		var customBehaviour = Enemy.currentBehaviourStateIndex - LastDefaultBehaviourIndex;
		if (customBehaviour <= 0) return;

        switch((CustomBehaviour) customBehaviour)
        {
            case CustomBehaviour.TamedFollowing:
                OnTamedFollowing();
                break;
            case CustomBehaviour.TamedDefending:
                OnTamedDefending();
                break;
            case CustomBehaviour.EscapedFromBall:
                OnEscapedFromBall();
                break;
			default: break;
        }
		// todo: return here

        if (Enemy.stunnedIndefinitely <= 0)
		{
			if (Enemy.stunNormalizedTimer >= 0f)
			{
                Enemy.stunNormalizedTimer -= Time.deltaTime / Enemy.enemyType.stunTimeMultiplier;
			}
			else
			{
                Enemy.stunnedByPlayer = null;
				if (Enemy.postStunInvincibilityTimer >= 0f)
				{
                    Enemy.postStunInvincibilityTimer -= Time.deltaTime * 5f;
				}
			}
		}
		if (!Enemy.ventAnimationFinished)
		{
			Enemy.ventAnimationFinished = true;
			if (Enemy.creatureAnimator != null)
			{
				Enemy.creatureAnimator.SetBool("inSpawningAnimation", value: false);
			}
		}
		if (!base.IsOwner)
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
		if (Enemy.isEnemyDead)
		{
            Enemy.SetClientCalculatingAI(enable: false);
			return;
		}
		if (!Enemy.inSpecialAnimation)
		{
			Enemy.SetClientCalculatingAI(enable: true);
		}
		if (Enemy.movingTowardsTargetPlayer && Enemy.targetPlayer != null)
		{
			if (Enemy.setDestinationToPlayerInterval <= 0f)
			{
                Enemy.setDestinationToPlayerInterval = 0.25f;
                Enemy.destination = RoundManager.Instance.GetNavMeshPosition(Enemy.targetPlayer.transform.position, RoundManager.Instance.navHit, 2.7f);
				Debug.Log("Set destination to target player A");
			}
			else
			{
                Enemy.destination = new Vector3(Enemy.targetPlayer.transform.position.x, Enemy.destination.y, Enemy.targetPlayer.transform.position.z);
				Debug.Log("Set destination to target player B");
                Enemy.setDestinationToPlayerInterval -= Time.deltaTime;
			}
			if (Enemy.addPlayerVelocityToDestination > 0f)
			{
				if (Enemy.targetPlayer == GameNetworkManager.Instance.localPlayerController)
				{
                    Enemy.destination += Vector3.Normalize(Enemy.targetPlayer.thisController.velocity * 100f) * Enemy.addPlayerVelocityToDestination;
				}
				else if (Enemy.targetPlayer.timeSincePlayerMoving < 0.25f)
				{
                    Enemy.destination += Vector3.Normalize((Enemy.targetPlayer.serverPlayerPosition - Enemy.targetPlayer.oldPlayerPosition) * 100f) * Enemy.addPlayerVelocityToDestination;
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
			DoAIInterval();
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

    public virtual void Start()
    {
        try
        {
            LethalMon.Logger.LogInfo("Set enemy variables for " + GetType().Name);

            LastDefaultBehaviourIndex = LastDefaultBehaviourIndices[Enemy.GetType()];
            Enemy.agent = base.gameObject.GetComponentInChildren<NavMeshAgent>();
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

    public virtual PokeballItem RetrieveInBall(Vector3 position)
    {
	    GameObject ball;
	    switch (ballType)
	    {
		    case BallType.GREAT_BALL:
			    ball = Object.Instantiate(LethalMon.greatBallSpawnPrefab, position, Quaternion.Euler(new Vector3(0, 0f, 0f)));
			    break;
		    case BallType.ULTRA_BALL:
			    ball = Object.Instantiate(LethalMon.ultraBallSpawnPrefab, position, Quaternion.Euler(new Vector3(0, 0f, 0f)));
			    break;
		    case BallType.MASTER_BALL:
			    ball = Object.Instantiate(LethalMon.masterBallSpawnPrefab, position, Quaternion.Euler(new Vector3(0, 0f, 0f)));
			    break;
		    default:
			    ball = Object.Instantiate(LethalMon.pokeballSpawnPrefab, position, Quaternion.Euler(new Vector3(0, 0f, 0f)));
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

	    return pokeballItem;
    }
}