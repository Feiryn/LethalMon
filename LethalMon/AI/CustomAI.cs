using System;
using GameNetcodeStuff;
using LethalMon.Items;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace LethalMon.AI;

public class CustomAI : EnemyAI
{
    public PlayerControllerB ownerPlayer;
    
    public ulong ownClientId;

    public BallType ballType;
    
    protected Vector3 previousPosition;

    public int ballValue;

    public bool scrapPersistedThroughRounds;

    public bool alreadyCollectedThisRound;
    
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
		    float distance1 = Vector3.Distance(this.transform.position, potentialPosition1);
		    float distance2 = Vector3.Distance(this.transform.position, potentialPosition2);

		    if (distance1 > 4f && distance2 > 4f)
		    {
			    previousPosition = base.transform.position;
			    SetDestinationToPosition(distance1 < distance2 ? potentialPosition1 : potentialPosition2);   
		    }
	    }
    }

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
	    pokeballItem.SetCaughtEnemy(this.enemyType);
	    ball.GetComponent<NetworkObject>().Spawn(false);
	    pokeballItem.FallToGround();

		this.GetComponent<NetworkObject>().Despawn(true);

	    return pokeballItem;
    }

    public virtual void CopyProperties(EnemyAI enemyAI)
    {
	    this.enemyType = enemyAI.enemyType;
	    this.creatureSFX = enemyAI.creatureSFX;
	    this.creatureVoice = enemyAI.creatureVoice;
    }
}