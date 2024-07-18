using System;
using GameNetcodeStuff;
using LethalMon.Patches;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.Behaviours;

public class RedLocustBeesTamedBehaviour : TamedEnemyBehaviour
{
    #region Properties
    internal RedLocustBees bees { get; private set; }

    public bool angry = false;
    #endregion

    #region Base Methods
    internal override void OnUpdate(bool update = false, bool doAIInterval = true)
    {
        base.OnUpdate(update, false);

        BeesZapOnTimer();
    }

    internal override void Start()
    {
        base.Start();

        bees = (Enemy as RedLocustBees)!;
        if (bees == null)
            bees = gameObject.AddComponent<RedLocustBees>();

        if (this.ownerPlayer == null) return;
        
        if(bees.agent != null)
            bees.agent.speed = 10.3f;
            
        bees.beesIdle.volume = 0.2f;
        bees.beesDefensive.volume = 0.2f;
        bees.beesAngry.Stop();
        bees.beeZapAudio.Stop();
        bees.gameObject.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f); // Make it smaller
    }

    internal override void OnTamedDefending()
    {
        if (!angry)
            ChangeAngryMode(true);

        if (targetPlayer != null)
        {
            float distance = Vector3.Distance(targetPlayer.transform.position, bees.transform.position);
            LethalMon.Log("Distance to player: " + distance);
            if (targetPlayer.isPlayerDead || !targetPlayer.isPlayerControlled || distance > 25f)
            {
                LethalMon.Log("Stop targeting player");
                targetPlayer = null;
            }
            else if (distance < 2.5f)
            {
                LethalMon.Log("Target player collided");

                BeeDamageServerRPC(targetPlayer.GetComponent<NetworkObject>());

                ChangeAngryMode(false);
                targetPlayer = null;
            }
            else
            {
                LethalMon.Log("Follow player");
                bees.SetDestinationToPosition(targetPlayer.transform.position);
                return;
            }
        }
        else if (targetEnemy != null)
        {
            float distance = Vector3.Distance(targetEnemy.transform.position, bees.transform.position);
            LethalMon.Log("Distance to enemy: " + distance);
            if (targetEnemy.isEnemyDead || distance > 25f)
            {
                LethalMon.Log("Stop targeting enemy");
                targetEnemy = null;
            }
            else if (distance < 2.5f)
            {
                LethalMon.Log("Target enemy collided");

                targetEnemy.SetEnemyStunned(true, 5f);

                ChangeAngryMode(false);
                targetEnemy = null;
            }
            else
            {
                LethalMon.Log("Follow enemy");
                bees.SetDestinationToPosition(targetEnemy.transform.position);
                return;
            }
        }
        else
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
    }

    internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
    {
        base.OnEscapedFromBall(playerWhoThrewBall);

        if (Utils.IsHost)
        {
            bees.SetMovingTowardsTargetPlayer(playerWhoThrewBall);
            bees.SwitchToBehaviourState(2);
            RedLocustBeesPatch.AngryUntil.Add(bees.GetInstanceID(),
                DateTime.Now.AddSeconds(10)); // todo: solve locally here instead of patch
        }
    }
    #endregion

    #region Methods
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

    public void ChangeAngryMode(bool angry)
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
        AngryServerRpc(bees.GetComponent<NetworkObject>(), angry);
    }
    
    private void ResetBeeZapTimer()
    {
        bees.beesZapCurrentTimer = 0f;
        bees.beeZapAudio.Stop();
    }
    #endregion

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
        LethalMon.Log("BeeDamage client rpc received");
        if (!networkObjectReference.TryGet(out NetworkObject networkObject))
        {
            LethalMon.Log(bees.gameObject.name + ": Failed to get network object from network object reference (BeeDamageClientRpc RPC)", LethalMon.LogType.Error);
            return;
        }

        if(!networkObject.TryGetComponent( out PlayerControllerB player))
        {
            LethalMon.Log(bees.gameObject.name + ": Failed to get player object (BeeDamageClientRpc RPC)", LethalMon.LogType.Error  );
            return;
        }

        player.DamagePlayer(30, hasDamageSFX: true, callRPC: true, CauseOfDeath.Electrocution, 3);
    }
    #endregion
}