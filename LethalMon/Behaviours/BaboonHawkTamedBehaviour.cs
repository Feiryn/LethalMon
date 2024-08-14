using GameNetcodeStuff;
using System.Collections.Generic;
using LethalMon.Items;
using Unity.Netcode;
using UnityEngine;
using System.Collections;
using LethalMon.CustomPasses;
using System.Linq;

namespace LethalMon.Behaviours
{
#if DEBUG
    internal class BaboonHawkTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        internal BaboonBirdAI? _baboonHawk = null;
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

        internal static AudioClip? echoLotSFX = null;

        internal static readonly float MaximumEchoDistance = 50f;
        internal static readonly float EchoKeepAlive = 3f; // Keep-alive once full distance is reached
        internal static readonly Color EchoLotColor = new Color(1f, 1f, 0f, 0.45f);
        #endregion

        #region Cooldowns
        private static readonly string CooldownId = "baboonhawk_echolot";
    
        internal override Cooldown[] Cooldowns => [new Cooldown(CooldownId, "Echo lot", 10f)];

        private CooldownNetworkBehaviour? echoLotCooldown;
        #endregion

        #region Action Keys
        private List<ActionKey> _actionKeys = new List<ActionKey>()
        {
            new ActionKey() { actionKey = ModConfig.Instance.ActionKey1, description = "Search for items" }
        };
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
            StartCoroutine(EchoLotColorAdjust());

            if (echoLotSFX != null)
                BaboonHawk.creatureSFX.PlayOneShot(echoLotSFX);

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

            LethalMon.Log("Start of echo lot.", LethalMon.LogType.Warning);
            yield return new WaitWhile(() =>
            {
                customPass.maxVisibilityDistance += Time.deltaTime * MaximumEchoDistance; // takes 1s
                LethalMon.Log("Echo lot distance: " + customPass.maxVisibilityDistance);
                return customPass.maxVisibilityDistance < MaximumEchoDistance;
            });

            LethalMon.Log("Echo lot reached max distance.", LethalMon.LogType.Warning);
            yield return new WaitForSeconds(EchoKeepAlive);
            LethalMon.Log("Echo lot reach end of lifetime.", LethalMon.LogType.Warning);

            yield return new WaitWhile(() =>
            {
                customPass.maxVisibilityDistance -= Time.deltaTime * MaximumEchoDistance * 2f; // takes half a sec
                LethalMon.Log("Echo lot distance: " + customPass.maxVisibilityDistance);
                return customPass.maxVisibilityDistance > 0f;
            });
            customPass.enabled = false;
            LethalMon.Log("Echo lot effect is over.", LethalMon.LogType.Warning);
        }

        internal static void LoadAudio(AssetBundle assetBundle)
        {
            echoLotSFX = assetBundle.LoadAsset<AudioClip>("Assets/Audio/BaboonHawk/EchoLot.ogg");
        }
        #endregion

        #region Base Methods
        internal override void Start()
        {
            base.Start();

            echoLotCooldown = GetCooldownWithId(CooldownId);

#if DEBUG
            ownerPlayer = Utils.AllPlayers.Where((p) => p.playerClientId == 0ul).First();
            ownClientId = 0ul;
#endif

            if (ownerPlayer != null)
                BaboonHawk.transform.localScale = Vector3.one * 0.6f;
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            // ANY CLIENT
            base.OnEscapedFromBall(playerWhoThrewBall);
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
