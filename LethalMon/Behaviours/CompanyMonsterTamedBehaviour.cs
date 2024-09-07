using GameNetcodeStuff;
using System.Collections.Generic;
using UnityEngine;

namespace LethalMon.Behaviours
{
#if DEBUG
    internal class CompanyMonsterTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        private CompanyMonsterAI? _companyMonster = null; // Replace with enemy class
        internal CompanyMonsterAI CompanyMonster
        {
            get
            {
                if (_companyMonster == null)
                    _companyMonster = (Enemy as CompanyMonsterAI)!;

                return _companyMonster;
            }
        }

        Vector3 _startPosition = Vector3.zero;

        internal override string DefendingBehaviourDescription => "You can change the displayed text when the enemy is defending by something more precise... Or remove this line to use the default one";

        internal override bool CanDefend => attackCooldown == null || attackCooldown.IsFinished();
        #endregion

        #region Cooldowns
        private const string AttackCooldownId = "companymonster_attack";
    
        internal override Cooldown[] Cooldowns => [new Cooldown(AttackCooldownId, "Attack", 1f)]; // todo: change back to 10

        private CooldownNetworkBehaviour? attackCooldown;
        #endregion

        #region Action Keys
        private readonly List<ActionKey> _actionKeys =
        [
            new ActionKey() { Key = ModConfig.Instance.ActionKey1, Description = "Action description here" }
        ];
        internal override List<ActionKey> ActionKeys => _actionKeys;

        internal override void ActionKey1Pressed()
        {
            base.ActionKey1Pressed();

            if (CanDefend)
            {
                if (CompanyMonster.tentaclePrefab == null)
                    LethalMon.Log("No tentacles..", LethalMon.LogType.Warning);

                CompanyMonster.tentaclePrefab?.SetActive(true);
                CompanyMonster.monsterAnimator?.SetBool("visible", value: true);
                //CompanyMonster.monsterAnimator?.Play("Base Layer.Tentacle1Explore");

                if(CompanyMonster.mood?.wallAttackSFX != null && CompanyMonster.TryGetComponent(out AudioSource audioSource))
                    audioSource.PlayOneShot(CompanyMonster.mood.wallAttackSFX);

                attackCooldown?.Reset();
            }
        }
        #endregion

        #region Base Methods
        internal override void Start()
        {
            base.Start();

            attackCooldown = GetCooldownWithId(AttackCooldownId);
            _startPosition = CompanyMonster.transform.position;

            if (IsTamed)
            {
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, IsOwnerPlayer);
            }
        }

        internal override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            base.OnUpdate(update, doAIInterval);

            CompanyMonster.Update();
            
            CompanyMonster.transform.position = _startPosition; // debug!
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            // ANY CLIENT
            base.OnEscapedFromBall(playerWhoThrewBall);

            DestroyImmediate(gameObject); // remove CompanyMonsterAI after the event
        }
        #endregion
    }
#endif
}
