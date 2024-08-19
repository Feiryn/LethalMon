using System;
using GameNetcodeStuff;
using LethalMon.Behaviours;
using UnityEngine;

namespace LethalMon.CatchableEnemy;

/// <summary>
/// An enemy that can be catched
/// </summary>
public abstract class CatchableEnemy(int id, string displayName, int catchDifficulty)
{
    private readonly int _baseCatchDifficulty = catchDifficulty;
    
    /// <summary>
    /// Difficulty to capture the monster (0-9). <see cref="Data.CaptureProbabilities"/>
    /// </summary>
    public int CatchDifficulty => Math.Clamp(_baseCatchDifficulty + ModConfig.Instance.values.CaptureRateModifier, 0, 9);

    /// <summary>
    /// The display name of the monster
    /// </summary>
    public string DisplayName { private set; get; } = displayName;

    /// <summary>
    /// The ID of the monster (used to save the monster in the ball when the game is closed). Must be unique across the mod
    /// todo Replace because there can be collisions
    /// </summary>
    public int Id { get; } = id;

    public float GetCaptureProbability(int ballStrength, EnemyAI? enemyAI = null)
    {
        if (ballStrength < 0 || ballStrength >= Data.CaptureProbabilities.Length || this.CatchDifficulty < 0 || this.CatchDifficulty >= Data.CaptureProbabilities[0].Length)
        {
            return 0;
        }

        var captureProbability = Data.CaptureProbabilities[ballStrength][this.CatchDifficulty];

        IncreaseProbabilityByReducedHP(enemyAI, ref captureProbability);

        return captureProbability;
    }

    /* Calculation
     * a = capture probability -> 0f (impossible) to 1f (guaranteed)
     * b = hp reduction -> 0f (no damage) to 1f (dead)
     * c = multiplier
     * 
     * formula: a + a * b * c * (1 - a)
     */
    public void IncreaseProbabilityByReducedHP(EnemyAI? enemyAI, ref float captureProbability)
    {
        var multiplier = ModConfig.Instance.values.EnemyHPCaptureProbabilityMultiplier;
        if (multiplier <= 0f || captureProbability >= 1f)
            return;

        if (enemyAI == null || !enemyAI.enemyType.canDie || !enemyAI.enemyType.enemyPrefab.TryGetComponent(out EnemyAI enemyAIDefault))
            return;

        var hpReduction = 1f - (float)enemyAI.enemyHP / enemyAIDefault.enemyHP;
        //LethalMon.Log($"Enemy has {enemyAI.enemyHP}/{enemyAIDefault.enemyHP} HP ({(1f - hpReduction) * 100f}%)");

        var failureProbability = 1f - captureProbability;
        var additionalSuccessChance = captureProbability * hpReduction * multiplier * failureProbability;

        //LethalMon.Log("Previously had a captureProbability of " + captureProbability);
        captureProbability = Mathf.Min(captureProbability + additionalSuccessChance, 1f);
       // LethalMon.Log("Now has a captureProbability of " + captureProbability);
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