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

                BeeDamageServerRPC(bees.targetPlayer.GetComponent<NetworkObject>());

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
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
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

        bees.beeZapAudio.pitch = UnityEngine.Random.Range(0.8f, 1.1f);
        bees.beeZapAudio.PlayOneShot(bees.enemyType.audioClips[UnityEngine.Random.Range(0, bees.enemyType.audioClips.Length)], UnityEngine.Random.Range(0.6f, 1f));
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
            AngryServerRpc(bees.GetComponent<NetworkObject>(), angry);
    }
    
    private void ResetBeeZapTimer()
    {
        bees.beesZapCurrentTimer = 0f;
        bees.beeZapAudio.Stop();
    }

    #region RPCs

    [ServerRpc(RequireOwnership = false)]
    public void AngryServerRpc(NetworkObjectReference networkObjectReference, bool angry)
    {
        AngryClientRpc(angry);
    }
    
    [ClientRpc]
    public void AngryClientRpc(bool angry)
    {
        this.angry = angry;
        //ChangeAngryMode(angry, false);
        ResetBeeZapTimer();
    }

    [ServerRpc(RequireOwnership = false)]
    public void BeeDamageServerRPC(NetworkObjectReference networkObjectReference)
    {
        BeeDamageClientRpc(networkObjectReference);
    }
    
    [ClientRpc]
    public void BeeDamageClientRpc(NetworkObjectReference networkObjectReference)
    {
        Debug.Log("BeeDamage client rpc received");
        if (!networkObjectReference.TryGet(out NetworkObject networkObject))
        {
            Debug.LogError(bees.gameObject.name + ": Failed to get network object from network object reference (BeeDamageClientRpc RPC)");
            return;
        }

        if(!networkObject.TryGetComponent( out PlayerControllerB player))
        {
            Debug.LogError(bees.gameObject.name + ": Failed to get player object (BeeDamageClientRpc RPC)");
            return;
        }

        player.DamagePlayer(30, hasDamageSFX: true, callRPC: true, CauseOfDeath.Electrocution, 3);
    }
    #endregion
}