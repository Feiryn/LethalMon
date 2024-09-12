﻿using GameNetcodeStuff;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LethalMon.Behaviours
{
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

        internal override string DefendingBehaviourDescription => "You can change the displayed text when the enemy is defending by something more precise... Or remove this line to use the default one";

        internal override bool CanDefend => attackCooldown == null || attackCooldown.IsFinished();

        private DepositItemsDesk? _depositItemsDesk = null;
        private InteractTrigger? _redeemItemsTrigger = null;
        private GameObject? _redeemItemsTriggerObject = null;
        #endregion

        #region Cooldowns
        private const string AttackCooldownId = "companymonster_attack";

        internal override Cooldown[] Cooldowns => [new Cooldown(AttackCooldownId, "Attack", 1f)]; // todo: change back to 10

        private CooldownNetworkBehaviour? attackCooldown;
        #endregion

        #region Custom behaviours
        internal enum CustomBehaviour
        {
            RunToCounter = 1
        }
        internal override List<Tuple<string, string, Action>>? CustomBehaviourHandler =>
        [
            new (CustomBehaviour.RunToCounter.ToString(), "Running to sell counter", OnRunToCounterBehavior)
        ];

        internal override void InitCustomBehaviour(int behaviour)
        {
            // ANY CLIENT
            base.InitCustomBehaviour(behaviour);

            switch ((CustomBehaviour)behaviour)
            {
                case CustomBehaviour.RunToCounter:
                    
                    if(IsOwnerPlayer)
                    {
                        if(_depositItemsDesk == null || !Utils.AtCompanyBuilding)
                        {
                            LethalMon.Log("No sell counter found. Returning back to following.", LethalMon.LogType.Warning);
                            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                            return;
                        }

                        MoveTowards(_depositItemsDesk.transform.position);
                    }
                    break;

                default:
                    break;
            }
        }

        internal void OnRunToCounterBehavior()
        {
            if (Vector3.Distance(_depositItemsDesk!.transform.position, CompanyMonster.transform.position) > 2f)
                return;

            CompanyMonster.RedeemItemsServerRpc();
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
        }
        #endregion

        #region Action Keys
        private readonly List<ActionKey> _actionKeys =
        [
            new ActionKey() { Key = ModConfig.Instance.ActionKey1, Description = "Attack" }
        ];
        internal override List<ActionKey> ActionKeys => _actionKeys;

        internal override void ActionKey1Pressed()
        {
            base.ActionKey1Pressed();

            if (CanDefend)
            {
                CompanyMonster.AttackServerRpc();

                if (CompanyMonster.mood?.wallAttackSFX != null && CompanyMonster.creatureSFX != null)
                    CompanyMonster.creatureSFX.PlayOneShot(CompanyMonster.mood.wallAttackSFX);

                attackCooldown?.Reset();
            }
        }
        #endregion

        #region Base Methods
        internal override void Start()
        {
            base.Start();

            attackCooldown = GetCooldownWithId(AttackCooldownId);

            if (IsTamed)
            {
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, IsOwnerPlayer);
                PlaceOnNavMesh();

                if (IsOwnerPlayer && Utils.AtCompanyBuilding)
                {
                    LethalMon.Log("Tamed Company Monster: Creating redeem trigger.");
                    _depositItemsDesk = FindObjectOfType<DepositItemsDesk>();
                    Utils.CreateInteractionForEnemy(CompanyMonster, "Redeem items", 2f, (player) => SwitchToCustomBehaviour((int)CustomBehaviour.RunToCounter), out _redeemItemsTrigger, out _redeemItemsTriggerObject);
                }
            }
        }

        void OnDestroy()
        {
            if (_redeemItemsTrigger != null)
                Destroy(_redeemItemsTrigger);

            if (_redeemItemsTriggerObject != null)
                Destroy(_redeemItemsTriggerObject);

            base.OnDestroy();
        }

        internal override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            base.OnUpdate(update, doAIInterval);

            CompanyMonster.Update();
        }

        internal override void DoAIInterval()
        {
            base.DoAIInterval();

            if(_depositItemsDesk != null)
                SetRedeemItemsTriggerVisible(CompanyMonster.caughtItems.Count > 0 && Vector3.Distance(_depositItemsDesk.transform.position, CompanyMonster.transform.position) < 5f);
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            // ANY CLIENT
            base.OnEscapedFromBall(playerWhoThrewBall);

            DestroyImmediate(gameObject); // remove CompanyMonsterAI after the event
        }
        #endregion

        #region Methods
        public void SetRedeemItemsTriggerVisible(bool visible = true)
        {
            if (_redeemItemsTrigger == null) return;

            _redeemItemsTrigger.holdInteraction = visible;
            _redeemItemsTrigger.isPlayingSpecialAnimation = !visible;
        }
        #endregion
    }
}
