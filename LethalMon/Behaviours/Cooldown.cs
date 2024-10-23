namespace LethalMon.Behaviours
{
    /// <summary>
    /// Represents a cooldown behavior with an identifier, display name, and cooldown time.
    /// </summary>
    public class Cooldown
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Cooldown"/> class.
        /// </summary>
        /// <param name="id">The unique identifier for the cooldown (usually monstername_cooldowntype).</param>
        /// <param name="displayName">The display name for the cooldown (in the HUD).</param>
        /// <param name="cooldownTime">The duration of the cooldown in seconds.</param>
        public Cooldown(string id, string displayName, float cooldownTime)
        {
            Id = id;
            DisplayName = displayName;
            CooldownTime = cooldownTime;
        }

        /// <summary>
        /// Gets the unique identifier for the cooldown.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Gets the display name for the cooldown.
        /// </summary>
        public string DisplayName { get; private set; }

        /// <summary>
        /// Gets the duration of the cooldown in seconds.
        /// </summary>
        public float CooldownTime { get; private set; }
    }
}