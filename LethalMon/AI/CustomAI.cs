using System;
using GameNetcodeStuff;
using LethalMon.Items;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace LethalMon.AI;

/// <summary>
/// The AI of a captured monster
/// </summary>
public class CustomAI : EnemyAI
{
	/// <summary>
	/// The owner of the CustomAI
	/// </summary>
    public PlayerControllerB ownerPlayer;
    
	/// <summary>
	/// Owner client ID
	/// </summary>
    public ulong ownClientId;

	/// <summary>
	/// The ball used to capture the monster
	/// </summary>
	public BallType ballType;
	
	/// <summary>
	/// The value of the ball used to capture the monster
	/// </summary>
    public int ballValue;

	/// <summary>
	/// True if the ball of the monster was present in the previous round
	/// </summary>
    public bool scrapPersistedThroughRounds;

	/// <summary>
	/// True if the ball has already been collected this round
	/// </summary>
    public bool alreadyCollectedThisRound;

    /// <summary>
    /// Previous position of the AI
    /// </summary>
    protected Vector3 previousPosition;
    
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
    /// Make the AI follows the owner
    /// </summary>
    public void FollowOwner()
    {
	    Vector3 potentialPosition1, potentialPosition2;

	    if (Vector3.Distance(this.transform.position, ownerPlayer.transform.position) > 30f)
	    {
		    this.agent.enabled = false;
		    this.transform.position = Utils.GetPositionBehindPlayer(this.ownerPlayer);
		    this.agent.enabled = true;
	    }
	    else if (FindRaySphereIntersections(this.transform.position,  (ownerPlayer.transform.position - this.transform.position).normalized, ownerPlayer.transform.position, 8f,
		        out potentialPosition1,
		        out potentialPosition2))
	    {
		    var position = this.transform.position;
		    float distance1 = Vector3.Distance(position, potentialPosition1);
		    float distance2 = Vector3.Distance(position, potentialPosition2);

		    if (distance1 > 4f && distance2 > 4f)
		    {
			    previousPosition = base.transform.position;
			    SetDestinationToPosition(distance1 < distance2 ? potentialPosition1 : potentialPosition2);   
		    }
	    }
	    
	    // todo else turn in the direction of the owner
    }

    /// <summary>
    /// Function called at regular interval. Put the AI logic in there
    /// </summary>
    public override void DoAIInterval()
    {
	    if (this.openDoorSpeedMultiplier > 0f)
	    {
		    Collider[] colliders = Physics.OverlapSphere(this.transform.position, 0.5f);
		    foreach (Collider collider in colliders)
		    {
			    DoorLock doorLock = collider.GetComponentInParent<DoorLock>();
			    if (doorLock != null && !doorLock.isDoorOpened && !doorLock.isLocked)
			    {
				    Debug.Log("CustomAI opens door");
				    if (doorLock.gameObject.TryGetComponent(out AnimatedObjectTrigger trigger))
				    {
					    trigger.TriggerAnimationNonPlayer(false, true, false);
				    }
				    doorLock.OpenDoorAsEnemyServerRpc();
			    }
		    }
	    }
	    
	    base.DoAIInterval();
    }

