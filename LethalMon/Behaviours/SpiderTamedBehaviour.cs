using GameNetcodeStuff;
using System.Collections.Generic;
using System;
using LethalMon.Items;
using UnityEngine;
using System.Linq;
using Unity.Netcode;
using System.Collections;
using Vector3 = UnityEngine.Vector3;

namespace LethalMon.Behaviours
{
    internal class SpiderTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        private SandSpiderAI? _spider = null; // Replace with enemy class
        internal SandSpiderAI Spider
        {
            get
            {
                if (_spider == null)
                    _spider = (Enemy as SandSpiderAI)!;

                return _spider;
            }
        }

        public static readonly float SpiderBounceForce = 1.5f;
        internal static bool IsWebJumping = false;
        internal float timeOfLastWebJump = 0f;
        private float? _defaultJumpForce = null;
        private Coroutine? _webJumpCoroutine = null;

        private SandSpiderWebTrap? _previousWebTrap = null;

        internal override bool CanDefend => shootWebCooldown != null && shootWebCooldown.IsFinished();

        // Audio
        internal static AudioClip[] webBounceSFX = [];
        #endregion

        #region Cooldowns
        private const string ShootWebCooldownID = "monstername_cooldownname";

        internal override Cooldown[] Cooldowns => [new Cooldown(ShootWebCooldownID, "Shooting web", ModConfig.Instance.values.SpiderWebCooldown)];

        private CooldownNetworkBehaviour? shootWebCooldown;
        #endregion

        #region Action Keys
        private readonly List<ActionKey> _actionKeys =
        [
            new ActionKey() { Key = ModConfig.Instance.ActionKey1, Description = "Shoot web" }
        ];
        internal override List<ActionKey> ActionKeys => _actionKeys;

        internal override void ActionKey1Pressed()
        {
            base.ActionKey1Pressed();

            if (ownerPlayer != null && CanDefend)
            {
                var basePos = ownerPlayer.transform.position + Vector3.up * 1.5f + ownerPlayer.transform.forward * 3f;
                ShootWeb(basePos, basePos + ownerPlayer.transform.forward);
            }
        }
        #endregion

