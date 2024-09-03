﻿using GameNetcodeStuff;
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
        internal bool isWebJumping = false;
        internal float timeOfLastWebJump = 0f;
        private float? _defaultJumpForce = null;
        private Coroutine? _webJumpCoroutine = null;

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
                var basePos = ownerPlayer.transform.position + Vector3.up * 0.5f + ownerPlayer.transform.forward * 3f;
                ShootWeb(basePos, basePos + ownerPlayer.transform.forward);
            }
        }
        #endregion

        #region Base Methods
        internal override void Start()
        {
            base.Start();

            shootWebCooldown = GetCooldownWithId(ShootWebCooldownID);

            if(IsTamed)
                Spider.transform.localScale = Vector3.one * 0.7f;

            EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true);
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
            // OWNER ONLY
            base.OnTamedFollowing();

            if (CanDefend)
                TargetNearestEnemy();
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

            JumpedOnWebServerRpc(webTrapID);
        }

        [ServerRpc(RequireOwnership = false)]
        internal void JumpedOnWebServerRpc(int webTrapID) => JumpedOnWebClientRpc(webTrapID);

        [ClientRpc]
        internal void JumpedOnWebClientRpc(int webTrapID)
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
        }

        internal IEnumerator PerformWebJump()
        {
            var player = Utils.CurrentPlayer;

            if (_defaultJumpForce == null || !isWebJumping)
                _defaultJumpForce = player.jumpForce;

            if (player.jumpCoroutine != null)
                player.StopCoroutine(player.jumpCoroutine);
            player.jumpCoroutine = player.StartCoroutine(player.PlayerJump());

            player.playerSlidingTimer = 0f;
            player.isJumping = true;
            player.sprintMeter = Mathf.Clamp(player.sprintMeter - 0.08f, 0f, 1f);

            isWebJumping = true;
            timeOfLastWebJump = Time.realtimeSinceStartup;

            var modifiedJumpForce = _defaultJumpForce.Value * SpiderBounceForce;
            //LethalMon.Log("Modified jump force: " + modifiedJumpForce);

            const string Jumping = "Jumping";
            yield return new WaitUntil(() =>
            {
                player.jumpForce = modifiedJumpForce;
                return (Time.realtimeSinceStartup - timeOfLastWebJump > 1f) && !player.playerBodyAnimator.GetBool(Jumping);
            });

            player.jumpForce = _defaultJumpForce.Value;
            isWebJumping = false;
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
            LethalMon.Log("WebBending");
            var initialScale = webObject.transform.localScale;
            float duration = 1f;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                float scaleMultiplier = WebBendingCurve.Evaluate(elapsedTime / duration);

                webObject.transform.localScale = initialScale * scaleMultiplier;

                elapsedTime += Time.deltaTime;
                yield return null;
            }
        }
        #endregion

        #region Webshooting
        // HOST ONLY!
        internal void ShootWebsAround(int amount = 10) // Shoot webs in any direction
        {
            for (int i = 0; i < amount; i++)
            {
                Vector3 direction = Vector3.Scale(UnityEngine.Random.onUnitSphere, new Vector3(1f, UnityEngine.Random.Range(0.5f, 3f), 1f)).normalized;
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

        internal void ShootWebAt(Vector3 targetPosition, float size = 3f) => ShootWeb(targetPosition - (targetPosition - ownerPlayer!.transform.position).normalized * size + Vector3.up * 0.5f, targetPosition + Vector3.up * 0.5f);

        internal void ShootWeb(Vector3 from, Vector3 to)
        {
            shootWebCooldown?.Reset();

            if (!Spider.IsOwner) return;

            Spider.SpawnWebTrapServerRpc(from, to);
            if(IsTamed)
                Spider.webTraps.Last().gameObject.AddComponent<WebBehaviour>().spawnedBy = this;
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
        internal class WebBehaviour : MonoBehaviour
        {
            private const float SpeedDivider = 20f;

            private float _lifeTime = 5f; // Time how long the web can handle enemies

            private struct EnemyInfo(EnemyAI enemyAI, float enterAgentSpeed, float enterAnimationSpeed)
            {
                internal EnemyAI enemyAI { get; set; } = enemyAI;
                internal float enterAgentSpeed { get; set; } = enterAgentSpeed;
                internal float enterAnimationSpeed { get; set; } = enterAnimationSpeed;
            }

            internal SpiderTamedBehaviour? spawnedBy = null;
            internal SandSpiderWebTrap? webTrap = null;

            private Dictionary<int, EnemyInfo> _touchingEnemies = new Dictionary<int, EnemyInfo>();

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
                if (_nonEnemyCollider.Contains(other.GetInstanceID())) return; // save performance

                var enemyAI = other.GetComponentInParent<EnemyAI>();
                if (enemyAI == null)
                {
                    _nonEnemyCollider.Add(other.GetInstanceID());
                    return;
                }

                LethalMon.Log($"Enemy {enemyAI.name} entered the web.", LethalMon.LogType.Warning);
                _touchingEnemies.Add(other.GetInstanceID(), new EnemyInfo(enemyAI, enemyAI.agent.speed, enemyAI.creatureAnimator.speed));
                if (webTrap != null && spawnedBy?.Spider != null)
                {
                    webTrap.webAudio.Play();
                    webTrap.webAudio.PlayOneShot(spawnedBy.Spider.hitWebSFX);
                }
            }

            void OnTriggerStay(Collider other)
            {
                if (_touchingEnemies.TryGetValue(other.GetInstanceID(), out EnemyInfo enemyInfo))
                {
                    //LethalMon.Log($"Enemy {enemyInfo.enemyAI.name} is inside the web.");
                    enemyInfo.enemyAI.agent.speed = enemyInfo.enterAgentSpeed / SpeedDivider;
                    enemyInfo.enemyAI.creatureAnimator.speed = enemyInfo.enterAnimationSpeed / SpeedDivider;

                    _lifeTime -= Time.deltaTime;
                }

                if( _lifeTime <= 0 && webTrap != null && spawnedBy != null )
                    spawnedBy.BreakWebServerRpc(webTrap.trapID);
            }

            void OnTriggerExit(Collider other)
            {
                if (_touchingEnemies.TryGetValue(other.GetInstanceID(), out EnemyInfo enemyInfo))
                    LethalMon.Log($"Enemy {enemyInfo.enemyAI.name} left the web.", LethalMon.LogType.Warning);
                ResetEnemyValues(other.GetInstanceID());
                _touchingEnemies.Remove(other.GetInstanceID());

                if(_touchingEnemies.Count == 0 && webTrap != null && !webTrap.currentTrappedPlayer)
                    webTrap.webAudio.Stop();
            }

            void OnDestroy()
            {
                foreach(var enemyInstanceID in _touchingEnemies.Keys)
                    ResetEnemyValues(enemyInstanceID);
                _touchingEnemies.Clear();
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
