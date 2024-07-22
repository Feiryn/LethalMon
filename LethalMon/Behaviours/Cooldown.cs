namespace LethalMon.Behaviours;

public class Cooldown
{
    public string Id { get; private set;  }
    
    public string DisplayName { get; private set; }
    
    public float CooldownTime { get; private set; }

    public Cooldown(string id, string displayName, float cooldownTime)
    {
        Id = id;
        DisplayName = displayName;
        CooldownTime = cooldownTime;
    }
}