    /// <summary>
    /// Called when Unity updates the GameObject
    /// </summary>
    public override void Update()
    {
		if (stunnedIndefinitely <= 0)
		{
			if (stunNormalizedTimer >= 0f)
			{
				stunNormalizedTimer -= Time.deltaTime / enemyType.stunTimeMultiplier;
			}
			else
			{
				stunnedByPlayer = null;
				if (postStunInvincibilityTimer >= 0f)
				{
					postStunInvincibilityTimer -= Time.deltaTime * 5f;
				}
			}
		}
		if (!ventAnimationFinished)
		{
			ventAnimationFinished = true;
			if (creatureAnimator != null)
			{
				creatureAnimator.SetBool("inSpawningAnimation", value: false);
			}
		}
		if (!base.IsOwner)
		{
			SetClientCalculatingAI(enable: false);
			if (!inSpecialAnimation)
			{
				base.transform.position = Vector3.SmoothDamp(base.transform.position, serverPosition, ref tempVelocity, syncMovementSpeed);
				base.transform.eulerAngles = new Vector3(base.transform.eulerAngles.x, Mathf.LerpAngle(base.transform.eulerAngles.y, targetYRotation, 15f * Time.deltaTime), base.transform.eulerAngles.z);
			}
			timeSinceSpawn += Time.deltaTime;
			return;
		}
		if (isEnemyDead)
		{
			SetClientCalculatingAI(enable: false);
			return;
		}
		if (!inSpecialAnimation)
		{
			SetClientCalculatingAI(enable: true);
		}
		if (movingTowardsTargetPlayer && targetPlayer != null)
		{
			if (setDestinationToPlayerInterval <= 0f)
			{
				setDestinationToPlayerInterval = 0.25f;
				destination = RoundManager.Instance.GetNavMeshPosition(targetPlayer.transform.position, RoundManager.Instance.navHit, 2.7f);
				Debug.Log("Set destination to target player A");
			}
			else
			{
				destination = new Vector3(targetPlayer.transform.position.x, destination.y, targetPlayer.transform.position.z);
				Debug.Log("Set destination to target player B");
				setDestinationToPlayerInterval -= Time.deltaTime;
			}
			if (addPlayerVelocityToDestination > 0f)
			{
				if (targetPlayer == GameNetworkManager.Instance.localPlayerController)
				{
					destination += Vector3.Normalize(targetPlayer.thisController.velocity * 100f) * addPlayerVelocityToDestination;
				}
				else if (targetPlayer.timeSincePlayerMoving < 0.25f)
				{
					destination += Vector3.Normalize((targetPlayer.serverPlayerPosition - targetPlayer.oldPlayerPosition) * 100f) * addPlayerVelocityToDestination;
				}
			}
		}
		if (inSpecialAnimation)
		{
			return;
		}
		if (updateDestinationInterval >= 0f)
		{
			updateDestinationInterval -= Time.deltaTime;
		}
		else
		{
			DoAIInterval();
			updateDestinationInterval = AIIntervalTime;
		}
		if (Mathf.Abs(previousYRotation - base.transform.eulerAngles.y) > 6f)
		{
			previousYRotation = base.transform.eulerAngles.y;
			targetYRotation = previousYRotation;
			if (base.IsServer)
			{
				UpdateEnemyRotationClientRpc((short)previousYRotation);
			}
			else
			{
				UpdateEnemyRotationServerRpc((short)previousYRotation);
			}
		}
    }

    /// <summary>
    /// Unity Start function
    /// </summary>
    public override void Start()
    {
        try
        {
            agent = base.gameObject.GetComponentInChildren<NavMeshAgent>();
            skinnedMeshRenderers = base.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            meshRenderers = base.gameObject.GetComponentsInChildren<MeshRenderer>();
            creatureAnimator = base.gameObject.GetComponentInChildren<Animator>();
            thisNetworkObject = base.gameObject.GetComponentInChildren<NetworkObject>();
            allAINodes = GameObject.FindGameObjectsWithTag("AINode");
            path1 = new NavMeshPath();
            openDoorSpeedMultiplier = enemyType.doorSpeedMultiplier;
            serverPosition = base.transform.position;
            previousPosition = base.transform.position;
            if (base.IsOwner)
            {
                SyncPositionToClients();
            }
            else
            {
                SetClientCalculatingAI(enable: false);
            }

            if (creatureAnimator != null)
            {
	            creatureAnimator.SetBool("inSpawningAnimation", value: false);
            }
        }
        catch (Exception arg)
        {
            Debug.LogError($"Error when initializing enemy variables for {base.gameObject.name} : {arg}");
        }
    }

    /// <summary>
    /// Retrieve this CustomAI in a ball
    /// </summary>
    /// <param name="position">The position of the ball</param>
    /// <returns></returns>
    public virtual PokeballItem RetrieveInBall(Vector3 position)
    {
	    GameObject ball;
	    switch (this.ballType)
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
	    pokeballItem.scrapPersistedThroughRounds = this.scrapPersistedThroughRounds || this.alreadyCollectedThisRound;
	    pokeballItem.SetScrapValue(this.ballValue);
	    ball.GetComponent<NetworkObject>().Spawn(false);
	    pokeballItem.SetCaughtEnemy(this.enemyType);
	    pokeballItem.FallToGround();

		this.GetComponent<NetworkObject>().Despawn(true);

	    return pokeballItem;
    }

    /// <summary>
    /// Copy properties from the original EnemyAI that will be used by this CustomAI
    /// </summary>
    /// <param name="enemyAI">Original AI</param>
    public virtual void CopyProperties(EnemyAI enemyAI)
    {
	    this.enemyType = enemyAI.enemyType;
	    this.creatureSFX = enemyAI.creatureSFX;
	    this.creatureVoice = enemyAI.creatureVoice;
	    this.eye = enemyAI.eye;
    }
}