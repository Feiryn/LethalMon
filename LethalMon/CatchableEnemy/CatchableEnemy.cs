using System;
using GameNetcodeStuff;
using LethalMon.Behaviours;

namespace LethalMon.CatchableEnemy;

/// <summary>
/// An enemy that can be catched
/// </summary>
public abstract class CatchableEnemy
{
    private readonly int _baseCatchDifficulty;
    
    /// <summary>
    /// Difficulty to capture the monster (0-9). <see cref="Data.CaptureProbabilities"/>
    /// </summary>
    public int CatchDifficulty => Math.Clamp(_baseCatchDifficulty + ModConfig.Instance.values.CaptureRateModifier, 0, 9);

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
        this._baseCatchDifficulty = catchDifficulty;
    }

    public float GetCaptureProbability(int ballStrength)
    {
        if (ballStrength < 0 || ballStrength >= Data.CaptureProbabilities.Length || this.CatchDifficulty < 0 || this.CatchDifficulty >= Data.CaptureProbabilities[0].Length)
        {
            return 0;
        }

        return Data.CaptureProbabilities[ballStrength][this.CatchDifficulty];
    }
    
    /// <summary>
    /// Behaviour triggered if the capture fails
    /// </summary>
    /// <param name="enemyAI">Enemy that was captured</param>
    /// <param name="player">The player that threw the ball</param>
    public void CatchFailBehaviour(EnemyAI enemyAI, PlayerControllerB player)
    {
        if (ModConfig.Instance.values.MonstersReactToFailedCaptures && enemyAI.gameObject.TryGetComponent(out TamedEnemyBehaviour tamedEnemyBehaviour))
            tamedEnemyBehaviour.OnEscapedFromBall(player);
    }

    public virtual bool CanBeCapturedBy(EnemyAI enemyAI, PlayerControllerB player) { return true; }
    
    public virtual void BeforeCapture(EnemyAI enemyAI, PlayerControllerB player) { }
}