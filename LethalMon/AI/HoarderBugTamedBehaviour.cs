using HarmonyLib;
using LethalMon.Items;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.AI;

public class HoarderBugTamedBehaviour : TamedEnemyBehaviour
{
    internal HoarderBugAI hoarderBug { get; private set; }

    public int currentTimer = 0;

    public const int searchTimer = 10;

    public override void Start()
    {
        base.Start();

        hoarderBug = enemy as HoarderBugAI;
        if (hoarderBug == null)
            hoarderBug = gameObject.AddComponent<HoarderBugAI>();

        hoarderBug.creatureAnimator.Play("Base Layer.Walking");
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();

        // Grab an item if one is close
        if (!GrabTargetItemIfClose())
        {
            // We currently have an item to give to the owner
            if (hoarderBug.heldItem != null && ownerPlayer != null)
            {
                if (Vector3.Distance(hoarderBug.transform.position, ownerPlayer.transform.position) < 6f)
                {
                    Debug.Log("HoarderBugAI drops held item to the owner");
                    DropItemAndCallDropRPC(hoarderBug.heldItem.itemGrabbableObject.GetComponent<NetworkObject>());
                }
                else
                {
                    Debug.Log("HoarderBugAI move held item to the owner");
                    hoarderBug.SetMovingTowardsTargetPlayer(ownerPlayer);   
                }
            }
            else
            {
                if (hoarderBug?.targetItem != null)
                {
                    Debug.Log("HoarderBugAI found an object and move towards it");
                    SetGoTowardsTargetObject(hoarderBug.targetItem.gameObject);
                }
                else if(ownerPlayer != null)
                {
                    // Do not search too often
                    currentTimer++;
                    if (currentTimer >= HoarderBugTamedBehaviour.searchTimer)
                    {
                        Debug.Log("HoarderBugAI searches for items");
                        currentTimer = 0;
                        Collider[] colliders = Physics.OverlapSphere(hoarderBug.transform.position, 15f);
                        Debug.Log("HoarderBugAI found " + colliders.Length + " colliders");
                        
                        foreach (Collider collider in colliders)
                        {
                            GrabbableObject grabbable = collider.GetComponentInParent<GrabbableObject>();
                            if (grabbable != null)
                            {
                                Debug.Log("HoarderBugAI grabbable item (isInShipRoom: " + grabbable.isInShipRoom + ", isHeld: " + grabbable.isHeld + "): " + grabbable.name);
                                if (grabbable is { isInShipRoom: false, isHeld: false } && Vector3.Distance(grabbable.transform.position, ownerPlayer.transform.position) > 8f)
                                {
                                    // Check LOS
                                    if (!Physics.Linecast(hoarderBug.transform.position, grabbable.transform.position, out var _, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
                                    {
                                        Debug.Log("HoarderBugAI found item: " + grabbable.name);
                                        hoarderBug.targetItem = grabbable;
                                        return;   
                                    }
                                }
                            }
                        }
                    }

                    Debug.Log("HoarderBugAI follows the owner");
                    FollowOwner();
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
        hoarderBug.agentLocalVelocity = hoarderBug.animationContainer.InverseTransformDirection(Vector3.ClampMagnitude(hoarderBug.transform.position - previousPosition, 1f) / (Time.deltaTime * 2f));
        hoarderBug.velX = Mathf.Lerp(hoarderBug.velX, hoarderBug.agentLocalVelocity.x, 10f * Time.deltaTime);
        hoarderBug.creatureAnimator.SetFloat("VelocityX", Mathf.Clamp(hoarderBug.velX, 0f - maxSpeed, maxSpeed));
        hoarderBug.velZ = Mathf.Lerp(hoarderBug.velZ, 0f - hoarderBug.agentLocalVelocity.y, 10f * Time.deltaTime);
        hoarderBug.creatureAnimator.SetFloat("VelocityZ", Mathf.Clamp(hoarderBug.velZ, 0f - maxSpeed, maxSpeed));
        hoarderBug.creatureAnimator.SetFloat("speedMultiplier", Vector3.ClampMagnitude(hoarderBug.transform.position - previousPosition, 1f).sqrMagnitude / (Time.deltaTime / 4f));
        previousPosition = hoarderBug.transform.position;
    }

    public override PokeballItem RetrieveInBall(Vector3 position)
    {
        // Drop held item if any
        if (hoarderBug.heldItem != null)
        {
            DropItemAndCallDropRPC(hoarderBug.heldItem.itemGrabbableObject.GetComponent<NetworkObject>());
        }
        
        return base.RetrieveInBall(position);
    }
    
    private void SetGoTowardsTargetObject(GameObject foundObject)
    {
        if (hoarderBug.SetDestinationToPosition(foundObject.transform.position, checkForPath: true))
        {
            Debug.Log(base.gameObject.name + ": Setting target object and going towards it.");
            hoarderBug.targetItem = foundObject.GetComponent<GrabbableObject>();
        }
        else
        {
            hoarderBug.targetItem = null;
            Debug.Log(base.gameObject.name + ": i found an object but cannot reach it (or it has been taken by another bug): " + foundObject.name);
        }
    }
    
    private bool GrabTargetItemIfClose()
    {
        if (hoarderBug.targetItem != null && hoarderBug.heldItem == null && Vector3.Distance(hoarderBug.transform.position, hoarderBug.targetItem.transform.position) < 0.75f)
        {
            NetworkObject component = hoarderBug.targetItem.GetComponent<NetworkObject>();
            GrabItem(component);
            hoarderBug.sendingGrabOrDropRPC = true;
            GrabItemServerRpc(component);
            return true;
        }
        return false;
    }
    
    private void GrabItem(NetworkObject item)
    {
        if (hoarderBug.sendingGrabOrDropRPC)
        {
            hoarderBug.sendingGrabOrDropRPC = false;
            return;
        }
        if (hoarderBug.heldItem != null)
        {
            Debug.Log(base.gameObject.name + ": Trying to grab another item (" + item.gameObject.name + ") while hands are already full with item (" + hoarderBug.heldItem.itemGrabbableObject.gameObject.name + "). Dropping the currently held one.");
            DropItemAndCallDropRPC(hoarderBug.heldItem.itemGrabbableObject.GetComponent<NetworkObject>());
        }
        hoarderBug.targetItem = null;
        GrabbableObject component = item.gameObject.GetComponent<GrabbableObject>();
        hoarderBug.heldItem = new HoarderBugItem(component, HoarderBugItemStatus.Owned, new Vector3());
        component.parentObject = hoarderBug.grabTarget;
        component.hasHitGround = false;
        component.GrabItemFromEnemy(enemy);
        component.EnablePhysics(enable: false);
        
        hoarderBug.creatureAnimator.SetBool("Chase", true);
        hoarderBug.creatureSFX.clip = hoarderBug.bugFlySFX;
        hoarderBug.creatureSFX.Play();
        RoundManager.PlayRandomClip(hoarderBug.creatureVoice, hoarderBug.chitterSFX);
    }
    
    private void DropItem(NetworkObject dropItemNetworkObject, Vector3 targetFloorPosition)
    {
        if (hoarderBug.sendingGrabOrDropRPC)
        {
            hoarderBug.sendingGrabOrDropRPC = false;
            return;
        }
        if (hoarderBug.heldItem == null)
        {
            Debug.LogError("Hoarder bug: my held item is null when attempting to drop it!!");
            return;
        }
        GrabbableObject itemGrabbableObject = hoarderBug.heldItem.itemGrabbableObject;
        itemGrabbableObject.parentObject = null;
        itemGrabbableObject.transform.SetParent(StartOfRound.Instance.propsContainer, worldPositionStays: true);
        itemGrabbableObject.EnablePhysics(enable: true);
        itemGrabbableObject.fallTime = 0f;
        itemGrabbableObject.startFallingPosition = itemGrabbableObject.transform.parent.InverseTransformPoint(itemGrabbableObject.transform.position);
        itemGrabbableObject.targetFloorPosition = itemGrabbableObject.transform.parent.InverseTransformPoint(targetFloorPosition);
        itemGrabbableObject.floorYRot = -1;
        itemGrabbableObject.DiscardItemFromEnemy();
        hoarderBug.heldItem = null;
        
        hoarderBug.SetDestinationToPosition(hoarderBug.transform.position);
        hoarderBug.creatureAnimator.SetBool("Chase", false);
        hoarderBug.creatureSFX.Stop();
        RoundManager.PlayRandomClip(hoarderBug.creatureVoice, hoarderBug.chitterSFX);
    }
    
    private void DropItemAndCallDropRPC(NetworkObject dropItemNetworkObject)
    {
        Vector3 targetFloorPosition = RoundManager.Instance.RandomlyOffsetPosition(hoarderBug.heldItem.itemGrabbableObject.GetItemFloorPosition(), 1.2f, 0.4f);
        DropItem(dropItemNetworkObject, targetFloorPosition);
        hoarderBug.sendingGrabOrDropRPC = true;
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
        ((HoarderBugTamedBehaviour)target).DropItemServerRpc(value, value2);
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
            ((HoarderBugTamedBehaviour)target).DropItemClientRpc(value, value2);
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
            ((HoarderBugTamedBehaviour)target).GrabItemServerRpc(value);
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
            ((HoarderBugTamedBehaviour)target).GrabItemClientRpc(value);
            rpcExecStageField.SetValue(__RpcExecStage.None);
        }
    }
}