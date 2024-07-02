using GameNetcodeStuff;

namespace LethalMon.CatchableEnemy;

/// <summary>
/// An enemy that can be catched
/// </summary>
public abstract class CatchableEnemy
{
    /// <summary>
    /// Difficulty to capture the monster (0-9). <see cref="Data.CaptureProbabilities"/>
    /// </summary>
    private readonly int _catchDifficulty;

    /// <summary>
    /// The display name of the monster
    /// </summary>
    public string DisplayName { private set; get; }

    /// <summary>
    /// The ID of the monster (used to save the monster in the ball when the game is closed). Must be unique across the mod
    /// todo Replace because there can be collisions
    /// </summary>
    public int Id { get; }

    protected CatchableEnemy(int id, string displayName, int catchDifficulty)
    {
        this.Id = id;
        this.DisplayName = displayName;
        this._catchDifficulty = catchDifficulty;
    }

    public double GetCaptureProbability(int ballStrength)
    {
        if (ballStrength < 0 || ballStrength >= Data.CaptureProbabilities.Length || this._catchDifficulty < 0 || this._catchDifficulty >= Data.CaptureProbabilities[0].Length)
        {
            return 0;
        }

        return Data.CaptureProbabilities[ballStrength][this._catchDifficulty];
    }
    
    /// <summary>
    /// Behaviour triggered if the capture fails
    /// </summary>
    /// <param name="enemyAI">Enemy that was captured</param>
    /// <param name="player">The player that threw the ball</param>
    public abstract void CatchFailBehaviour(EnemyAI enemyAI, PlayerControllerB player);
}