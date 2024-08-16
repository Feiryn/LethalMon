using System;
using GameNetcodeStuff;
using LethalMon.Patches;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.Behaviours;

public class RedLocustBeesTamedBehaviour : TamedEnemyBehaviour
{
    #region Properties
    internal RedLocustBees? _bees = null;
    internal RedLocustBees Bees
    {
        get
        {
            if (_bees == null)
                _bees = (Enemy as RedLocustBees)!;

            return _bees;
        }
    }

    private bool _angry = false;
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

        if (!IsTamed) return;
        
        if(Bees.agent != null)
            Bees.agent.speed = 10.3f;
            
        Bees.beesIdle.volume = 0.2f;
        Bees.beesDefensive.volume = 0.2f;
        Bees.beesAngry.Stop();
        Bees.beeZapAudio.Stop();
        Bees.gameObject.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f); // Make them stick tighter together
    }

    internal override void OnTamedDefending()
    {
        if (!_angry)
            ChangeAngryMode(true);

        if (targetPlayer != null)
        {
            float distance = Vector3.Distance(targetPlayer.transform.position, Bees.transform.position);
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
                Bees.SetDestinationToPosition(targetPlayer.transform.position);
                return;
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

                targetEnemy.SetEnemyStunned(true, 5f);

                ChangeAngryMode(false);
                targetEnemy = null;
            }
            else
            {
                LethalMon.Log("Follow enemy");
                Bees.SetDestinationToPosition(targetEnemy.transform.position);
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
            Bees.SetMovingTowardsTargetPlayer(playerWhoThrewBall);
            Bees.SwitchToBehaviourState(2);
            RedLocustBeesPatch.AngryUntil.Add(Bees.GetInstanceID(),
                DateTime.Now.AddSeconds(10)); // todo: solve locally here instead of patch
        }
    }
    #endregion

    #region Methods
    private void BeesZapOnTimer()
    {
        if (!_angry)
            return;

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

    public void ChangeAngryMode(bool angry)
    {
        if(angry && targetEnemy == null && targetPlayer == null)
        {
            LethalMon.Logger.LogWarning("Attempting to make bees angry, but no target was defined.");
            return;
        }

        this._angry = angry;
        if(!angry)
        {
            targetEnemy = null;
            targetPlayer = null;
        }

        ResetBeeZapTimer();
        AngryServerRpc(Bees.GetComponent<NetworkObject>(), angry);
    }
    
    private void ResetBeeZapTimer()
    {
        Bees.beesZapCurrentTimer = 0f;
        Bees.beeZapAudio.Stop();
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
        _angry = angry;
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