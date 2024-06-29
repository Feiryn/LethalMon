using System.Reflection;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.AI;

public class RedLocustBeesTamedBehaviour : TamedEnemyBehaviour
{
    internal RedLocustBees bees { get; private set; }

    public bool angry = false;

    public override void OnUpdate()
    {
        base.Update();

        BeesZapOnTimer();
    }

    public override void Start()
    {
        base.Start();

        bees = (Enemy as RedLocustBees)!;

        LethalMon.Logger.LogWarning("RedLocustBeesCustomAI.Start: " + (bees == null).ToString());
        if(bees?.agent != null )
            bees.agent.speed = 10.3f;
    }

    internal override void OnTamedDefending()
    {
        if (!angry)
            ChangeAngryMode(true);

        if (bees.targetPlayer != null)
        {
            float distance = Vector3.Distance(bees.targetPlayer.transform.position, bees.transform.position);
            Debug.Log("Distance to player: " + distance);
            if (bees.targetPlayer.isPlayerDead || !bees.targetPlayer.isPlayerControlled || distance > 25f)
            {
                Debug.Log("Stop targeting player");
                bees.targetPlayer = null;
            }
            else if (distance < 2.5f)
            {
                Debug.Log("Target player collided");

                BeeDamagePacket(bees.targetPlayer.GetComponent<NetworkObject>());

                ChangeAngryMode(false);
                bees.targetPlayer = null;
            }
            else
            {
                Debug.Log("Follow player");
                bees.SetDestinationToPosition(bees.targetPlayer.transform.position);
                return;
            }
        }
        else if (targetEnemy != null)
        {
            float distance = Vector3.Distance(targetEnemy.transform.position, bees.transform.position);
            Debug.Log("Distance to enemy: " + distance);
            if (targetEnemy.isEnemyDead || distance > 25f)
            {
                Debug.Log("Stop targeting enemy");
                targetEnemy = null;
            }
            else if (distance < 2.5f)
            {
                Debug.Log("Target enemy collided");

                targetEnemy.SetEnemyStunned(true, 5f);

                ChangeAngryMode(false);
                targetEnemy = null;
            }
            else
            {
                Debug.Log("Follow enemy");
                bees.SetDestinationToPosition(targetEnemy.transform.position);
                return;
            }
        }
        else
            SwitchToCustomBehaviour(CustomBehaviour.TamedFollowing);
    }
    
    private void BeesZapOnTimer()
    {
        if (!angry)
            return;

        if (bees.beeZapRandom == null)
            bees.beeZapRandom = new System.Random();

        if (bees.beesZapCurrentTimer > bees.beesZapTimer)
        {
            bees.beesZapCurrentTimer = 0f;
            bees.beesZapTimer = bees.beeZapRandom.Next(1, 7) * 0.06f;
            BeesZap();
        }
        else
        {
            bees.beesZapCurrentTimer += Time.deltaTime;
        }
    }
    
    public void BeesZap()
    {
        if (bees.beeParticles.GetBool("Alive"))
        {
            if (bees.beeZapRandom == null)
                bees.beeZapRandom = new System.Random();

            for (int i = 0; i < bees.lightningPoints.Length; i++)
            {
                bees.lightningPoints[i].transform.position = RoundManager.Instance.GetRandomPositionInBoxPredictable(bees.beeParticlesTarget.transform.position, 4f, bees.beeZapRandom);
            }
            bees.lightningComponent.Trigger(0.1f);
        }

        bees.beeZapAudio.pitch = Random.Range(0.8f, 1.1f);
        bees.beeZapAudio.PlayOneShot(bees.enemyType.audioClips[Random.Range(0, bees.enemyType.audioClips.Length)], Random.Range(0.6f, 1f));
    }

    public void ChangeAngryMode(bool angry, bool syncRpc = true)
    {
        if(angry && targetEnemy == null && targetPlayer == null)
        {
            LethalMon.Logger.LogWarning("Attempting to make bees angry, but no target was defined.");
            return;
        }

        this.angry = angry;
        if(!angry)
        {
            targetEnemy = null;
            targetPlayer = null;
        }

        ResetBeeZapTimer();
        if(syncRpc)
            AngryPacket(bees.GetComponent<NetworkObject>(), angry);
    }
    
    private void ResetBeeZapTimer()
    {
        bees.beesZapCurrentTimer = 0f;
        bees.beeZapAudio.Stop();
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
        FastBufferWriter writer = __beginSendClientRpc(3703853659u, rpcParams, RpcDelivery.Reliable);
        writer.WriteValueSafe(in networkObjectReference);
        writer.WriteValueSafe(angry);
        __endSendClientRpc(ref writer, 3703853659u, rpcParams, RpcDelivery.Reliable);
        Debug.Log("AngryPacket client rpc send finished");
    }
    
    [ClientRpc]
    public void AngryClientRpc(NetworkObjectReference networkObjectReference, bool angry)
    {
        Debug.Log("AngryPacket client rpc received");
        if (networkObjectReference.TryGet(out NetworkObject networkObject))
        {
            RedLocustBeesTamedBehaviour bees = networkObject.GetComponent<RedLocustBeesTamedBehaviour>();
            bees.angry = angry;
            bees.ResetBeeZapTimer();
        }
        else
        {
            Debug.LogError(bees.gameObject.name + ": Failed to get network object from network object reference (AngryClientRpc RPC)");
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
            ((RedLocustBeesTamedBehaviour) target).AngryClientRpc(networkObjectReference, angry);
        }
    }
    
    #endregion

    #region BeeDamage

    public void BeeDamagePacket(NetworkObjectReference networkObjectReference)
    {
        ClientRpcParams rpcParams = default(ClientRpcParams);
        FastBufferWriter writer = __beginSendClientRpc(1715570234u, rpcParams, RpcDelivery.Reliable);
        writer.WriteValueSafe(in networkObjectReference);
        __endSendClientRpc(ref writer, 1715570234u, rpcParams, RpcDelivery.Reliable);
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
            Debug.LogError(bees.gameObject.name + ": Failed to get network object from network object reference (BeeDamageClientRpc RPC)");
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
            ((RedLocustBeesTamedBehaviour) target).BeeDamageClientRpc(networkObjectReference);
        }
    }

    #endregion
}