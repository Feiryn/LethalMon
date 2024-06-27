using GameNetcodeStuff;

namespace LethalMon.CatchableEnemy;

public abstract class CatchableEnemy
{
    private readonly int _catchDifficulty;

    public string DisplayName { private set; get; }

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
    
    public abstract void CatchFailBehaviour(EnemyAI enemyAI, PlayerControllerB player);
}