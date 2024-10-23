using System.Globalization;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace LethalMon.Behaviours;

public class CooldownNetworkBehaviour : NetworkBehaviour
{
    /// <summary>
    /// Gets the unique identifier for the cooldown.
    /// </summary>
    public NetworkVariable<FixedString64Bytes>? Id { get; private set; }

    /// <summary>
    /// Gets the display name for the cooldown.
    /// </summary>
    public NetworkVariable<FixedString512Bytes>? DisplayName { get; private set; }

    /// <summary>
    /// Gets the duration of the cooldown in seconds.
    /// </summary>
    public NetworkVariable<float>? CooldownTime { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the cooldown is paused.
    /// </summary>
    public bool Paused { get; private set; } = false;

    /// <summary>
    /// Gets the current timer value.
    /// </summary>
    public float CurrentTimer { get; private set; }

    private bool _needSyncing = true;

    private Image? _cooldownCircle;

    private TextMeshProUGUI? _cooldownTime;

    /// <summary>
    /// Initializes the timer with a specified value.
    /// </summary>
    /// <param name="timer">The initial timer value.</param>
    internal void InitTimer(float timer)
    {
        CurrentTimer = timer;
        _needSyncing = true;
    }

    /// <summary>
    /// Sets up the cooldown with the specified model.
    /// </summary>
    /// <param name="cooldownModel">The cooldown model.</param>
    internal void Setup(Cooldown cooldownModel)
    {
        Id = new NetworkVariable<FixedString64Bytes>(new FixedString64Bytes(cooldownModel.Id));
        DisplayName = new NetworkVariable<FixedString512Bytes>(new FixedString512Bytes(cooldownModel.DisplayName));
        CooldownTime = new NetworkVariable<float>(cooldownModel.CooldownTime);
        CurrentTimer = CooldownTime.Value;
    }

    /// <summary>
    /// Updates the cooldown timer and UI elements.
    /// </summary>
    internal void Update()
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

    /// <summary>
    /// Checks if the cooldown has finished.
    /// </summary>
    /// <returns>True if the cooldown has finished; otherwise, false.</returns>
    public bool IsFinished()
    {
        if (CooldownTime == null) return false;

        return CurrentTimer >= CooldownTime.Value;
    }

    /// <summary>
    /// Resets the cooldown timer.
    /// </summary>
    public void Reset()
    {
        CurrentTimer = 0f;
        _needSyncing = true;
    }

    /// <summary>
    /// Synchronizes the cooldown timer between clients and the server.
    /// </summary>
    private void Sync()
    {
        _needSyncing = false;
        SyncCooldownServerRpc();
    }

    /// <summary>
    /// Pauses the cooldown timer.
    /// </summary>
    public void Pause()
    {
        Paused = true;
        _needSyncing = true;
    }

    /// <summary>
    /// Resumes the cooldown timer.
    /// </summary>
    public void Resume()
    {
        Paused = false;
        _needSyncing = true;
    }

    /// <summary>
    /// Binds the cooldown to the HUD elements.
    /// </summary>
    /// <param name="cooldownCircle">The image representing the cooldown circle.</param>
    /// <param name="cooldownTime">The text element displaying the cooldown time.</param>
    /// <param name="cooldownName">The text element displaying the cooldown name.</param>
    internal void BindToHUD(Image cooldownCircle, TextMeshProUGUI cooldownTime, TextMeshProUGUI cooldownName)
    {
        _cooldownCircle = cooldownCircle;
        _cooldownTime = cooldownTime;
        cooldownName.text = DisplayName != null ? DisplayName.Value.ToString() : "";
    }

    /// <summary>
    /// Server RPC to synchronize the cooldown timer with clients.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void SyncCooldownServerRpc()
    {
        LethalMon.Log($"Send cooldown \"{(DisplayName != null ? DisplayName.Value : "")}\" syncing to {CurrentTimer}");
        SyncCooldownClientRpc(CurrentTimer, NetworkManager.ServerTime.Time, Paused);
    }

    /// <summary>
    /// Client RPC to update the cooldown timer based on server synchronization.
    /// </summary>
    /// <param name="currentTimer">The current timer value.</param>
    /// <param name="serverTime">The server time.</param>
    /// <param name="paused">Indicates whether the cooldown is paused.</param>
    [ClientRpc]
    public void SyncCooldownClientRpc(float currentTimer, double serverTime, bool paused)
    {
        CurrentTimer = currentTimer + (float) (NetworkManager.ServerTime.Time - serverTime);
        Paused = paused;
        LethalMon.Log($"Cooldown \"{DisplayName?.Value ?? ""}\"'s timer has been set to {CurrentTimer} (paused: " + Paused + ")");
    }
}