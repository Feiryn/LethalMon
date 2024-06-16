using System.Collections.Generic;
using HarmonyLib;
using LethalMon.Items;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.AI;

public class HoarderBugCustomAI : CustomAI
{
    private Vector3 agentLocalVelocity;
    
    public Transform animationContainer;
    
    private float velX;
    
    private float velZ;
    
    public GrabbableObject targetItem;
    
    public HoarderBugItem heldItem;
    
    private bool sendingGrabOrDropRPC;
    
    public Transform grabTarget;

    public const int searchTimer = 10;

    public int currentTimer = 0;
    
    public AudioClip bugFlySFX;
    
    public AudioClip[] chitterSFX;
    
    public override void Start()
    {
        base.Start();
        
        this.creatureAnimator.Play("Base Layer.Walking");
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();

        // Grab an item if one is close
        if (!GrabTargetItemIfClose())
        {
            // We currently have an item to give to the owner
            if (this.heldItem != null)
            {
                if (Vector3.Distance(this.transform.position, this.ownerPlayer.transform.position) < 6f)
                {
                    Debug.Log("HoarderBugAI drops held item to the owner");
                    this.DropItemAndCallDropRPC(heldItem.itemGrabbableObject.GetComponent<NetworkObject>());
                }
                else
                {
                    Debug.Log("HoarderBugAI move held item to the owner");
                    this.SetMovingTowardsTargetPlayer(this.ownerPlayer);   
                }
            }
            else
            {
                if (this.targetItem != null)
                {
                    Debug.Log("HoarderBugAI found an object and move towards it");
                    SetGoTowardsTargetObject(this.targetItem.gameObject);
                }
                else
                {
                    // Do not search too often
                    currentTimer++;
                    if (currentTimer >= HoarderBugCustomAI.searchTimer)
                    {
                        Debug.Log("HoarderBugAI searches for items");
                        currentTimer = 0;
                        Collider[] colliders = Physics.OverlapSphere(this.transform.position, 15f);
                        Debug.Log("HoarderBugAI found " + colliders.Length + " colliders");
                        
                        foreach (Collider collider in colliders)
                        {
                            GrabbableObject grabbable = collider.GetComponentInParent<GrabbableObject>();
                            if (grabbable != null)
                            {
                                Debug.Log("HoarderBugAI grabbable item (isInShipRoom: " + grabbable.isInShipRoom + ", isHeld: " + grabbable.isHeld + "): " + grabbable.name);
                                if (grabbable is { isInShipRoom: false, isHeld: false } && Vector3.Distance(grabbable.transform.position, this.ownerPlayer.transform.position) > 8f)
                                {
                                    // Check LOS
                                    if (!Physics.Linecast(this.transform.position, grabbable.transform.position, out var _, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
                                    {
                                        Debug.Log("HoarderBugAI found item: " + grabbable.name);
                                        this.targetItem = grabbable;
                                        return;   
                                    }
                                }
                            }
                        }
                    }

                    Debug.Log("HoarderBugAI follows the owner");
                    this.FollowOwner();
                }
            }
        }
        else
        {
            Debug.Log("HoarderBugAI grabbed close item");
        }
    }

    public override void Update()
    {
        base.Update();

        CalculateAnimationDirection();
    }
    
    private void CalculateAnimationDirection(float maxSpeed = 1f)
    {
        agentLocalVelocity = animationContainer.InverseTransformDirection(Vector3.ClampMagnitude(base.transform.position - previousPosition, 1f) / (Time.deltaTime * 2f));
        velX = Mathf.Lerp(velX, agentLocalVelocity.x, 10f * Time.deltaTime);
        creatureAnimator.SetFloat("VelocityX", Mathf.Clamp(velX, 0f - maxSpeed, maxSpeed));
        velZ = Mathf.Lerp(velZ, 0f - agentLocalVelocity.y, 10f * Time.deltaTime);
        creatureAnimator.SetFloat("VelocityZ", Mathf.Clamp(velZ, 0f - maxSpeed, maxSpeed));
        creatureAnimator.SetFloat("speedMultiplier", Vector3.ClampMagnitude(base.transform.position - previousPosition, 1f).sqrMagnitude / (Time.deltaTime / 4f));
        previousPosition = base.transform.position;
    }

    public override void CopyProperties(EnemyAI enemyAI)
    {
        base.CopyProperties(enemyAI);

        HoarderBugAI ai = ((HoarderBugAI)enemyAI);
        this.animationContainer = ai.animationContainer;
        this.grabTarget = ai.grabTarget;
        this.bugFlySFX = ai.bugFlySFX;
        this.chitterSFX = ai.chitterSFX;
    }

    public override PokeballItem RetrieveInBall(Vector3 position)
    {
        // Drop held item if any
        if (this.heldItem != null)
        {
            this.DropItemAndCallDropRPC(this.heldItem.itemGrabbableObject.GetComponent<NetworkObject>());
        }
        
        return base.RetrieveInBall(position);
    }
    
    private void SetGoTowardsTargetObject(GameObject foundObject)
    {
        if (SetDestinationToPosition(foundObject.transform.position, checkForPath: true))
        {
            Debug.Log(base.gameObject.name + ": Setting target object and going towards it.");
            targetItem = foundObject.GetComponent<GrabbableObject>();
        }
        else
        {
            targetItem = null;
            Debug.Log(base.gameObject.name + ": i found an object but cannot reach it (or it has been taken by another bug): " + foundObject.name);
        }
    }
    
    private bool GrabTargetItemIfClose()
    {
        if (targetItem != null && heldItem == null && Vector3.Distance(base.transform.position, targetItem.transform.position) < 0.75f)
        {
            NetworkObject component = targetItem.GetComponent<NetworkObject>();
            GrabItem(component);
            sendingGrabOrDropRPC = true;
            GrabItemServerRpc(component);
            return true;
        }
        return false;
    }
    
    private void GrabItem(NetworkObject item)
    {
        if (sendingGrabOrDropRPC)
        {
            sendingGrabOrDropRPC = false;
            return;
        }
        if (heldItem != null)
        {
            Debug.Log(base.gameObject.name + ": Trying to grab another item (" + item.gameObject.name + ") while hands are already full with item (" + heldItem.itemGrabbableObject.gameObject.name + "). Dropping the currently held one.");
            DropItemAndCallDropRPC(heldItem.itemGrabbableObject.GetComponent<NetworkObject>());
        }
        targetItem = null;
        GrabbableObject component = item.gameObject.GetComponent<GrabbableObject>();
        heldItem = new HoarderBugItem(component, HoarderBugItemStatus.Owned, new Vector3());
        component.parentObject = grabTarget;
        component.hasHitGround = false;
        component.GrabItemFromEnemy(this);
        component.EnablePhysics(enable: false);
        
        this.creatureAnimator.SetBool("Chase", true);
        this.creatureSFX.clip = bugFlySFX;
        this.creatureSFX.Play();
        RoundManager.PlayRandomClip(creatureVoice, chitterSFX);
    }
    
    private void DropItem(NetworkObject dropItemNetworkObject, Vector3 targetFloorPosition)
    {
        if (sendingGrabOrDropRPC)
        {
            sendingGrabOrDropRPC = false;
            return;
        }
        if (heldItem == null)
        {
            Debug.LogError("Hoarder bug: my held item is null when attempting to drop it!!");
            return;
        }
        GrabbableObject itemGrabbableObject = heldItem.itemGrabbableObject;
        itemGrabbableObject.parentObject = null;
        itemGrabbableObject.transform.SetParent(StartOfRound.Instance.propsContainer, worldPositionStays: true);
        itemGrabbableObject.EnablePhysics(enable: true);
        itemGrabbableObject.fallTime = 0f;
        itemGrabbableObject.startFallingPosition = itemGrabbableObject.transform.parent.InverseTransformPoint(itemGrabbableObject.transform.position);
        itemGrabbableObject.targetFloorPosition = itemGrabbableObject.transform.parent.InverseTransformPoint(targetFloorPosition);
        itemGrabbableObject.floorYRot = -1;
        itemGrabbableObject.DiscardItemFromEnemy();
        heldItem = null;
        
        this.SetDestinationToPosition(this.transform.position);
        this.creatureAnimator.SetBool("Chase", false);
        this.creatureSFX.Stop();
        RoundManager.PlayRandomClip(creatureVoice, chitterSFX);
    }
    
    private void DropItemAndCallDropRPC(NetworkObject dropItemNetworkObject)
    {
        Vector3 targetFloorPosition = RoundManager.Instance.RandomlyOffsetPosition(heldItem.itemGrabbableObject.GetItemFloorPosition(), 1.2f, 0.4f);
        DropItem(dropItemNetworkObject, targetFloorPosition);
        sendingGrabOrDropRPC = true;
        DropItemServerRpc(dropItemNetworkObject, targetFloorPosition);
    }
    
    internal static void InitializeRPCS()
    {
        NetworkManager.__rpc_func_table.Add(1120862648u, __rpc_handler_1120862648);
        NetworkManager.__rpc_func_table.Add(1650981814u, __rpc_handler_1650981814);
        NetworkManager.__rpc_func_table.Add(2688306109u, __rpc_handler_2688306109);
        NetworkManager.__rpc_func_table.Add(713145915u, __rpc_handler_713145915);
    }
    
    [ServerRpc]
	public void DropItemServerRpc(NetworkObjectReference objectRef, Vector3 targetFloorPosition)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Server && (networkManager.IsClient || networkManager.IsHost))
		{
			if (base.OwnerClientId != networkManager.LocalClientId)
			{
				if (networkManager.LogLevel <= LogLevel.Normal)
				{
					Debug.LogError("Only the owner can invoke a ServerRpc that requires ownership!");
				}
				return;
			}
			ServerRpcParams serverRpcParams = default(ServerRpcParams);
			FastBufferWriter bufferWriter = __beginSendServerRpc(2688306109u, serverRpcParams, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in objectRef);
			bufferWriter.WriteValueSafe(in targetFloorPosition);
			__endSendServerRpc(ref bufferWriter, 2688306109u, serverRpcParams, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Server && (networkManager.IsServer || networkManager.IsHost))
		{
			DropItemClientRpc(objectRef, targetFloorPosition);
		}
	}

	[ClientRpc]
	public void DropItemClientRpc(NetworkObjectReference objectRef, Vector3 targetFloorPosition)
	{
		NetworkManager networkManager = base.NetworkManager;
		if ((object)networkManager == null || !networkManager.IsListening)
		{
			return;
		}
		if (__rpc_exec_stage != __RpcExecStage.Client && (networkManager.IsServer || networkManager.IsHost))
		{
			ClientRpcParams clientRpcParams = default(ClientRpcParams);
			FastBufferWriter bufferWriter = __beginSendClientRpc(713145915u, clientRpcParams, RpcDelivery.Reliable);
			bufferWriter.WriteValueSafe(in objectRef);
			bufferWriter.WriteValueSafe(in targetFloorPosition);
			__endSendClientRpc(ref bufferWriter, 713145915u, clientRpcParams, RpcDelivery.Reliable);
		}
		if (__rpc_exec_stage == __RpcExecStage.Client && (networkManager.IsClient || networkManager.IsHost))
		{
			if (objectRef.TryGet(out var networkObject))
			{
				DropItem(networkObject, targetFloorPosition);
			}
			else
			{
				Debug.LogError(base.gameObject.name + ": Failed to get network object from network object reference (Drop item RPC)");
			}
		}
	}
    
    private static void __rpc_handler_2688306109(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
    {
        NetworkManager networkManager = target.NetworkManager;
        if ((object)networkManager == null || !networkManager.IsListening)
        {
            return;
        }
        if (rpcParams.Server.Receive.SenderClientId != target.OwnerClientId)
        {
            if (networkManager.LogLevel <= LogLevel.Normal)
            {
                Debug.LogError("Only the owner can invoke a ServerRpc that requires ownership!");
            }
            return;
        }
        reader.ReadValueSafe(out NetworkObjectReference value);
        reader.ReadValueSafe(out Vector3 value2);
        Traverse rpcExecStageField = Traverse.Create(target).Field("__rpc_exec_stage");
        rpcExecStageField.SetValue(__RpcExecStage.Server);
        ((HoarderBugCustomAI)target).DropItemServerRpc(value, value2);
        rpcExecStageField.SetValue(__RpcExecStage.None);
    }
    
    private static void __rpc_handler_713145915(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
    {
        NetworkManager networkManager = target.NetworkManager;
        if ((object)networkManager != null && networkManager.IsListening)
        {
            reader.ReadValueSafe(out NetworkObjectReference value);
            reader.ReadValueSafe(out Vector3 value2);
            Traverse rpcExecStageField = Traverse.Create(target).Field("__rpc_exec_stage");
            rpcExecStageField.SetValue(__RpcExecStage.Client);
            ((HoarderBugCustomAI)target).DropItemClientRpc(value, value2);
            rpcExecStageField.SetValue(__RpcExecStage.None);
        }
    }
    
    [ServerRpc]
    public void GrabItemServerRpc(NetworkObjectReference objectRef)
    {
        NetworkManager networkManager = base.NetworkManager;
        if (networkManager == null || !networkManager.IsListening)
        {
            return;
        }
        if (__rpc_exec_stage != __RpcExecStage.Server && (networkManager.IsClient || networkManager.IsHost))
        {
            if (base.OwnerClientId != networkManager.LocalClientId)
            {
                if (networkManager.LogLevel <= LogLevel.Normal)
                {
                    Debug.LogError("Only the owner can invoke a ServerRpc that requires ownership!");
                }
                return;
            }
            ServerRpcParams serverRpcParams = default(ServerRpcParams);
            FastBufferWriter bufferWriter = __beginSendServerRpc(1120862648u, serverRpcParams, RpcDelivery.Reliable);
            bufferWriter.WriteValueSafe(in objectRef, default(FastBufferWriter.ForNetworkSerializable));
            __endSendServerRpc(ref bufferWriter, 1120862648u, serverRpcParams, RpcDelivery.Reliable);
        }
        if (__rpc_exec_stage == __RpcExecStage.Server && (networkManager.IsServer || networkManager.IsHost))
        {
            GrabItemClientRpc(objectRef);
        }
    }
    
    [ClientRpc]
    public void GrabItemClientRpc(NetworkObjectReference objectRef)
    {
        NetworkManager networkManager = base.NetworkManager;
        if (networkManager == null || !networkManager.IsListening)
        {
            return;
        }
        if (__rpc_exec_stage != __RpcExecStage.Client && (networkManager.IsServer || networkManager.IsHost))
        {
            ClientRpcParams clientRpcParams = default(ClientRpcParams);
            FastBufferWriter bufferWriter = __beginSendClientRpc(1650981814u, clientRpcParams, RpcDelivery.Reliable);
            bufferWriter.WriteValueSafe(in objectRef, default(FastBufferWriter.ForNetworkSerializable));
            __endSendClientRpc(ref bufferWriter, 1650981814u, clientRpcParams, RpcDelivery.Reliable);
        }
        if (__rpc_exec_stage == __RpcExecStage.Client && (networkManager.IsClient || networkManager.IsHost))
        {
            // SwitchToBehaviourStateOnLocalClient(1);
            if (objectRef.TryGet(out var networkObject))
            {
                GrabItem(networkObject);
            }
            else
            {
                Debug.LogError(base.gameObject.name + ": Failed to get network object from network object reference (Grab item RPC)");
            }
        }
    }
    
    private static void __rpc_handler_1120862648(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
    {
        NetworkManager networkManager = target.NetworkManager;
        if (networkManager == null || !networkManager.IsListening)
        {
            return;
        }
        if (rpcParams.Server.Receive.SenderClientId != target.OwnerClientId)
        {
            if (networkManager.LogLevel <= LogLevel.Normal)
            {
                Debug.LogError("Only the owner can invoke a ServerRpc that requires ownership!");
            }
        }
        else
        {
            reader.ReadValueSafe(out NetworkObjectReference value);
            Traverse rpcExecStageField = Traverse.Create(target).Field("__rpc_exec_stage");
            rpcExecStageField.SetValue(__RpcExecStage.Server);
            ((HoarderBugCustomAI)target).GrabItemServerRpc(value);
            rpcExecStageField.SetValue(__RpcExecStage.None);
        }
    }
    
    private static void __rpc_handler_1650981814(NetworkBehaviour target, FastBufferReader reader, __RpcParams rpcParams)
    {
        NetworkManager networkManager = target.NetworkManager;
        if (networkManager != null && networkManager.IsListening)
        {
            reader.ReadValueSafe(out NetworkObjectReference value);
            Traverse rpcExecStageField = Traverse.Create(target).Field("__rpc_exec_stage");
            rpcExecStageField.SetValue(__RpcExecStage.Client);
            ((HoarderBugCustomAI)target).GrabItemClientRpc(value);
            rpcExecStageField.SetValue(__RpcExecStage.None);
        }
    }
}