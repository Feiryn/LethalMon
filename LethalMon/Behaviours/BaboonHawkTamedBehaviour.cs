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

        internal static AudioClip? EchoLotSFX = null;

        internal const float MaximumEchoDistance = 50f;
        internal const float EchoKeepAlive = 3f; // Keep-alive once full distance is reached
        internal static readonly Color EchoLotColor = new Color(1f, 1f, 0f, 0.45f);
        #endregion

        #region Cooldowns
        private const string CooldownId = "baboonhawk_echolot";
    
        internal override Cooldown[] Cooldowns => [new Cooldown(CooldownId, "Echo lot", 10f)];

        private CooldownNetworkBehaviour echoLotCooldown;
        #endregion

        BaboonHawkTamedBehaviour() => echoLotCooldown = GetCooldownWithId(CooldownId);

        #region Action Keys
        private readonly List<ActionKey> _actionKeys =
        [
            new ActionKey() { actionKey = ModConfig.Instance.ActionKey1, description = "Search for items" }
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
            echoLotCooldown.Reset();

            HUDManager.Instance.scanEffectAnimator.transform.position = BaboonHawk.transform.position;
            HUDManager.Instance.scanEffectAnimator.SetTrigger("scan");
            StartCoroutine(EchoLotColorAdjust());

            if (EchoLotSFX != null)
                BaboonHawk.creatureSFX.PlayOneShot(EchoLotSFX);

            StartCoroutine(EchoLotScanCoroutine());
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
            base.Start();

#if DEBUG
            SetTamedByHost_DEBUG();
#endif

            if (ownerPlayer != null)
                BaboonHawk.transform.localScale = Vector3.one * 0.75f;
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

        internal override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            // ANY CLIENT
            base.OnUpdate(update, doAIInterval);

            BaboonHawk.CalculateAnimationDirection(2f);
        }

        internal override void OnCallFromBall()
        {
            base.OnCallFromBall();

            if(IsOwnerPlayer)
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true);
        }
        #endregion
    }
#endif
}
