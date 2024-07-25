using System.Globalization;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace LethalMon.Behaviours;

public class CooldownNetworkBehaviour : NetworkBehaviour
{
    public NetworkVariable<FixedString64Bytes> Id { get; private set;  }
    
    public NetworkVariable<FixedString512Bytes> DisplayName { get; private set; }
    
    public NetworkVariable<float> CooldownTime { get; private set; }
    
    public float CurrentTimer { get; private set; }
    
    private bool _needSyncing = true;

    private Image? _cooldownCircle;

    private TextMeshProUGUI? _cooldownTime;

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

        if (_cooldownCircle != null)
        {
            _cooldownCircle.fillAmount = Mathf.Clamp(CurrentTimer / CooldownTime.Value, 0, 1);
            
            float cooldownLeftTime = Mathf.Clamp(CooldownTime.Value - CurrentTimer, 0, CooldownTime.Value);
            _cooldownTime!.text = cooldownLeftTime == 0f ? "" : Mathf.Floor(cooldownLeftTime).ToString(CultureInfo.InvariantCulture);
        }
        
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

    public void BindToHUD(Image cooldownCircle, TextMeshProUGUI cooldownTime, TextMeshProUGUI cooldownName)
    {
        _cooldownCircle = cooldownCircle;
        _cooldownTime = cooldownTime;
        cooldownName.text = DisplayName.Value.ToString();
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