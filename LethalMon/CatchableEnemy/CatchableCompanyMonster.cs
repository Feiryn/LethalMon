using GameNetcodeStuff;
using LethalMon.Behaviours;

namespace LethalMon.CatchableEnemy;

public class CatchableCompanyMonster : CatchableEnemy
{
    public CatchableCompanyMonster() : base(14, "Jeb (Company Monster)", 4)
    {
    }

    public override bool CanBeCapturedBy(EnemyAI enemyAI, PlayerControllerB player)
    {
        var companyMonsterAI = enemyAI as CompanyMonsterAI;
        return companyMonsterAI?.deskInside != null && companyMonsterAI.deskInside.attacking;
    }
}