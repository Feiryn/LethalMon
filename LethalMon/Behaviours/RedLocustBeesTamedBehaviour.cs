using System;
using GameNetcodeStuff;
using LethalMon.Patches;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.Behaviours;

internal class RedLocustBeesTamedBehaviour : TamedEnemyBehaviour
{
    #region Properties
    private RedLocustBees? _bees;
    internal RedLocustBees Bees
    {
        get
        {
            if (_bees == null)
                _bees = (Enemy as RedLocustBees)!;

            return _bees;
        }
    }

    public override float TargetingRange => 10f;
    #endregion

    #region Cooldowns

    private const string StunCooldownId = "Bees_stun";

    public override Cooldown[] Cooldowns => [new Cooldown(StunCooldownId, "Stun enemy", ModConfig.Instance.values.BeesStunCooldown)];

    private CooldownNetworkBehaviour? _stunCooldown;

    public override bool CanDefend => _stunCooldown != null && _stunCooldown.IsFinished();
    #endregion

    #region Base Methods
    public override void OnUpdate(bool update = false, bool doAIInterval = true)
    {
        base.OnUpdate(update, false);
    }

    public override void Start()
    {
        base.Start();

        if (!IsTamed) return;
        
        _stunCooldown = GetCooldownWithId(StunCooldownId);
        
        if(Bees.agent != null)
            Bees.agent.speed = 10.3f;

        Bees.gameObject.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f); // Make them stick tighter together
    }

    public override void OnTamedFollowing()
    {
        base.OnTamedFollowing();

        if (_stunCooldown != null && _stunCooldown.IsFinished())
            TargetNearestEnemy(true, false, 360f);
    }

    public override void OnTamedDefending()
    {
        BeesZapOnTimer();
        
        if (targetPlayer != null)
        {
            float distance = DistanceToTargetPlayer;
            LethalMon.Log("Distance to player: " + distance);
            if (targetPlayer.isPlayerDead || !targetPlayer.isPlayerControlled || distance > 25f)
            {
                LethalMon.Log("Stop targeting player");
                targetPlayer = null;
            }
            else if (distance < 2.5f)
            {
                LethalMon.Log("Target player collided");

                _stunCooldown?.Reset();
                BeeDamageServerRPC(targetPlayer.GetComponent<NetworkObject>());
                
                targetPlayer = null;
            }
            else
            {
                LethalMon.Log("Follow player");
                Bees.SetDestinationToPosition(targetPlayer.transform.position);
            }
        }
        else if (HasTargetEnemy)
        {
            float distance = DistanceToTargetEnemy;
            LethalMon.Log("Distance to enemy: " + distance);
            if (targetEnemy!.isEnemyDead || distance > 25f)
            {
                LethalMon.Log("Stop targeting enemy");
                targetEnemy = null;
            }
            else if (distance < 2.5f)
            {
                LethalMon.Log("Target enemy collided");

                _stunCooldown?.Reset();
                targetEnemy.SetEnemyStunned(true, 7f);
                
                targetEnemy = null;
            }
            else
            {
                LethalMon.Log("Follow enemy");
                Bees.SetDestinationToPosition(targetEnemy.transform.position);
            }
        }
        else
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
    }

    public override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
    {
        base.OnEscapedFromBall(playerWhoThrewBall);

        if (Utils.IsHost)
        {
            Bees.SetMovingTowardsTargetPlayer(playerWhoThrewBall);
            Bees.SwitchToBehaviourState(2);
            RedLocustBeesPatch.AngryUntil.Add(Bees.GetInstanceID(),
                DateTime.Now.AddSeconds(10)); // todo: solve locally here instead of patch
        }
    }

    public override void InitTamingBehaviour(TamingBehaviour behaviour)
    {
        base.InitTamingBehaviour(behaviour);

        Bees.beesIdle.volume = 0.2f;
        Bees.beesDefensive.volume = 0.2f;
        Bees.beesAngry.Stop();
        
        ResetBeeZapTimer();
        
        if (behaviour == TamingBehaviour.TamedFollowing)
        {
            targetEnemy = null;
            targetPlayer = null;
        }
    }
    #endregion

    #region Methods
    private void BeesZapOnTimer()
    {
        if (Bees.beeZapRandom == null)
            Bees.beeZapRandom = new System.Random();

        if (Bees.beesZapCurrentTimer > Bees.beesZapTimer)
        {
            Bees.beesZapCurrentTimer = 0f;
            Bees.beesZapTimer = Bees.beeZapRandom.Next(1, 7) * 0.06f;
            BeesZap();
        }
        else
        {
            Bees.beesZapCurrentTimer += Time.deltaTime;
        }
    }
    
    public void BeesZap()
    {
        if (Bees.beeParticles.GetBool("Alive"))
        {
            if (Bees.beeZapRandom == null)
                Bees.beeZapRandom = new System.Random();

            for (int i = 0; i < Bees.lightningPoints.Length; i++)
            {
                Bees.lightningPoints[i].transform.position = RoundManager.Instance.GetRandomPositionInBoxPredictable(Bees.beeParticlesTarget.transform.position, 4f, Bees.beeZapRandom);
            }
            Bees.lightningComponent.Trigger(0.1f);
        }

        Bees.beeZapAudio.pitch = UnityEngine.Random.Range(0.8f, 1.1f);
        Bees.beeZapAudio.PlayOneShot(Bees.enemyType.audioClips[UnityEngine.Random.Range(0, Bees.enemyType.audioClips.Length)], UnityEngine.Random.Range(0.6f, 1f));
    }
    
    private void ResetBeeZapTimer()
    {
        Bees.beesZapCurrentTimer = 0f;
        Bees.beeZapAudio.Stop();
    }
    #endregion

    #region RPCs
    [ServerRpc(RequireOwnership = false)]
    public void BeeDamageServerRPC(NetworkObjectReference networkObjectReference) // todo: check if targetPlayer is synced. if so, remove parameter
    {
        BeeDamageClientRpc(networkObjectReference);
    }
    
    [ClientRpc]
    public void BeeDamageClientRpc(NetworkObjectReference networkObjectReference)
    {
        LethalMon.Log("BeeDamage client rpc received");
        if (!networkObjectReference.TryGet(out NetworkObject networkObject))
        {
            LethalMon.Log(Bees.gameObject.name + ": Failed to get network object from network object reference (BeeDamageClientRpc RPC)", LethalMon.LogType.Error);
            return;
        }

        if(!networkObject.TryGetComponent( out PlayerControllerB player))
        {
            LethalMon.Log(Bees.gameObject.name + ": Failed to get player object (BeeDamageClientRpc RPC)", LethalMon.LogType.Error  );
            return;
        }

        player.DamagePlayer(30, hasDamageSFX: true, callRPC: true, CauseOfDeath.Electrocution, 3);
    }
    #endregion
}