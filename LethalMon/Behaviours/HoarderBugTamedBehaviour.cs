using LethalMon.Items;
using System.Collections.Generic;
using System;
using Unity.Netcode;
using UnityEngine;
using GameNetcodeStuff;

namespace LethalMon.Behaviours;

public class HoarderBugTamedBehaviour : TamedEnemyBehaviour
{
    #region Properties
    internal HoarderBugAI hoarderBug { get; private set; }

    public float currentTimer = 0f;

    public const float searchTimer = 1f; // in seconds

    internal override bool CanDefend => false;

    #endregion

    #region Custom behaviours
    private enum CustomBehaviour
    {
        GettingItem = 1,
        BringBackItem
    }
    internal override List<Tuple<string, string, Action>>? CustomBehaviourHandler => new()
    {
        new (CustomBehaviour.GettingItem.ToString(), "Saw an interesting item!", OnGettingItem),
        new (CustomBehaviour.BringBackItem.ToString(), "Brings an item to you!", OnBringBackItem)
    };

    public void OnGettingItem()
    {
        if(hoarderBug.heldItem != null)
        {
            SwitchToCustomBehaviour((int)CustomBehaviour.BringBackItem);
            return;
        }

        if (GrabTargetItemIfClose())
        {
            LethalMon.Log("HoarderBugAI grabbed close item");
            return;
        }

        if (hoarderBug.targetItem != null)
        {
            LethalMon.Log("HoarderBugAI found an object and move towards it");
            hoarderBug.SetGoTowardsTargetObject(hoarderBug.targetItem.gameObject);
        }
        else
        {
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
        }
    }

    public void OnBringBackItem()
    {
        if (hoarderBug.heldItem == null || ownerPlayer == null)
        {
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
            return;
        }

        if (Vector3.Distance(hoarderBug.transform.position, ownerPlayer.transform.position) < 6f)
        {
            LethalMon.Log("HoarderBugAI drops held item to the owner");
            DropItemAndCallDropRPC(hoarderBug.heldItem.itemGrabbableObject.GetComponent<NetworkObject>());
        }
        else
        {
            LethalMon.Log("HoarderBugAI move held item to the owner");
            hoarderBug.SetDestinationToPosition(ownerPlayer.transform.position);
        }
    }
    #endregion
    
    #region Cooldowns

    private static readonly string BringItemCooldownId = "hoarderbug_bringitem";
    
    internal override Cooldown[] Cooldowns => new[] { new Cooldown(BringItemCooldownId, "Bring item", ModConfig.Instance.values.HoardingBugBringItemCooldown) };

    private CooldownNetworkBehaviour bringItemCooldown;

    #endregion

    #region Base Methods
    internal override void Start()
    {
        base.Start();

        hoarderBug = (Enemy as HoarderBugAI)!;
        if (hoarderBug == null)
            hoarderBug = gameObject.AddComponent<HoarderBugAI>();

        bringItemCooldown = GetCooldownWithId(BringItemCooldownId);
        
        if (ownerPlayer != null)
        {
            hoarderBug.creatureAnimator.Play("Base Layer.Walking");
        }
        
    }

    internal override void OnTamedFollowing()
    {
        base.OnTamedFollowing();

        if (ownerPlayer == null || !bringItemCooldown.IsFinished()) return;
        
        currentTimer += Time.deltaTime;
        if (currentTimer > searchTimer)
        {
            //LethalMon.Log("HoarderBugAI searches for items");
            currentTimer = 0f;
            var colliders = Physics.OverlapSphere(hoarderBug.transform.position, 15f);

            foreach (Collider collider in colliders)
            {
                GrabbableObject grabbable = collider.GetComponentInParent<GrabbableObject>();
                if (grabbable == null || grabbable.isInShipRoom || grabbable.isHeld || Vector3.Distance(grabbable.transform.position, ownerPlayer.transform.position) < 8f) continue;

                // Check LOS
                if (!Physics.Linecast(hoarderBug.transform.position, grabbable.transform.position, out var _, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
                {
                    LethalMon.Log("HoarderBugAI found item: " + grabbable.name);
                    hoarderBug.targetItem = grabbable;
                    SwitchToCustomBehaviour((int)CustomBehaviour.GettingItem);
                    return;
                }
            }
        }
    }

    internal override void OnTamedDefending()
    {
        base.OnTamedDefending();
    }

    internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
    {
        base.OnEscapedFromBall(playerWhoThrewBall);

        if (Utils.IsHost)
        {
            hoarderBug.angryTimer = 10f;
            hoarderBug.angryAtPlayer = playerWhoThrewBall;
        }
    }

    internal override void DoAIInterval()
    {
        base.DoAIInterval();
    }

    internal override void OnUpdate(bool update = false, bool doAIInterval = true)
    {
        base.OnUpdate(update, doAIInterval);

        hoarderBug.CalculateAnimationDirection();
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
    #endregion

    #region Methods
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
            LethalMon.Log(base.gameObject.name + ": Trying to grab another item (" + item.gameObject.name + ") while hands are already full with item (" + hoarderBug.heldItem.itemGrabbableObject.gameObject.name + "). Dropping the currently held one.");
            DropItemAndCallDropRPC(hoarderBug.heldItem.itemGrabbableObject.GetComponent<NetworkObject>());
        }
        hoarderBug.targetItem = null;
        GrabbableObject component = item.gameObject.GetComponent<GrabbableObject>();
        hoarderBug.heldItem = new HoarderBugItem(component, HoarderBugItemStatus.Owned, new Vector3());
        component.parentObject = hoarderBug.grabTarget;
        component.hasHitGround = false;
        component.GrabItemFromEnemy(Enemy);
        component.EnablePhysics(enable: false);

        // todo: maybe original one is re-useable till here
        
        hoarderBug.creatureAnimator.SetBool("Chase", true);
        hoarderBug.creatureSFX.clip = LethalMon.HoardingBugFlySfx;
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
            LethalMon.Log("Hoarder bug: my held item is null when attempting to drop it!!", LethalMon.LogType.Error);
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

        // todo: maybe original one is re-useable till here

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
    #endregion

    #region RPCs
    [ServerRpc(RequireOwnership = false)]
	public void DropItemServerRpc(NetworkObjectReference objectRef, Vector3 targetFloorPosition)
    {
        bringItemCooldown.Resume();
		DropItemClientRpc(objectRef, targetFloorPosition);
        SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
	}

	[ClientRpc]
	public void DropItemClientRpc(NetworkObjectReference objectRef, Vector3 targetFloorPosition)
	{
		if (!objectRef.TryGet(out var networkObject))
        {
            LethalMon.Log(base.gameObject.name + ": Failed to get network object from network object reference (Drop item RPC)", LethalMon.LogType.Error);
            return;
		}

        DropItem(networkObject, targetFloorPosition);
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void GrabItemServerRpc(NetworkObjectReference objectRef)
    {
        bringItemCooldown.Reset();
        bringItemCooldown.Pause();
        GrabItemClientRpc(objectRef);
        SwitchToCustomBehaviour((int)CustomBehaviour.BringBackItem);
    }
    
    [ClientRpc]
    public void GrabItemClientRpc(NetworkObjectReference objectRef)
    {
        // SwitchToBehaviourStateOnLocalClient(1);
        if (!objectRef.TryGet(out var networkObject))
        {
            LethalMon.Log(base.gameObject.name + ": Failed to get network object from network object reference (Grab item RPC)", LethalMon.LogType.Error);
            return;
        }

        GrabItem(networkObject);
    }
    #endregion
}