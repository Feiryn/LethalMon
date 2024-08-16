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
    private HoarderBugAI? _HoarderBug = null;
    internal HoarderBugAI HoarderBug
    {
        get
        {
            if (_HoarderBug == null)
                _HoarderBug = (Enemy as HoarderBugAI)!;

            return _HoarderBug;
        }
    }

    private float _currentTimer = 0f;

    internal const float SearchTimer = 1f; // in seconds

    internal override bool CanDefend => false;

    internal static AudioClip? FlySFX = null;

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
        if(HoarderBug.heldItem != null)
        {
            SwitchToCustomBehaviour((int)CustomBehaviour.BringBackItem);
            return;
        }

        if (GrabTargetItemIfClose())
        {
            LethalMon.Log("HoarderBugAI grabbed close item");
            return;
        }

        if (HoarderBug.targetItem != null)
        {
            LethalMon.Log("HoarderBugAI found an object and move towards it");
            HoarderBug.SetGoTowardsTargetObject(HoarderBug.targetItem.gameObject);
        }
        else
        {
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
        }
    }

    public void OnBringBackItem()
    {
        if (HoarderBug.heldItem == null || ownerPlayer == null)
        {
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
            return;
        }

        if (DistanceToOwner < 6f)
        {
            LethalMon.Log("HoarderBugAI drops held item to the owner");
            DropHeldItemAndCallDropRPC();
        }
        else
        {
            LethalMon.Log("HoarderBugAI move held item to the owner");
            HoarderBug.SetDestinationToPosition(ownerPlayer.transform.position);
        }
    }
    #endregion
    
    #region Cooldowns

    private static readonly string BringItemCooldownId = "hoarderbug_bringitem";
    
    internal override Cooldown[] Cooldowns => [new Cooldown(BringItemCooldownId, "Bring item", ModConfig.Instance.values.HoardingBugBringItemCooldown)];

    private readonly CooldownNetworkBehaviour bringItemCooldown;

    #endregion

    #region Base Methods
    HoarderBugTamedBehaviour()
    {
        bringItemCooldown = GetCooldownWithId(BringItemCooldownId);
    }

    internal override void Start()
    {
        base.Start();
        
        if (IsTamed)
            HoarderBug.creatureAnimator.Play("Base Layer.Walking");
        
    }

    internal override void OnTamedFollowing()
    {
        base.OnTamedFollowing();

        if (!IsTamed || !bringItemCooldown.IsFinished()) return;
        
        _currentTimer += Time.deltaTime;
        if (_currentTimer > SearchTimer)
        {
            //LethalMon.Log("HoarderBugAI searches for items");
            _currentTimer = 0f;
            var colliders = Physics.OverlapSphere(HoarderBug.transform.position, 15f);

            foreach (Collider collider in colliders)
            {
                GrabbableObject grabbable = collider.GetComponentInParent<GrabbableObject>();
                if (grabbable == null || grabbable.isInShipRoom || grabbable.isHeld || Vector3.Distance(grabbable.transform.position, ownerPlayer!.transform.position) < 8f) continue;

                // Check LOS
                if (!Physics.Linecast(HoarderBug.transform.position, grabbable.transform.position, out var _, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
                {
                    LethalMon.Log("HoarderBugAI found item: " + grabbable.name);
                    HoarderBug.targetItem = grabbable;
                    SwitchToCustomBehaviour((int)CustomBehaviour.GettingItem);
                    return;
                }
            }
        }
    }

    internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
    {
        base.OnEscapedFromBall(playerWhoThrewBall);

        if (Utils.IsHost)
        {
            HoarderBug.angryTimer = 10f;
            HoarderBug.angryAtPlayer = playerWhoThrewBall;
        }
    }

    internal override void OnUpdate(bool update = false, bool doAIInterval = true)
    {
        base.OnUpdate(update, doAIInterval);

        HoarderBug.CalculateAnimationDirection();
    }

    public override PokeballItem? RetrieveInBall(Vector3 position)
    {
        // Drop held item if any
        if (HoarderBug.heldItem?.itemGrabbableObject != null)
            DropHeldItemAndCallDropRPC();
        
        return base.RetrieveInBall(position);
    }
    #endregion

    #region Methods
    public static void LoadAudio(AssetBundle assetBundle)
    {
        FlySFX = assetBundle.LoadAsset<AudioClip>("Assets/Audio/HoardingBug/Fly.ogg");
    }

    private bool GrabTargetItemIfClose()
    {
        if (HoarderBug.targetItem == null || HoarderBug.heldItem != null || Vector3.Distance(HoarderBug.transform.position, HoarderBug.targetItem.transform.position) > 0.75f)
            return false;

        if (!HoarderBug.targetItem.TryGetComponent(out NetworkObject networkObject))
            return false;

        GrabItem(networkObject);
        HoarderBug.sendingGrabOrDropRPC = true;
        GrabItemServerRpc(networkObject);
        return true;
    }
    
    private void GrabItem(NetworkObject item)
    {
        if (HoarderBug.sendingGrabOrDropRPC)
        {
            HoarderBug.sendingGrabOrDropRPC = false;
            return;
        }

        if (HoarderBug.heldItem != null)
        {
            LethalMon.Log(base.gameObject.name + ": Trying to grab another item (" + item.gameObject.name + ") while hands are already full with item (" + HoarderBug.heldItem.itemGrabbableObject.gameObject.name + "). Dropping the currently held one.");
            DropHeldItemAndCallDropRPC();
        }

        HoarderBug.targetItem = null;
        if (item.gameObject.TryGetComponent(out GrabbableObject grabbableObject))
        {
            HoarderBug.heldItem = new HoarderBugItem(grabbableObject, HoarderBugItemStatus.Owned, new Vector3());
            grabbableObject.parentObject = HoarderBug.grabTarget;
            grabbableObject.hasHitGround = false;
            grabbableObject.GrabItemFromEnemy(Enemy);
            grabbableObject.EnablePhysics(enable: false);
        }

        // todo: maybe original one is re-useable till here
        
        HoarderBug.creatureAnimator.SetBool("Chase", true);
        HoarderBug.creatureSFX.clip = FlySFX;
        HoarderBug.creatureSFX.Play();
        RoundManager.PlayRandomClip(HoarderBug.creatureVoice, HoarderBug.chitterSFX);
    }
    
    private void DropHeldItem(Vector3 targetFloorPosition)
    {
        if (HoarderBug.sendingGrabOrDropRPC)
        {
            HoarderBug.sendingGrabOrDropRPC = false;
            return;
        }

        if (HoarderBug.heldItem?.itemGrabbableObject == null)
        {
            LethalMon.Log("Hoarder bug: my held item is null when attempting to drop it!!", LethalMon.LogType.Error);
            return;
        }

        var itemGrabbableObject = HoarderBug.heldItem.itemGrabbableObject;
        itemGrabbableObject.parentObject = null;
        itemGrabbableObject.transform.SetParent(StartOfRound.Instance.propsContainer, worldPositionStays: true);
        itemGrabbableObject.EnablePhysics(enable: true);
        itemGrabbableObject.fallTime = 0f;
        itemGrabbableObject.startFallingPosition = itemGrabbableObject.transform.parent.InverseTransformPoint(itemGrabbableObject.transform.position);
        itemGrabbableObject.targetFloorPosition = itemGrabbableObject.transform.parent.InverseTransformPoint(targetFloorPosition);
        itemGrabbableObject.floorYRot = -1;
        itemGrabbableObject.DiscardItemFromEnemy();
        HoarderBug.heldItem = null;

        // todo: maybe original one is re-useable till here

        HoarderBug.SetDestinationToPosition(HoarderBug.transform.position);
        HoarderBug.creatureAnimator.SetBool("Chase", false);
        HoarderBug.creatureSFX.Stop();
        RoundManager.PlayRandomClip(HoarderBug.creatureVoice, HoarderBug.chitterSFX);
    }
    
    private void DropHeldItemAndCallDropRPC()
    {
        Vector3 targetFloorPosition = RoundManager.Instance.RandomlyOffsetPosition(HoarderBug.heldItem.itemGrabbableObject.GetItemFloorPosition(), 1.2f, 0.4f);
        DropHeldItem(targetFloorPosition);
        HoarderBug.sendingGrabOrDropRPC = true;
        DropHeldItemServerRpc( targetFloorPosition);
    }
    #endregion

    #region RPCs
    [ServerRpc(RequireOwnership = false)]
	public void DropHeldItemServerRpc(Vector3 targetFloorPosition)
    {
        bringItemCooldown.Resume();
		DropHeldItemClientRpc(targetFloorPosition);
        SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
	}

	[ClientRpc]
	public void DropHeldItemClientRpc(Vector3 targetFloorPosition)
	{
        DropHeldItem(targetFloorPosition);
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