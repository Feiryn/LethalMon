using GameNetcodeStuff;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using System.Collections;
using LethalMon.CustomPasses;

namespace LethalMon.Behaviours
{
#if DEBUG
    internal class BaboonHawkTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        private BaboonBirdAI? _baboonHawk = null;
        internal BaboonBirdAI BaboonHawk
        {
            get
            {
                if (_baboonHawk == null)
                    _baboonHawk = (Enemy as BaboonBirdAI)!;

                return _baboonHawk;
            }
        }

        //internal override string DefendingBehaviourDescription => "Y";

        internal override bool CanDefend => false;
        internal override float TargetingRange => 5f;

        internal static AudioClip? EchoLotSFX = null;

        internal const float MaximumEchoDistance = 50f;
        internal const float EchoKeepAlive = 3f; // Keep-alive once full distance is reached
        internal static readonly Color EchoLotColor = new(1f, 1f, 0f, 0.45f);

        private float _idleTimer = 0f;
        #endregion

        #region Cooldowns
        private const string EchoLotCooldownId = "baboonhawk_echolot";
        private const string HittingEnemyCooldownId = "baboonhawk_hittingenemy";

        internal override Cooldown[] Cooldowns => [
            new Cooldown(EchoLotCooldownId, "Echo lot", 15f),
            new Cooldown(HittingEnemyCooldownId, "Hitting enemy", 5f)
            ];

        private CooldownNetworkBehaviour? echoLotCooldown;
        private CooldownNetworkBehaviour? hittingEnemyCooldown;
        #endregion

        #region Action Keys
        private readonly List<ActionKey> _actionKeys =
        [
            new ActionKey() { Key = ModConfig.Instance.ActionKey1, Description = "Search for items" }
        ];
        internal override List<ActionKey> ActionKeys => _actionKeys;

        internal override void ActionKey1Pressed()
        {
            base.ActionKey1Pressed();
            if (echoLotCooldown != null && echoLotCooldown.IsFinished())
                StartEchoLotServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        internal void StartEchoLotServerRpc()
        {
            StartEchoLotClientRpc();
        }

        [ClientRpc]
        internal void StartEchoLotClientRpc()
        {
            echoLotCooldown?.Reset();

            HUDManager.Instance.scanEffectAnimator.transform.position = BaboonHawk.transform.position;
            HUDManager.Instance.scanEffectAnimator.SetTrigger("scan");
            ownerPlayer?.StartCoroutine(EchoLotColorAdjust());

            if (EchoLotSFX != null)
                BaboonHawk.creatureSFX.PlayOneShot(EchoLotSFX);

            ownerPlayer?.StartCoroutine(EchoLotScanCoroutine()); // Putting it on the ownerPlayer just in case they retreive the enemy during the process
        }

        internal IEnumerator EchoLotColorAdjust()
        {
            var scanMaterial = HUDManager.Instance.scanEffectAnimator.GetComponent<MeshRenderer>()?.material;
            if (scanMaterial == null) yield break;

            var originalColor = scanMaterial.color;
            scanMaterial.color = EchoLotColor;
            yield return new WaitForSeconds(1f);
            scanMaterial.color = originalColor;
        }

        internal IEnumerator EchoLotScanCoroutine()
        {
            var customPass = CustomPassManager.Instance.EnableCustomPass(CustomPassManager.CustomPassType.SeeThroughItems, true) as SeeThroughCustomPass;
            if (customPass == null) yield break;

            customPass.maxVisibilityDistance = 0f;

            yield return new WaitWhile(() =>
            {
                customPass.maxVisibilityDistance += Time.deltaTime * MaximumEchoDistance; // takes 1s
                return customPass.maxVisibilityDistance < MaximumEchoDistance;
            });

            yield return new WaitForSeconds(EchoKeepAlive);

            yield return new WaitWhile(() =>
            {
                customPass.maxVisibilityDistance -= Time.deltaTime * MaximumEchoDistance * 2f; // takes half a sec
                return customPass.maxVisibilityDistance > 0f;
            });
            customPass.enabled = false;
        }

        internal static void LoadAudio(AssetBundle assetBundle)
        {
            EchoLotSFX = assetBundle.LoadAsset<AudioClip>("Assets/Audio/BaboonHawk/EchoLot.ogg");
        }
        #endregion

        #region Base Methods
        internal override void Start()
        {
            SetTamedByHost_DEBUG(); // DEBUG

            base.Start();

            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing); // DEBUG

            echoLotCooldown = GetCooldownWithId(EchoLotCooldownId);
            hittingEnemyCooldown = GetCooldownWithId(HittingEnemyCooldownId);

            if (IsTamed)
            {
                BaboonHawk.transform.localScale = Vector3.one * 0.75f;
            }

            if(BaboonHawk.agent != null)
                BaboonHawk.agent.angularSpeed = 0f;
        }

