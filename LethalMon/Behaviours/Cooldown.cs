namespace LethalMon.Behaviours;

public class Cooldown(string id, string displayName, float cooldownTime)
{
    public string Id { get; private set; } = id;

    public string DisplayName { get; private set; } = displayName;

    public float CooldownTime { get; private set; } = cooldownTime;
}