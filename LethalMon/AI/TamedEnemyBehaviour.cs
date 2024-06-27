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

	public EnemyAI? enemy = null;

    public PlayerControllerB? ownerPlayer = null;

    public ulong ownClientId;

    public BallType ballType;
    
    protected Vector3 previousPosition;

    public int ballValue;

    public bool scrapPersistedThroughRounds;

    public bool alreadyCollectedThisRound;

	public NetworkVariable<bool> IsEnabled = new NetworkVariable<bool>(false);

	#region CustomBehaviour
	public enum CustomBehaviour
	{
		TamedFollowing = 1,
		TamedDefending = 2,
		EscapedFromBall = 3
	}

	public void SwitchToCustomBehaviour(CustomBehaviour behaviour) => enemy.SwitchToBehaviourState(LastDefaultBehaviourIndex[enemy.GetType()] + (int)behaviour);

    public static Dictionary<Type, int> LastDefaultBehaviourIndex = new Dictionary<Type, int>();

    [HarmonyPrefix, HarmonyPatch(typeof(GameNetworkManager), "Start")]
    public static void AddCustomBehaviours()
    {
		foreach(var enemyType in Utils.EnemyTypes)
		{
			if (enemyType?.enemyPrefab == null || !enemyType.enemyPrefab.TryGetComponent(out EnemyAI enemyAI)) continue;
            {
                LastDefaultBehaviourIndex.Add(enemyAI.GetType(), enemyAI.enemyBehaviourStates.Length - 1);

                // Behaviour states
                enemyAI.enemyBehaviourStates = new List<EnemyBehaviourState>(enemyAI.enemyBehaviourStates)
                {
                    new EnemyBehaviourState() { name = "TamedFollowing" },
                    new EnemyBehaviourState() { name = "TamedDefending" },
                    new EnemyBehaviourState() { name = "EscapedFromBall" }
                }.ToArray();


                LethalMon.Logger.LogInfo("Added custom behaviourStates for " + enemyType.enemyName);

                // Behaviour controller
                var aiType = BehaviourClassMapping.GetValueOrDefault(enemyAI.GetType(), typeof(TamedEnemyBehaviour));
                var tamedBehaviour = enemyType.enemyPrefab.gameObject.AddComponent(aiType) as TamedEnemyBehaviour;
                if (tamedBehaviour == null)
                {
                    LethalMon.Logger.LogWarning($"TamedEnemyBehaviour initialization failed for {enemyType.enemyName}");
                    return;
                }
                LethalMon.Logger.LogInfo($"Added TamedEnemyBehaviour for {enemyType.enemyName}");
            }
        }
    }

	// Skip original code if using enemy is at a custom behaviour
	internal static bool IsUsingCustomBehaviour(EnemyAI enemyAI) => enemyAI.currentBehaviourStateIndex > LastDefaultBehaviourIndex.GetValueOrDefault(enemyAI.GetType(), int.MaxValue);

    [HarmonyPrefix, HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.Update))]
    public static bool UpdatePrefix(EnemyAI __instance) => IsUsingCustomBehaviour(__instance);

    /*[HarmonyPrefix, HarmonyPatch(typeof(EnemyAI), "LateUpdate")]
    public static bool LateUpdatePrefix(EnemyAI __instance) => IsUsingCustomBehaviour(__instance);*/

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
	}
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

	    if (Vector3.Distance(enemy.transform.position, ownerPlayer.transform.position) > 30f)
	    {
            enemy.agent.enabled = false;
            enemy.transform.position = Utils.GetPositionBehindPlayer(ownerPlayer);
            enemy.agent.enabled = true;
	    }
	    else if (FindRaySphereIntersections(enemy.transform.position,  (ownerPlayer.transform.position - enemy.transform.position).normalized, ownerPlayer.transform.position, 8f,
		        out Vector3 potentialPosition1,
		        out Vector3 potentialPosition2))
	    {
		    var position = enemy.transform.position;
		    float distance1 = Vector3.Distance(position, potentialPosition1);
		    float distance2 = Vector3.Distance(position, potentialPosition2);

		    if (distance1 > 4f && distance2 > 4f)
		    {
			    previousPosition = base.transform.position;
			    enemy.SetDestinationToPosition(distance1 < distance2 ? potentialPosition1 : potentialPosition2);   
		    }
	    }
	    
	    // todo else turn in the direction of the owner
    }

    public virtual void DoAIInterval()
    {
		if(!IsUsingCustomBehaviour(enemy)) return; // TODO: rework using switch-case

	    if (enemy.openDoorSpeedMultiplier > 0f)
	    {
		    Collider[] colliders = Physics.OverlapSphere(enemy.transform.position, 0.5f);
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
	    
	    enemy.DoAIInterval();
    }

    public virtual void Update()
    {
        if (!IsUsingCustomBehaviour(enemy)) return; // TODO: rework using switch-case

        if (enemy.stunnedIndefinitely <= 0)
		{
			if (enemy.stunNormalizedTimer >= 0f)
			{
                enemy.stunNormalizedTimer -= Time.deltaTime / enemy.enemyType.stunTimeMultiplier;
			}
			else
			{
                enemy.stunnedByPlayer = null;
				if (enemy.postStunInvincibilityTimer >= 0f)
				{
                    enemy.postStunInvincibilityTimer -= Time.deltaTime * 5f;
				}
			}
		}
		if (!enemy.ventAnimationFinished)
		{
			enemy.ventAnimationFinished = true;
			if (enemy.creatureAnimator != null)
			{
				enemy.creatureAnimator.SetBool("inSpawningAnimation", value: false);
			}
		}
		if (!base.IsOwner)
		{
            enemy.SetClientCalculatingAI(enable: false);
			if (!enemy.inSpecialAnimation)
			{
				base.transform.position = Vector3.SmoothDamp(base.transform.position, enemy.serverPosition, ref enemy.tempVelocity, enemy.syncMovementSpeed);
				base.transform.eulerAngles = new Vector3(base.transform.eulerAngles.x, Mathf.LerpAngle(base.transform.eulerAngles.y, enemy.targetYRotation, 15f * Time.deltaTime), base.transform.eulerAngles.z);
			}
            enemy.timeSinceSpawn += Time.deltaTime;
			return;
		}
		if (enemy.isEnemyDead)
		{
            enemy.SetClientCalculatingAI(enable: false);
			return;
		}
		if (!enemy.inSpecialAnimation)
		{
			enemy.SetClientCalculatingAI(enable: true);
		}
		if (enemy.movingTowardsTargetPlayer && enemy.targetPlayer != null)
		{
			if (enemy.setDestinationToPlayerInterval <= 0f)
			{
                enemy.setDestinationToPlayerInterval = 0.25f;
                enemy.destination = RoundManager.Instance.GetNavMeshPosition(enemy.targetPlayer.transform.position, RoundManager.Instance.navHit, 2.7f);
				Debug.Log("Set destination to target player A");
			}
			else
			{
                enemy.destination = new Vector3(enemy.targetPlayer.transform.position.x, enemy.destination.y, enemy.targetPlayer.transform.position.z);
				Debug.Log("Set destination to target player B");
                enemy.setDestinationToPlayerInterval -= Time.deltaTime;
			}
			if (enemy.addPlayerVelocityToDestination > 0f)
			{
				if (enemy.targetPlayer == GameNetworkManager.Instance.localPlayerController)
				{
                    enemy.destination += Vector3.Normalize(enemy.targetPlayer.thisController.velocity * 100f) * enemy.addPlayerVelocityToDestination;
				}
				else if (enemy.targetPlayer.timeSincePlayerMoving < 0.25f)
				{
                    enemy.destination += Vector3.Normalize((enemy.targetPlayer.serverPlayerPosition - enemy.targetPlayer.oldPlayerPosition) * 100f) * enemy.addPlayerVelocityToDestination;
				}
			}
		}
		if (enemy.inSpecialAnimation)
		{
			return;
		}
		if (enemy.updateDestinationInterval >= 0f)
		{
            enemy.updateDestinationInterval -= Time.deltaTime;
		}
		else
		{
			DoAIInterval();
			enemy.updateDestinationInterval = enemy.AIIntervalTime;
		}
		if (Mathf.Abs(enemy.previousYRotation - base.transform.eulerAngles.y) > 6f)
		{
			enemy.previousYRotation = base.transform.eulerAngles.y;
            enemy.targetYRotation = enemy.previousYRotation;
			if (base.IsServer)
			{
                enemy.UpdateEnemyRotationClientRpc((short)enemy.previousYRotation);
			}
			else
			{
                enemy.UpdateEnemyRotationServerRpc((short)enemy.previousYRotation);
			}
		}
    }

    public virtual void Start()
    {
        try
        {
            enemy = gameObject.GetComponent<EnemyAI>();
            if (enemy != null)
                LethalMon.Logger.LogInfo("Set enemy variable for " + GetType().Name);

            enemy.agent = base.gameObject.GetComponentInChildren<NavMeshAgent>();
            enemy.skinnedMeshRenderers = base.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            enemy.meshRenderers = base.gameObject.GetComponentsInChildren<MeshRenderer>();
            enemy.creatureAnimator = base.gameObject.GetComponentInChildren<Animator>();
            enemy.thisNetworkObject = base.gameObject.GetComponentInChildren<NetworkObject>();
            enemy.allAINodes = GameObject.FindGameObjectsWithTag("AINode");
            enemy.path1 = new NavMeshPath();
            enemy.openDoorSpeedMultiplier = enemy.enemyType.doorSpeedMultiplier;
            enemy.serverPosition = base.transform.position;
            previousPosition = base.transform.position;
            if (base.IsOwner)
            {
                enemy.SyncPositionToClients();
            }
            else
            {
                enemy.SetClientCalculatingAI(enable: false);
            }

            if (enemy.creatureAnimator != null)
            {
                enemy.creatureAnimator.SetBool("inSpawningAnimation", value: false);
            }
        }
        catch (Exception arg)
        {
            Debug.LogError($"Error when initializing enemy variables for {base.gameObject.name} : {arg}");
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
	    pokeballItem.SetCaughtEnemy(enemy.enemyType);
	    pokeballItem.FallToGround();

		enemy.GetComponent<NetworkObject>().Despawn(true);

	    return pokeballItem;
    }
}