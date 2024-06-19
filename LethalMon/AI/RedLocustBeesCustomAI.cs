using System.Linq;
using System.Reflection;
using DigitalRuby.ThunderAndLightning;
using GameNetcodeStuff;
using LethalMon.Items;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.VFX;

namespace LethalMon.AI;

public class RedLocustBeesCustomAI : CustomAI
{
    public bool angry = false;
    
    public EnemyAI? targetEnemyAI = null;
    
    public VisualEffect beeParticles;
    
    public Transform[] lightningPoints;
    
    public LightningBoltPathScript lightningComponent;
    
    public Transform beeParticlesTarget;
    
    public AudioSource beeZapAudio;
    
    private System.Random beeZapRandom = new();
    
    private float beesZapCurrentTimer;

    private float beesZapTimer;
    
    public override void CopyProperties(EnemyAI enemyAI)
    {
        base.CopyProperties(enemyAI);
        
        this.beeParticles = ((RedLocustBees) enemyAI).beeParticles;
        this.lightningPoints = ((RedLocustBees) enemyAI).lightningPoints;
        this.lightningComponent = ((RedLocustBees) enemyAI).lightningComponent;
        this.beeParticlesTarget = ((RedLocustBees) enemyAI).beeParticlesTarget;
        this.beeZapAudio = ((RedLocustBees) enemyAI).beeZapAudio;
        this.beesZapCurrentTimer = ((RedLocustBees) enemyAI).beesZapCurrentTimer;
        this.beesZapTimer = ((RedLocustBees) enemyAI).beesZapTimer;
        ((RedLocustBees) enemyAI).beesIdle.volume = 0.2f;
        ((RedLocustBees) enemyAI).beesDefensive.volume = 0.2f;
        ((RedLocustBees) enemyAI).beesAngry.Stop();
        this.beeZapAudio.Stop();
        this.gameObject.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f); // Make it smaller

    }

    public override void Update()
    {
        base.Update();

        BeesZapOnTimer();
    }

    public override void Start()
    {
        base.Start();
        
        this.agent.speed = 10.3f;
    }

    public override void DoAIInterval()
    {
        base.DoAIInterval();

        if (this.targetPlayer != null)
        {
            float distance = Vector3.Distance(this.targetPlayer.transform.position, this.transform.position);
            Debug.Log("Distance to player: " + distance);
            if (this.targetPlayer.isPlayerDead || !this.targetPlayer.isPlayerControlled || distance > 25f)
            {
                Debug.Log("Stop targeting player");
                this.targetPlayer = null;
            }
            else if (distance < 2.5f)
            {
                Debug.Log("Target player collided");

                BeeDamagePacket(this.targetPlayer.GetComponent<NetworkObject>());
            
                ChangeAngryMode(false);
                this.targetPlayer = null;
            }
            else
            {
                Debug.Log("Follow player");
                this.SetDestinationToPosition(this.targetPlayer.transform.position);
                return;
            }
        }
        else if (this.targetEnemyAI != null)
        {
            float distance = Vector3.Distance(this.targetEnemyAI.transform.position, this.transform.position);
            Debug.Log("Distance to enemy: " + distance);
            if (this.targetEnemyAI.isEnemyDead || distance > 25f)
            {
                Debug.Log("Stop targeting enemy");
                this.targetEnemyAI = null;
            }
            else if (distance < 2.5f)
            {
                Debug.Log("Target enemy collided");
            
                this.targetEnemyAI.SetEnemyStunned(true, 5f);
            
                ChangeAngryMode(false);
                this.targetEnemyAI = null;
            }
            else
            {
                Debug.Log("Follow enemy");
                this.SetDestinationToPosition(this.targetEnemyAI.transform.position);  
                return;
            }
        }
        
        Debug.Log("Follow owner");
        this.FollowOwner();
    }

    public void AttackPlayer(PlayerControllerB player)
    {
        Debug.Log($"Bees of {this.ownerPlayer.playerUsername} attack {player.playerUsername}");

        ChangeAngryMode(true);
        this.targetPlayer = player;
    }
    
    public void AttackEnemyAI(EnemyAI enemyAI)
    {
        Debug.Log($"Bees of {this.ownerPlayer.playerUsername} attack {enemyAI.enemyType.name}");

        ChangeAngryMode(true);
        this.targetEnemyAI = enemyAI;
    }
    
    private void BeesZapOnTimer()
    {
        if (!this.angry)
        {
            return;
        }
        if (beesZapCurrentTimer > beesZapTimer)
        {
            beesZapCurrentTimer = 0f;
            beesZapTimer = beeZapRandom.Next(1, 7) * 0.06f;
            BeesZap();
        }
        else
        {
            beesZapCurrentTimer += Time.deltaTime;
        }
    }
    
    public void BeesZap()
    {
        if (beeParticles.GetBool("Alive"))
        {
            for (int i = 0; i < lightningPoints.Length; i++)
            {
                lightningPoints[i].transform.position = RoundManager.Instance.GetRandomPositionInBoxPredictable(beeParticlesTarget.transform.position, 4f, beeZapRandom);
            }
            lightningComponent.Trigger(0.1f);
        }

        beeZapAudio.pitch = Random.Range(0.8f, 1.1f);
        beeZapAudio.PlayOneShot(enemyType.audioClips[Random.Range(0, enemyType.audioClips.Length)], Random.Range(0.6f, 1f));
    }

    public void ChangeAngryMode(bool angry)
    {
        this.angry = angry;
        this.ResetBeeZapTimer();
        this.AngryPacket(this.GetComponent<NetworkObject>(), angry);
    }
    
    private void ResetBeeZapTimer()
    {
        beesZapCurrentTimer = 0f;
        beeZapAudio.Stop();
    }
    
    internal static void InitializeRPCS()
    {
        NetworkManager.__rpc_func_table.Add(3703853659u, __rpc_handler_3703853659);
        NetworkManager.__rpc_func_table.Add(1715570234u, __rpc_handler_1715570234);
    }
    
    #region AngryRpc
    
    public void AngryPacket(NetworkObjectReference networkObjectReference, bool angry)
    {
        ClientRpcParams rpcParams = default(ClientRpcParams);
        FastBufferWriter writer = this.__beginSendClientRpc(3703853659u, rpcParams, RpcDelivery.Reliable);
        writer.WriteValueSafe(in networkObjectReference);
        writer.WriteValueSafe(angry);
        this.__endSendClientRpc(ref writer, 3703853659u, rpcParams, RpcDelivery.Reliable);
        Debug.Log("AngryPacket client rpc send finished");
    }
    
    [ClientRpc]
    public void AngryClientRpc(NetworkObjectReference networkObjectReference, bool angry)
    {
        Debug.Log("AngryPacket client rpc received");
        if (networkObjectReference.TryGet(out NetworkObject networkObject))
        {
            RedLocustBeesCustomAI bees = networkObject.GetComponent<RedLocustBeesCustomAI>();
            bees.angry = angry;
            bees.ResetBeeZapTimer();
        }
        else
        {
            Debug.LogError(this.gameObject.name + ": Failed to get network object from network object reference (AngryClientRpc RPC)");
        }
    }

    private static void __rpc_handler_3703853659(NetworkBehaviour target, FastBufferReader reader,
        __RpcParams rpcParams)
    {
        NetworkManager networkManager = target.NetworkManager;
        if (networkManager != null && networkManager.IsListening && !(networkManager.IsServer || networkManager.IsHost))
        {
            Debug.Log("Execute RPC handler " + MethodBase.GetCurrentMethod().Name);
            reader.ReadValueSafe(out NetworkObjectReference networkObjectReference);
            reader.ReadValueSafe(out bool angry);
            ((RedLocustBeesCustomAI) target).AngryClientRpc(networkObjectReference, angry);
        }
    }
    
    #endregion

    #region BeeDamage

    public void BeeDamagePacket(NetworkObjectReference networkObjectReference)
    {
        ClientRpcParams rpcParams = default(ClientRpcParams);
        FastBufferWriter writer = this.__beginSendClientRpc(1715570234u, rpcParams, RpcDelivery.Reliable);
        writer.WriteValueSafe(in networkObjectReference);
        this.__endSendClientRpc(ref writer, 1715570234u, rpcParams, RpcDelivery.Reliable);
        Debug.Log("BeeDamagePacket client rpc send finished");
    }
    
    [ClientRpc]
    public void BeeDamageClientRpc(NetworkObjectReference networkObjectReference)
    {
        Debug.Log("BeeDamage client rpc received");
        if (networkObjectReference.TryGet(out NetworkObject networkObject))
        {
            PlayerControllerB player = networkObject.GetComponent<PlayerControllerB>();
            player.DamagePlayer(30, hasDamageSFX: true, callRPC: true, CauseOfDeath.Electrocution, 3);
        }
        else
        {
            Debug.LogError(this.gameObject.name + ": Failed to get network object from network object reference (BeeDamageClientRpc RPC)");
        }
    }

    private static void __rpc_handler_1715570234(NetworkBehaviour target, FastBufferReader reader,
        __RpcParams rpcParams)
    {
        NetworkManager networkManager = target.NetworkManager;
        if (networkManager != null && networkManager.IsListening)
        {
            Debug.Log("Execute RPC handler " + MethodBase.GetCurrentMethod().Name);
            reader.ReadValueSafe(out NetworkObjectReference networkObjectReference);
            ((RedLocustBeesCustomAI) target).BeeDamageClientRpc(networkObjectReference);
        }
    }

    #endregion
}