using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.Behaviours;

public class CooldownNetworkBehaviour : NetworkBehaviour
{
    public NetworkVariable<FixedString64Bytes> Id { get; private set;  }
    
    public NetworkVariable<FixedString512Bytes> DisplayName { get; private set; }
    
    public NetworkVariable<float> CooldownTime { get; private set; }
    
    public float CurrentTimer { get; private set; }
    
    private bool _needSyncing = true;

    public void InitTimer(float timer)
    {
        CurrentTimer = timer;
        _needSyncing = true;
    }
    
    public void Setup(Cooldown cooldownModel)
    {
        Id = new NetworkVariable<FixedString64Bytes>(new FixedString64Bytes(cooldownModel.Id));
        DisplayName = new NetworkVariable<FixedString512Bytes>(new FixedString512Bytes(cooldownModel.DisplayName));
        CooldownTime = new NetworkVariable<float>(cooldownModel.CooldownTime);
        CurrentTimer = CooldownTime.Value;
    }
    
    public void Update()
    {
        CurrentTimer += Time.deltaTime;

        if (_needSyncing)
        {
            Sync();
        }
    }

    public bool IsFinished()
    {
        return CurrentTimer >= CooldownTime.Value;
    }

    public void Reset()
    {
        CurrentTimer = 0f;
        _needSyncing = true;
    }

    public void Sync()
    {
        _needSyncing = false;
        SyncCooldownServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void SyncCooldownServerRpc()
    {
        LethalMon.Log($"Send cooldown \"{DisplayName.Value}\" syncing to {CurrentTimer}");
        SyncCooldownClientRpc(CurrentTimer, NetworkManager.ServerTime.Time);
    }

    [ClientRpc]
    public void SyncCooldownClientRpc(float currentTimer, double serverTime)
    {
        CurrentTimer = currentTimer + (float) (NetworkManager.ServerTime.Time - serverTime);
        LethalMon.Log($"Cooldown \"{DisplayName.Value}\"'s timer has been set to {CurrentTimer}");
    }
}