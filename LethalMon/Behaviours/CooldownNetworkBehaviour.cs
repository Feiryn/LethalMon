using System.Globalization;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace LethalMon.Behaviours;

public class CooldownNetworkBehaviour : NetworkBehaviour
{
    public NetworkVariable<FixedString64Bytes>? Id { get; private set; }

    public NetworkVariable<FixedString512Bytes>? DisplayName { get; private set; }
    
    public NetworkVariable<float>? CooldownTime { get; private set; }

    public bool Paused { get; private set; } = false;
    
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
        if (!Paused)
        {
            CurrentTimer += Time.deltaTime;   
        }

        if (_cooldownCircle != null && CooldownTime != null)
        {
            if (Paused && CurrentTimer == 0f)
            {
                _cooldownCircle.fillAmount = 1;
            }
            else
            {
                _cooldownCircle.fillAmount = Mathf.Clamp(CurrentTimer / CooldownTime.Value, 0, 1);
            }
            
            float cooldownLeftTime = Mathf.Clamp(CooldownTime.Value - CurrentTimer, 0, CooldownTime.Value);
            _cooldownTime!.text = cooldownLeftTime == 0f ? "" : ((int) Mathf.Round(cooldownLeftTime)).ToString(CultureInfo.InvariantCulture);
        }
        
        if (_needSyncing)
        {
            Sync();
        }
    }

    public bool IsFinished()
    {
        if (CooldownTime == null) return false;

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

    public void Pause()
    {
        Paused = true;
        _needSyncing = true;
    }
    
    public void Resume()
    {
        Paused = false;
        _needSyncing = true;
    }
    
    public void BindToHUD(Image cooldownCircle, TextMeshProUGUI cooldownTime, TextMeshProUGUI cooldownName)
    {
        _cooldownCircle = cooldownCircle;
        _cooldownTime = cooldownTime;
        cooldownName.text = DisplayName != null ? DisplayName.Value.ToString() : "";
    }

    [ServerRpc(RequireOwnership = false)]
    public void SyncCooldownServerRpc()
    {
        LethalMon.Log($"Send cooldown \"{(DisplayName != null ? DisplayName.Value : "")}\" syncing to {CurrentTimer}");
        SyncCooldownClientRpc(CurrentTimer, NetworkManager.ServerTime.Time, Paused);
    }

    [ClientRpc]
    public void SyncCooldownClientRpc(float currentTimer, double serverTime, bool paused)
    {
        CurrentTimer = currentTimer + (float) (NetworkManager.ServerTime.Time - serverTime);
        Paused = paused;
        LethalMon.Log($"Cooldown \"{(DisplayName != null ? DisplayName.Value : "")}\"'s timer has been set to {CurrentTimer} (paused: " + Paused + ")");
    }
}