        internal override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            base.OnUpdate(update, false);

            BaboonHawk.CalculateAnimationDirection();

            if (BaboonHawk.miscAnimationTimer > 0f)
                BaboonHawk.miscAnimationTimer -= Time.deltaTime;

            if (BaboonHawk.headLookRig.weight < 1f)
                BaboonHawk.headLookRig.weight = Mathf.Lerp(BaboonHawk.headLookRig.weight, 1f, Time.deltaTime * 10f);
        }

        internal override void TurnTowardsPosition(Vector3 position)
        {
            if(IsSleeping) return;

            base.TurnTowardsPosition(position);

            BaboonHawk.AnimateLooking(position);
        }

        internal override void InitTamingBehaviour(TamingBehaviour behaviour)
        {
            base.InitTamingBehaviour(behaviour);

            switch(behaviour)
            {
                case TamingBehaviour.TamedFollowing:
                    SetAggressive(false);
                    SetFighting(false);
                    break;
                case TamingBehaviour.TamedDefending:
                    if (targetEnemy == null) return;
                    
                    MoveTowards(targetEnemy.transform.position);

                    SetAggressive(true);
                    SetFighting(true);
                    _idleTimer = 0f;
                    BaboonHawk.aggressionAudio.clip = BaboonHawk.enemyType.audioClips[2];
                    BaboonHawk.aggressionAudio.Play();
                    BaboonHawk.aggressionAudio.volume = 0f;
                    if (Utils.IsHost)
                        BaboonHawk.StartMiscAnimationServerRpc(0);
                    BaboonHawk.timeSinceHitting = 0f; // Skips the normal OnCollideWithEnemy
                    break;
                default: break;
            }
        }

        internal override void OnTamedFollowing()
        {
            base.OnTamedFollowing();

            // Aduio
            if (BaboonHawk.aggressionAudio.volume > 0f)
                BaboonHawk.aggressionAudio.volume = Mathf.Max(BaboonHawk.aggressionAudio.volume + Time.deltaTime * 5f, 0f);
            else if (BaboonHawk.aggressionAudio.isPlaying)
                BaboonHawk.aggressionAudio.Stop();

            // Animations
            if (BaboonHawk.agentLocalVelocity.x == 0f && BaboonHawk.agentLocalVelocity.z == 0f) // Idle
                _idleTimer += Time.deltaTime;

            SetSitting(_idleTimer > 1f && _idleTimer <= 6f);
            SetSleeping(_idleTimer > 6f);

            // Targeting
            TargetNearestEnemy();
        }

        internal override void OnTamedDefending()
        {
            base.OnTamedDefending();

            if (!HasTargetEnemy || targetEnemy!.isEnemyDead || DistanceToOwner > 10f)
            {
                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                return;
            }

            if (BaboonHawk.aggressionAudio.volume < 1f)
                BaboonHawk.aggressionAudio.volume = Mathf.Min(BaboonHawk.aggressionAudio.volume + Time.deltaTime * 4f, 1f);

            if (!IsCollidingWithTargetEnemy)
            {
                BaboonHawk.SetDestinationToPosition(targetEnemy.transform.position);
                if (IsOwner)
                    BaboonHawk.agent.speed = hittingEnemyCooldown != null ? Mathf.Clamp(hittingEnemyCooldown.CurrentTimer * 2f, 1f, 5f) : 4f; // Slow down after hit
                return;
            }

            if(hittingEnemyCooldown != null && hittingEnemyCooldown.IsFinished())
            {
                LethalMon.Log("BaboonHawk hitting target");
                hittingEnemyCooldown.Reset();

                BaboonHawk.creatureAnimator.ResetTrigger("Hit");
                BaboonHawk.creatureAnimator.SetTrigger("Hit");
                BaboonHawk.creatureSFX.PlayOneShot(BaboonHawk.enemyType.audioClips[5]);
                WalkieTalkie.TransmitOneShotAudio(BaboonHawk.creatureSFX, BaboonHawk.enemyType.audioClips[5]);
                RoundManager.Instance.PlayAudibleNoise(BaboonHawk.creatureSFX.transform.position, 8f, 0.7f);
                targetEnemy.HitEnemy(1, null, playHitSFX: true);
                return;
            }

            if (BaboonHawk.miscAnimationTimer <= 0f && hittingEnemyCooldown != null && hittingEnemyCooldown.CurrentTimer > 2f)
                BaboonHawk.StartMiscAnimationServerRpc(Random.RandomRangeInt(0, BaboonHawk.enemyType.miscAnimations.Length - 1));
        }

        internal override bool EnemyMeetsTargetingConditions(EnemyAI enemyAI)
        {
            if (!enemyAI.enemyType.canDie) return false; // pointless to attack

            return base.EnemyMeetsTargetingConditions(enemyAI);
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            // ANY CLIENT
            base.OnEscapedFromBall(playerWhoThrewBall);

            if (Utils.IsHost)
            {
                var tinyHawk = Utils.SpawnEnemyAtPosition(Utils.Enemy.BaboonHawk, BaboonHawk.transform.position) as BaboonBirdAI;
                if (tinyHawk != null)
                {
                    tinyHawk.transform.localScale = Vector3.one * 0.3f;
                    tinyHawk.creatureVoice.pitch = 1.5f;
                    tinyHawk.creatureVoice.volume = 0.5f;
                    tinyHawk.targetPlayer = playerWhoThrewBall;

                    if (BaboonHawk.scoutingGroup == null)
                        BaboonHawk.StartScoutingGroup(tinyHawk, true);
                    else
                        tinyHawk.JoinScoutingGroup(BaboonHawk);

                    if (tinyHawk.TryGetComponent(out BaboonHawkTamedBehaviour tinyHawkbehaviour))
                        tinyHawkbehaviour.StartFocusOnPlayer(playerWhoThrewBall);
                }
                StartFocusOnPlayer(playerWhoThrewBall);
            }
        }

        internal override void OnCallFromBall()
        {
            base.OnCallFromBall();

            if(IsOwnerPlayer)
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true);
        }
        #endregion

        #region Methods
        internal bool IsSleeping => BaboonHawk.creatureAnimator != null && BaboonHawk.creatureAnimator.GetBool("sleep");
        internal void SetSleeping(bool sleeping = true) => BaboonHawk.creatureAnimator?.SetBool("sleep", sleeping);
        internal bool IsSitting => BaboonHawk.creatureAnimator != null && BaboonHawk.creatureAnimator.GetBool("sitting");
        internal void SetSitting(bool sitting = true) => BaboonHawk.creatureAnimator?.SetBool("sit", sitting);
        internal bool IsFighting => BaboonHawk.creatureAnimator != null && BaboonHawk.creatureAnimator.GetBool("fighting");
        internal void SetFighting(bool fighting = true) => BaboonHawk.creatureAnimator?.SetBool("fighting", fighting);
        internal bool IsAggressive => BaboonHawk.creatureAnimator != null && BaboonHawk.creatureAnimator.GetBool("aggressiveDisplay");
        internal void SetAggressive(bool aggressive = true) => BaboonHawk.creatureAnimator?.SetBool("aggressiveDisplay", aggressive);
        
        public void StartFocusOnPlayer(PlayerControllerB focussedPlayer)
        {
            BaboonHawk.fightTimer = 0f;
            BaboonHawk.focusingOnThreat = true;
            BaboonHawk.StartFocusOnThreatServerRpc(focussedPlayer.NetworkObject);
            BaboonHawk.focusedThreat = MakePlayerAThreat(focussedPlayer);
            BaboonHawk.focusedThreatTransform = focussedPlayer.transform;
        }

        public Threat MakePlayerAThreat(PlayerControllerB player)
        {
            if (BaboonHawk.threats.TryGetValue(player.transform, out Threat threat))
            { // Already a threat
                threat.threatLevel = 0;
                threat.interestLevel = 99;
                threat.hasAttacked = true;
                LethalMon.Log("Made player a higher target");
                return threat;
            }

            threat = new Threat();
            if (player.TryGetComponent<IVisibleThreat>(out var visibleThreat))
            {
                threat.type = visibleThreat.type;
                threat.threatScript = visibleThreat;
            }
            threat.timeLastSeen = Time.realtimeSinceStartup;
            threat.lastSeenPosition = player.transform.position + Vector3.up * 0.5f;
            threat.distanceToThreat = Vector3.Distance(player.transform.position, BaboonHawk.transform.position);
            threat.distanceMovedTowardsBaboon = 0f;
            threat.threatLevel = 0;
            threat.interestLevel = 99;
            threat.hasAttacked = true;
            if (BaboonHawk.threats.TryAdd(player.transform, threat))
                LethalMon.Log("Added player as threat");
            return threat;
        }
        #endregion
    }
#endif
}
