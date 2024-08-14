using System.Collections.Generic;
using GameNetcodeStuff;
using LethalMon.Items;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.Behaviours;

internal class ForestGiantTamedBehaviour : TamedEnemyBehaviour
{
    #region Properties
    internal ForestGiantAI? _forestGiant = null;
    internal ForestGiantAI ForestGiant
    {
        get
        {
            if (_forestGiant == null)
                _forestGiant = (Enemy as ForestGiantAI)!;

            return _forestGiant;
        }
    }
    #endregion

    #region Cooldowns
    private static readonly string ShieldCooldownId = "forestgiant_shield";

    internal override Cooldown[] Cooldowns => new[] { new Cooldown(ShieldCooldownId, "Shield", ModConfig.Instance.values.ForestGiantShieldCooldown) };

    private CooldownNetworkBehaviour shieldCooldown;
    #endregion
    
    #region Action Keys
    private List<ActionKey> _actionKeys = new()
    {
        new ActionKey { actionKey = ModConfig.Instance.ActionKey1, description = "Deploy shield" }
    };
    internal override List<ActionKey> ActionKeys => _actionKeys;

    internal override void ActionKey1Pressed()
    {
        base.ActionKey1Pressed();
        
        DeployShieldServerRpc();
    }
    #endregion

    #region Base Methods
    internal override void Start()
    {
        base.Start();

        shieldCooldown = GetCooldownWithId(ShieldCooldownId);

        if (ownerPlayer != null)
        {
            ForestGiant.transform.localScale *= 0.25f;
            ForestGiant.creatureAnimator.Play("Base Layer.Walking Blend Tree");
        }
    }

    internal override void InitTamingBehaviour(TamingBehaviour behaviour)
    {
        // ANY CLIENT
        base.InitTamingBehaviour(behaviour);

        switch(behaviour)
        {
            case TamingBehaviour.TamedFollowing:
                if (Utils.CurrentPlayer == ownerPlayer)
                    EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true);
                break;

            case TamingBehaviour.TamedDefending:
                if (Utils.CurrentPlayer == ownerPlayer)
                    EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, false);
                
                DeployShield();
                break;

            default: break;
        }
    }

    internal override void OnTamedFollowing()
    {
        // OWNER ONLY
        base.OnTamedFollowing();
    }

    internal override void OnTamedDefending()
    {
        // OWNER ONLY
        base.OnTamedDefending();
    }

    internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
    {
        // ANY CLIENT
        // todo escape behaviour
        base.OnEscapedFromBall(playerWhoThrewBall);
    }

    internal override void OnUpdate(bool update = false, bool doAIInterval = true)
    {
        // ANY CLIENT
        base.OnUpdate(update, doAIInterval);
    }

    internal override void DoAIInterval()
    {
        // ANY CLIENT, every EnemyAI.updateDestinationInterval, if OnUpdate.doAIInterval = true
        base.DoAIInterval();
    }

    public override PokeballItem? RetrieveInBall(Vector3 position)
    {
        // ANY CLIENT
        return base.RetrieveInBall(position);
    }

    public override bool CanBeTeleported()
    {
        // HOST ONLY
        return base.CanBeTeleported();
    }
    #endregion
    
    #region Methods

    public void DeployShield()
    {
        GameObject forceField = Instantiate(LethalMon.forceFieldPrefab, ForestGiant.transform.position, Quaternion.identity);
        forceField.transform.parent = ForestGiant.transform;
        forceField.transform.localScale *= 0.1f;
    }
    #endregion

    #region RPCs
    [ServerRpc(RequireOwnership = false)]
    public void DeployShieldServerRpc()
    {
        // HOST ONLY
        DeployShieldClientRpc();
    }

    [ClientRpc]
    public void DeployShieldClientRpc()
    {
        // ANY CLIENT (HOST INCLUDED)
        DeployShield();
    }
    #endregion
}