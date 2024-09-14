using GameNetcodeStuff;
using LethalMon.Patches;
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
            RunToCounter = 1,
            RunTowardsItem,
            EatItem
        }
        internal override List<Tuple<string, string, Action>>? CustomBehaviourHandler =>
        [
            new (CustomBehaviour.RunToCounter.ToString(), "Running to sell counter", OnRunToCounterBehavior),
            new (CustomBehaviour.RunTowardsItem.ToString(), "Running towards an item", OnRunTowardsItemBehavior),
            new (CustomBehaviour.EatItem.ToString(), "Eats an item", OnEatItemBehavior)
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
                            LethalMon.Log("CompanyMonster: No sell counter found. Returning back to following.", LethalMon.LogType.Warning);
                            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                            return;
                        }

                        MoveTowards(_depositItemsDesk.transform.position);
                    }

                    break;

                case CustomBehaviour.RunTowardsItem:
                    if (IsOwnerPlayer && targetItem != null)
                        MoveTowards(targetItem.transform.position);

                    break;

                case CustomBehaviour.EatItem:
                    if (CompanyMonster.IsOwner)
                    {
                        if (targetItem == null)
                            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                        else
                            CompanyMonster.ReachOutForItemServerRpc(targetItem.NetworkObject);
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

        internal void OnRunTowardsItemBehavior()
        {
            if(targetItem == null || targetItem.isHeld || targetItem.isHeldByEnemy)
            {
                if(targetItem == null)
                    LethalMon.Log("CompanyMonster: Target item disappeared. Returning back to following.", LethalMon.LogType.Warning);
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                return;
            }

            TargetNearestEnemy();

            if (DistanceToTargetItem < 2f)
                SwitchToCustomBehaviour((int)CustomBehaviour.EatItem);
        }

        internal void OnEatItemBehavior()
        {
            if (targetItem == null || targetItem.isHeld || targetItem.isHeldByEnemy || CompanyMonster.hasEatenItem)
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
        }
        #endregion

        #region Action Keys
        private readonly List<ActionKey> _actionKeys =
        [
            new ActionKey() { Key = ModConfig.Instance.ActionKey1, Description = "Attack" }
        ];
        internal override List<ActionKey> ActionKeys => _actionKeys;

        static GameObject? containerCube = null, tentacleCube = null;
        internal override void ActionKey1Pressed()
        {
            base.ActionKey1Pressed();

            /*CompanyMonster.caughtEnemies = new Dictionary<string, int>
            {
                { Utils.Enemy.Crawler.ToString(), 3 },
                { Utils.Enemy.Centipede.ToString(), 2 },
                { Utils.Enemy.SpringMan.ToString(), 1 }
            };
            CompanyMonster.SpawnCaughtEnemiesOnServer();*/

            /*if (containerCube == null)
            {
                containerCube = DebugPatches.CreateCube(Color.red, CompanyMonster.tentacleContainer!.transform.position);
                containerCube.transform.SetParent(CompanyMonster.tentacleContainer!.transform, true);
            }

            if(tentacleCube == null)
            {
                tentacleCube = DebugPatches.CreateCube(Color.green, CompanyMonster.tentacles[0].transform.position);
                tentacleCube.transform.SetParent(CompanyMonster.tentacles[0].transform.parent, true);
            }

            foreach (var tentacle in CompanyMonster.tentacles)
            {
                tentacle.SetActive(true);
                tentacle.transform.position = CompanyMonster.tentacleContainer!.transform.position - CompanyMonster.eye.transform.forward;
                tentacle.transform.localScale = Vector3.one;
            }

            CompanyMonster.RandomizeTentacleRotations();*/

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

        internal override void OnTamedFollowing()
        {
            base.OnTamedFollowing();

            if (!TargetNearestEnemy())
                TargetNearestItem();
        }

        internal override void OnFoundTarget()
        {
            if (targetEnemy != null)
                SwitchToTamingBehaviour(TamingBehaviour.TamedDefending);
            else if (targetItem != null)
                SwitchToCustomBehaviour((int)CustomBehaviour.RunTowardsItem);
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
