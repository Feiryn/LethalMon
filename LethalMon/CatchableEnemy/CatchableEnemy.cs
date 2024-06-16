using GameNetcodeStuff;
using LethalMon.AI;
using UnityEngine;

namespace LethalMon.CatchableEnemy;

public abstract class CatchableEnemy
{
    private readonly int _catchDifficulty;

    public int Id { get; }

    protected CatchableEnemy(int id, int catchDifficulty)
    {
        this.Id = id;
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

    public abstract void CatchFailBehaviour(EnemyAI enemyAI, PlayerControllerB player);

    public abstract CustomAI AddAiComponent(GameObject gameObject);
}