        #region Base Methods
        internal override void Start()
        {
            base.Start();

            shootWebCooldown = GetCooldownWithId(ShootWebCooldownID);

            if (IsTamed)
            {
                Spider.transform.localScale = Vector3.one * 0.7f;
                Spider.footstepAudio.volume = 0.1f;
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, IsOwnerPlayer);
            }

        }

        internal override void InitTamingBehaviour(TamingBehaviour behaviour)
        {
            // ANY CLIENT
            base.InitTamingBehaviour(behaviour);

            switch (behaviour)
            {
                case TamingBehaviour.TamedFollowing:
                    break;

                case TamingBehaviour.TamedDefending:
                    Vector3? targetPosition = null;
                    if (targetEnemy != null && !targetEnemy.isEnemyDead)
                        targetPosition = targetEnemy.transform.position;
                    else if (targetPlayer != null && !targetPlayer.isPlayerDead)
                        targetPosition = targetPlayer.transform.position;

                    if (targetPosition.HasValue && Spider.IsOwner)
                    {
                        LethalMon.Log("Spider: Shoot web on target!", LethalMon.LogType.Warning);
                        ShootWebAt(targetPosition.Value);
                    }

                    SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
                    break;

                default: break;
            }
        }

        internal override void OnTamedFollowing()
        {
            base.OnTamedFollowing();

            if(CanDefend)
            {
                if (ownerPlayer!.fallValueUncapped < -30f)
                {
                    if (Physics.Raycast(new Ray(ownerPlayer.transform.position, Vector3.down), 3f, StartOfRound.Instance.allPlayersCollideWithMask, QueryTriggerInteraction.Ignore))
                    {
                        var baseWebPos = ownerPlayer.transform.position + Vector3.down * 2f;
                        ShootWeb(baseWebPos + Vector3.left, baseWebPos + Vector3.right, 1); // Single-use web
                    }
                }
                else
                    TargetNearestEnemy();
            }
        }

        public override void MoveTowards(Vector3 position)
        {
            base.MoveTowards(position);

            Spider.navigateToPositionTarget = position;
        }

        internal override void TurnTowardsPosition(Vector3 position)
        {
            base.TurnTowardsPosition(position);

            Spider.SetSpiderLookAtPosition(position);
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            // ANY CLIENT
            base.OnEscapedFromBall(playerWhoThrewBall);

            ShootWebsAround();
        }

        internal override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            // ANY CLIENT
            base.OnUpdate(update, doAIInterval);

            Spider.Update();
        }

        internal override void LateUpdate()
        {
            base.LateUpdate();

            Spider.LateUpdate();
        }

        internal override void DoAIInterval()
        {
            // ANY CLIENT, every EnemyAI.updateDestinationInterval, if OnUpdate.doAIInterval = true
            base.DoAIInterval();

            if (Spider.navigateMeshTowardsPosition)
                Spider.CalculateSpiderPathToPosition();
        }

        public override PokeballItem? RetrieveInBall(Vector3 position)
        {
            for(int i = Spider.webTraps.Count - 1; i >= 0; --i)
                Spider.BreakWebClientRpc(Spider.webTraps[i].transform.position, i);

            return base.RetrieveInBall(position);
        }
        #endregion

        #region Methods
        internal static void LoadAudio(AssetBundle assetBundle)
        {
            var webBounceAudioClips = new List<AudioClip>();
            for(int i = 1; i <= 3; ++i)
            {
                var audioClip = assetBundle.LoadAsset<AudioClip>($"Assets/Audio/Spider/webBounce{i}.ogg");
                if (audioClip != null)
                    webBounceAudioClips.Add(audioClip);
            }

            webBounceSFX = webBounceAudioClips.ToArray();
        }
        #endregion

        #region WebJumping
        internal void JumpOnWebLocalClient(int webTrapID)
        {
            if (_webJumpCoroutine != null)
                StopCoroutine(_webJumpCoroutine);
            _webJumpCoroutine = StartCoroutine(PerformWebJump());

            JumpedOnWebServerRpc(webTrapID, (int)Utils.CurrentPlayerID!);
        }

        [ServerRpc(RequireOwnership = false)]
        internal void JumpedOnWebServerRpc(int webTrapID, int playerID) => JumpedOnWebClientRpc(webTrapID, playerID);

        [ClientRpc]
        internal void JumpedOnWebClientRpc(int webTrapID, int playerID)
        {
            if (Spider.webTraps.Count <= webTrapID)
            {
                LethalMon.Log("Unable to find web trap for bouncing.", LethalMon.LogType.Error);
                return;
            }

            var webTrap = Spider.webTraps[webTrapID];
            if (webBounceSFX.Length > 0)
                Utils.PlaySoundAtPosition(webTrap.transform.position, webBounceSFX[UnityEngine.Random.RandomRangeInt(0, webBounceSFX.Length - 1)]);

            StartCoroutine(WebBending(webTrap.gameObject));

            if(webTrap.TryGetComponent(out TamedWebBehaviour webBehaviour))
            {
                webBehaviour.playerUses--;
                if (webBehaviour.playerUses == 0)
                    Spider.BreakWebServerRpc(webTrapID, playerID);
            }
        }

        internal IEnumerator PerformWebJump()
        {
            var player = Utils.CurrentPlayer;

            if (_defaultJumpForce == null || !IsWebJumping)
                _defaultJumpForce = player.jumpForce;

            if (player.jumpCoroutine != null)
                player.StopCoroutine(player.jumpCoroutine);
            player.jumpCoroutine = player.StartCoroutine(player.PlayerJump());

            player.playerSlidingTimer = 0f;
            player.isJumping = true;
            player.sprintMeter = Mathf.Clamp(player.sprintMeter - 0.08f, 0f, 1f);

            IsWebJumping = true;
            timeOfLastWebJump = Time.realtimeSinceStartup;

            var modifiedJumpForce = _defaultJumpForce.Value * SpiderBounceForce;

            const string Jumping = "Jumping";
            yield return new WaitUntil(() =>
            {
                player.jumpForce = modifiedJumpForce;
                return (Time.realtimeSinceStartup - timeOfLastWebJump > 1f) && !player.playerBodyAnimator.GetBool(Jumping);
            });

            player.jumpForce = _defaultJumpForce.Value;
            IsWebJumping = false;
        }


        static readonly AnimationCurve WebBendingCurve = new(
            new Keyframe(0, 1f),
            new Keyframe(0.05f, 0.4f),
            new Keyframe(0.2f, 1.3f),
            new Keyframe(0.3f, 0.65f),
            new Keyframe(0.4f, 1.2f),
            new Keyframe(0.6f, 0.9f),
            new Keyframe(0.8f, 1.05f),
            new Keyframe(1f, 1f)
        );

        internal IEnumerator WebBending(GameObject webObject)
        {
            var initialScale = webObject.transform.localScale;
            float duration = 1f;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                if (webObject == null) yield break;

                float scaleMultiplier = WebBendingCurve.Evaluate(elapsedTime / duration);

                webObject.transform.localScale = initialScale * scaleMultiplier;

                elapsedTime += Time.deltaTime;
                yield return null;
            }
        }
        #endregion

        #region Webshooting
        internal void ShootWebsAround(int amount = 10) // Shoot webs in any direction
        {
            for (int i = 0; i < amount; i++)
            {
                Vector3 direction = Vector3.Scale(UnityEngine.Random.onUnitSphere, new Vector3(1f, UnityEngine.Random.Range(0.2f, 1f), 1f));
                direction.y = Mathf.Max(direction.y, 0f);
                ShootWebInDirection(direction);
            }
        }

        internal void ShootWebInDirection(Vector3 direction) // Inspired by SandSpiderAI.AttemptPlaceWebTrap()
        {
            var ray = new Ray(Spider.abdomen.position + Vector3.up * 0.4f, direction);
            if (Physics.Raycast(ray, out RaycastHit rayHit, 7f, StartOfRound.Instance.collidersAndRoomMask))
            {
                if (rayHit.distance < 2f)
                    return;

                Vector3 point = rayHit.point;
                if (Physics.Raycast(Spider.abdomen.position, Vector3.down, out rayHit, 10f, StartOfRound.Instance.collidersAndRoomMask))
                {
                    Vector3 startPosition = rayHit.point + Vector3.up * 0.2f;
                    ShootWeb(startPosition, point);
                }
            }
        }

        internal void ShootWebAt(Vector3 targetPosition, float size = 3f) => ShootWeb(targetPosition - (targetPosition - ownerPlayer!.transform.position).normalized * 2f * size + Vector3.up, targetPosition + Vector3.up);

        internal void ShootWeb(Vector3 from, Vector3 to, int playerUses = 10)
        {
            if(!Spider.IsOwner)
            {
                ShootWebServerRpc(from, to, playerUses);
                return;
            }

            shootWebCooldown?.Reset();

            if (!Spider.IsOwner) return;

            if (_previousWebTrap != null)
                Spider.BreakWebServerRpc(_previousWebTrap.trapID, 0);
            Spider.SpawnWebTrapServerRpc(from, to);
            _previousWebTrap = Spider.webTraps.Last();
            if (IsTamed)
            {
                var webBehaviour = Spider.webTraps.Last().gameObject.AddComponent<TamedWebBehaviour>();
                webBehaviour.spawnedBy = this;
                webBehaviour.playerUses = playerUses;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        internal void ShootWebServerRpc(Vector3 from, Vector3 to, int playerUses = 10) => ShootWebClientRpc(from, to, playerUses);

        [ClientRpc]
        internal void ShootWebClientRpc(Vector3 from, Vector3 to, int playerUses)
        {
            if(Spider.IsOwner)
                ShootWeb(from, to, playerUses);
        }

        [ServerRpc(RequireOwnership = false)]
        internal void BreakWebServerRpc(int trapID) // avoids chase
        {
            if (trapID > Spider.webTraps.Count - 1) return; // no such web

            Vector3 position = Spider.webTraps[trapID].centerOfWeb.position;
            Spider.BreakWebClientRpc(position, trapID);
        }
        #endregion

        #region WebBehaviour
        internal class TamedWebBehaviour : MonoBehaviour
        {
            private const float SpeedDivider = 20f;

            internal float webDuration = 5f; // Time how long the web can handle enemies
            internal int playerUses = 10;

            private struct EnemyInfo(EnemyAI enemyAI, float enterAgentSpeed, float enterAnimationSpeed)
            {
                internal EnemyAI enemyAI { get; set; } = enemyAI;
                internal float enterAgentSpeed { get; set; } = enterAgentSpeed;
                internal float enterAnimationSpeed { get; set; } = enterAnimationSpeed;
            }

            internal SpiderTamedBehaviour? spawnedBy = null;
            internal SandSpiderWebTrap? webTrap = null;

            private Dictionary<int, int> _touchingEnemyParts = new Dictionary<int, int>();          // collider InstanceID - enemy InstanceID
            private Dictionary<int, EnemyInfo> _touchingEnemies = new Dictionary<int, EnemyInfo>(); // enemy InstanceID - info

            private HashSet<int> _nonEnemyCollider = new HashSet<int>();

            void Update()
            {
                if (webTrap == null)
                    webTrap = gameObject.GetComponent<SandSpiderWebTrap>();

                if (spawnedBy == null || webTrap == null)
                {
                    Destroy(this);
                    return;
                }
            }

            void OnTriggerEnter(Collider other)
            {
                if (_nonEnemyCollider.Contains(other.GetInstanceID()) || _touchingEnemyParts.ContainsKey(other.GetInstanceID())) return; // save performance

                var enemyAI = other.GetComponentInParent<EnemyAI>();
                if (enemyAI == null || enemyAI == spawnedBy?.Spider)
                {
                    _nonEnemyCollider.Add(other.GetInstanceID());
                    return;
                }

                _touchingEnemyParts[other.GetInstanceID()] = enemyAI.GetInstanceID();

                if (!_touchingEnemies.ContainsKey(enemyAI.GetInstanceID()))
                {
                    LethalMon.Log($"Enemy {enemyAI.name} entered the web. IDs: " + other.GetInstanceID() + " / " + enemyAI.GetInstanceID(), LethalMon.LogType.Warning);
                    _touchingEnemies.Add(enemyAI.GetInstanceID(), new EnemyInfo(enemyAI, enemyAI.agent.speed, enemyAI.creatureAnimator.speed));
                    if (webTrap != null && spawnedBy?.Spider != null)
                    {
                        webTrap.webAudio.Play();
                        webTrap.webAudio.PlayOneShot(spawnedBy.Spider.hitWebSFX);
                    }
                }
            }

            void LateUpdate()
            {
                foreach(var enemyInfo in _touchingEnemies.Values)
                {
                    LethalMon.Log($"Enemy {enemyInfo.enemyAI.name} is inside the web.");
                    enemyInfo.enemyAI.agent.speed = enemyInfo.enterAgentSpeed / SpeedDivider;
                    enemyInfo.enemyAI.creatureAnimator.speed = enemyInfo.enterAnimationSpeed / (SpeedDivider / 2f);

                    webDuration -= Time.deltaTime;
                }

                if(webDuration <= 0 && webTrap != null && spawnedBy != null )
                    spawnedBy.BreakWebServerRpc(webTrap.trapID);
            }

            void OnTriggerExit(Collider other)
            {
                int colliderInstanceID = other.GetInstanceID();
                if (!_touchingEnemyParts.TryGetValue(colliderInstanceID, out int enemyInstanceID))
                    return; // Enemy isn't in the web

                _touchingEnemyParts.Remove(colliderInstanceID);
                if (_touchingEnemies.TryGetValue(enemyInstanceID, out EnemyInfo enemyInfo))
                {
                    // Enemy is touching the web
                    if (!_touchingEnemyParts.ContainsValue(enemyInstanceID))
                    {
                        // Enemy left the web with every part
                        LethalMon.Log($"Enemy {enemyInfo.enemyAI.name} left the web.", LethalMon.LogType.Warning);
                        ResetEnemyValues(enemyInstanceID);
                        _touchingEnemies.Remove(enemyInstanceID);

                        if (_touchingEnemies.Count == 0 && webTrap != null && !webTrap.currentTrappedPlayer)
                            webTrap.webAudio.Stop();
                    }
                }
            }

            void OnDestroy()
            {
                foreach(var enemyInstanceID in _touchingEnemies.Keys)
                    ResetEnemyValues(enemyInstanceID);
            }

            void ResetEnemyValues(int instanceID)
            {
                if (!_touchingEnemies.TryGetValue(instanceID, out EnemyInfo enemyInfo)) return;

                enemyInfo.enemyAI.agent.speed = enemyInfo.enterAgentSpeed;
                enemyInfo.enemyAI.creatureAnimator.speed = enemyInfo.enterAnimationSpeed;
            }
        }
        #endregion
    }
}
