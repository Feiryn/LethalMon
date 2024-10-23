using System;
using GameNetcodeStuff;
using LethalMon.Behaviours;
using UnityEngine;

namespace LethalMon.CatchableEnemy;

/// <summary>
/// Represents a catchable enemy.
/// </summary>
/// <param name="displayName">The display name of the monster</param>
/// <param name="catchDifficulty">The difficulty to capture the monster (0-9). <see cref="Data.CaptureProbabilities"/></param>
/// <param name="behaviourDescription">The behaviour description of the monster shown in the PC's dex</param>
public abstract class CatchableEnemy(string displayName, int catchDifficulty, string behaviourDescription)
{
    private readonly int _baseCatchDifficulty = catchDifficulty;
    
    /// <summary>
    /// Difficulty to capture the monster (0-9). <see cref="Data.CaptureProbabilities"/>
    /// </summary>
    public int CatchDifficulty => Math.Clamp(_baseCatchDifficulty + ModConfig.Instance.values.CaptureRateModifier, 0, 9);

    /// <summary>
    /// Price to duplicate the monster.
    /// </summary>
    public int DuplicationPrice => Math.Max(ModConfig.Instance.values.DuplicationPrices[Math.Clamp(_baseCatchDifficulty, 0, 9)], 0);
    
    /// <summary>
    /// The display name of the monster
    /// </summary>
    public string DisplayName { private set; get; } = displayName;
    
    /// <summary>
    /// The behaviour description of the monster shown in the PC's dex
    /// </summary>
    public string BehaviourDescription { get; } = behaviourDescription;

    internal float GetCaptureProbability(int ballStrength, EnemyAI? enemyAI = null)
    {
        if (ballStrength < 0 || ballStrength >= Data.CaptureProbabilities.Length || this.CatchDifficulty < 0 || this.CatchDifficulty >= Data.CaptureProbabilities[0].Length)
        {
            return 0;
        }

        var captureProbability = Data.CaptureProbabilities[ballStrength][this.CatchDifficulty];

        IncreaseProbabilityByReducedHp(enemyAI, ref captureProbability);

        return captureProbability;
    }

    /* Calculation
     * a = capture probability -> 0f (impossible) to 1f (guaranteed)
     * b = hp reduction -> 0f (no damage) to 1f (dead)
     * c = multiplier
     * 
     * formula: a + a * b * c * (1 - a)
     */
    private static void IncreaseProbabilityByReducedHp(EnemyAI? enemyAI, ref float captureProbability)
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
    
    internal void CatchFailBehaviour(EnemyAI enemyAI, PlayerControllerB player)
    {
        if (ModConfig.Instance.values.MonstersReactToFailedCaptures && enemyAI.gameObject.TryGetComponent(out TamedEnemyBehaviour tamedEnemyBehaviour))
            tamedEnemyBehaviour.OnEscapedFromBall(player);
    }

    /// <summary>
    /// Function to check if the enemy can be captured by the player.
    /// </summary>
    /// <param name="enemyAI"></param>
    /// <param name="player"></param>
    /// <returns>Whether the enemy can be captured by the player</returns>
    public virtual bool CanBeCapturedBy(EnemyAI enemyAI, PlayerControllerB player) { return true; }
    
    /// <summary>
    /// Function to execute before capturing the enemy.
    /// </summary>
    /// <param name="enemyAI">The enemy AI</param>
    /// <param name="player">The player that threw the ball</param>
    public virtual void BeforeCapture(EnemyAI enemyAI, PlayerControllerB player) { }
}