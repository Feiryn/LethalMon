using GameNetcodeStuff;
using HarmonyLib;
using LethalMon.Items;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Random = UnityEngine.Random;
using Vector3 = UnityEngine.Vector3;

namespace LethalMon.Behaviours
{
    internal class KidnapperFoxTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        private BushWolfEnemy? _fox = null;
        internal BushWolfEnemy Fox
        {
            get
            {
                if (_fox == null)
                    _fox = (Enemy as BushWolfEnemy)!;

                return _fox;
            }
        }

        private MoldSpreadManager? _moldSpreadManager = null;
        internal MoldSpreadManager MoldSpreadManager
        {
            get
            {
                if (_moldSpreadManager == null)
                    _moldSpreadManager = FindObjectOfType<MoldSpreadManager>();

                return _moldSpreadManager;
            }
        }

        internal static List<ulong> hidingPlayers = [];

        internal List<GameObject> hidingSpores = [];
        internal readonly int MaximumHidingSpores = 3;
        internal Vector3 lastHidingSporePosition = Vector3.zero;

        internal readonly float IdleTimeTillSpawningSpore = 3f;
        internal float idleTime = 0f;


        private DateTime canTongueHitAfter = new DateTime(0);
        internal readonly float TongueHitCooldownSeconds = 5f;
        internal Coroutine? tongueShootCoroutine = null;
        internal Coroutine? pushTargetCoroutine = null;
        internal readonly int TongueKillPercentage = 20;
        internal EnemyAI? lastHitEnemy = null;
        internal int enemyHitTimes = 0;
        #endregion

        #region Action Keys
        private List<ActionKey> _actionKeys = new List<ActionKey>()
        {
            new ActionKey() { actionKey = ModConfig.Instance.ActionKey1, description = "Spawn hiding shroud" }
        };
        internal override List<ActionKey> ActionKeys => _actionKeys;

        internal bool tongueOut = false;
        internal override void ActionKey1Pressed()
        {
            base.ActionKey1Pressed();

            Fox.creatureAnimator.SetBool("ShootTongue", value: tongueOut);
            tongueOut = !tongueOut;
            Fox.creatureAnimator.SetBool("mouthOpen", Vector3.Distance(Fox.transform.position, ownerPlayer!.transform.position) < 3f);
        }
        #endregion

        #region Base Methods
        internal void Awake()
        {
#if DEBUG
            ownerPlayer = Utils.AllPlayers.Where((p) => p.playerClientId == 0ul).First();
            ownClientId = 0ul;
            if(Utils.IsHost)
                MoldSpreadManager.GenerateMold(ownerPlayer.transform.position, 2); // Needed so it doesn't get yeeted again at Start
#endif
        }

        internal override void Start()
        {
            base.Start();
#if DEBUG
            // Debug
            ownerPlayer = Utils.AllPlayers.Where((p) => p.playerClientId == 0ul).First();
            ownClientId = 0ul;
            isOutsideOfBall = true;
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
            if (Utils.IsHost)
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true);
#endif
        }

        internal override void InitTamingBehaviour(TamingBehaviour behaviour)
        {
            // OWNER ONLY
            base.InitTamingBehaviour(behaviour);

            switch (behaviour)
            {
                case TamingBehaviour.TamedFollowing:
                    Fox.inSpecialAnimation = false;
                    Fox.EnableEnemyMesh(enable: true);
                    if (Fox.agent != null)
                    {
                        Fox.agent.enabled = true;
                        Fox.agent.speed = 5f;
                    }
                    //Fox.tongueTarget = ownerPlayer == Utils.CurrentPlayer ? ownerPlayer!.upperSpineLocalPoint : ownerPlayer!.upperSpine;
                    break;

                case TamingBehaviour.TamedDefending:
                    ShootTongueAtEnemy();
                    break;

                default: break;
            }
        }

        internal override void OnTamedFollowing()
        {
            // OWNER ONLY
            base.OnTamedFollowing();

            Fox.CalculateAnimationDirection(Fox.maxAnimSpeed);
            Fox.LateUpdate(); // Fox.AddProceduralOffsetToLimbsOverTerrain();

            if (Fox.agentLocalVelocity.x == 0f && Fox.agentLocalVelocity.z == 0f) // Idle
            {
                idleTime += Time.deltaTime;
                if (idleTime > IdleTimeTillSpawningSpore)
                {
                    idleTime = 0f;
                    if (Vector3.Distance(lastHidingSporePosition, Fox.transform.position) > 3f)
                    {
                        SpawnHidingMoldServerRpc(Fox.transform.position);
                        lastHidingSporePosition = Fox.transform.position;
                    }
                }
            }
            else
                idleTime = 0f;

            if (canTongueHitAfter < DateTime.Now)
                TargetNearestEnemy();
        }

        internal override void OnTamedDefending()
        {
            // OWNER ONLY
            if ((targetEnemy == null || targetEnemy.isEnemyDead) && tongueShootCoroutine != null)
            {
                StopCoroutine(tongueShootCoroutine);
                tongueShootCoroutine = null;

                targetEnemy = null;
                Fox.creatureAnimator.SetBool("shootTongue", false);
                Fox.spitParticle.Stop();

                SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
            }

            base.OnTamedDefending();
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            // ANY CLIENT
            base.OnEscapedFromBall(playerWhoThrewBall);
        }

        internal override void OnCallFromBall()
        {
            base.OnCallFromBall();

            Fox.EnableEnemyMesh(true);
        }

        public override PokeballItem? RetrieveInBall(Vector3 position)
        {
            // ANY CLIENT
            for (int i = hidingSpores.Count - 1; i >= 0; --i)
                DestroyHidingSpore(hidingSpores[i]);

            if (tongueShootCoroutine != null)
                StopCoroutine(tongueShootCoroutine);

            if (pushTargetCoroutine != null)
                StopCoroutine(pushTargetCoroutine);

            return base.RetrieveInBall(position);
        }

        internal override void TurnTowardsPosition(Vector3 position)
        {
            //base.TurnTowardsPosition(position);
            Fox.LookAtPosition(position);
        }
        #endregion

        #region TongueShooting
        internal void ShootTongueAtEnemy()
        {
            if (targetEnemy == null || tongueShootCoroutine != null || canTongueHitAfter >= DateTime.Now) return;

            var killEnemy = targetEnemy.enemyType.canDie && (enemyHitTimes >= 4 || Random.Range(0, 100) < TongueKillPercentage);

            tongueShootCoroutine = StartCoroutine(ShootTongueAtEnemyCoroutine(killEnemy));

            if (killEnemy)
            {
                lastHitEnemy = null;
                enemyHitTimes = 0;
            }
            else if (lastHitEnemy == targetEnemy)
                enemyHitTimes++;
            else
            {
                lastHitEnemy = targetEnemy;
                enemyHitTimes = 1;
            }
        }

        internal IEnumerator ShootTongueAtEnemyCoroutine(bool howlAndKill = false)
        {
            if (targetEnemy == null)
            {
                tongueShootCoroutine = null;
                yield break;
            }

            LethalMon.Log("ShootTongueAtEnemy");

            if (howlAndKill)
            {
                Fox.DoMatingCall();
                float timer = 0f;
                while (timer < 1.5f)
                {
                    timer += Time.deltaTime;
                    Fox.LookAtPosition(targetEnemy.transform.position);
                    yield return null;
                }
            }

            if (howlAndKill)
            {
                int hits = 0;
                while (!targetEnemy.isEnemyDead && hits < 3)
                {
                    PushTargetEnemyWithTongueServerRpc(targetEnemy.NetworkObject, 1);
                    yield return new WaitWhile(() => pushTargetCoroutine != null);
                    hits++;
                }

                if (!targetEnemy.isEnemyDead) // Finisher
                {
                    PushTargetEnemyWithTongueServerRpc(targetEnemy.NetworkObject, targetEnemy.enemyHP);
                    yield return new WaitWhile(() => pushTargetCoroutine != null);
                }
            }
            else
            {
                PushTargetEnemyWithTongueServerRpc(targetEnemy.NetworkObject); // warning shot
                yield return new WaitWhile(() => pushTargetCoroutine != null);
            }

            targetEnemy = null;

            LethalMon.Log("ShootTongueAtEnemy -> Finished coroutine");
            tongueShootCoroutine = null;
            canTongueHitAfter = DateTime.Now.AddSeconds(TongueHitCooldownSeconds);
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);

        }

        [ServerRpc(RequireOwnership = false)]
        internal void PushTargetEnemyWithTongueServerRpc(NetworkObjectReference enemyRef, int damageOnHit = 0)
        {
            LethalMon.Log("PushTargetEnemyWithTongueServerRpc");
            PushTargetEnemyWithTongueClientRpc(enemyRef, damageOnHit);
        }

        [ClientRpc]
        internal void PushTargetEnemyWithTongueClientRpc(NetworkObjectReference enemyRef, int damageOnHit = 0)
        {
            if (!enemyRef.TryGet(out NetworkObject networkObject) || !networkObject.TryGetComponent(out targetEnemy) || targetEnemy == null)
            {
                LethalMon.Log("PushTargetEnemyWithTongueClientRpc: Unable to get enemy object.", LethalMon.LogType.Error);
                return;
            }

            LethalMon.Log("PushTargetEnemyWithTongueClientRpc");
            if (pushTargetCoroutine != null)
                StopCoroutine(pushTargetCoroutine);

            pushTargetCoroutine = StartCoroutine(PushTargetEnemyWithTongue(damageOnHit));
        }

        internal IEnumerator PushTargetEnemyWithTongue(int damageOnHit = 0)
        {
            if (targetEnemy == null)
            {
                pushTargetCoroutine = null;
                yield break;
            }

            LethalMon.Log("PushTargetEnemyWithTongue started.", LethalMon.LogType.Warning);
            // Calculate enemy push path
            var force = Mathf.Min(10f - Vector3.Distance(Fox.transform.position, targetEnemy.transform.position), 3f) * 10f; // smaller distance = larger flight
            if (Utils.TryGetRealEnemyBounds(targetEnemy, out Bounds enemyBounds))
            {
                LethalMon.Log("ENEMY HEIGHT: " + enemyBounds.size.y);
                force /= enemyBounds.size.y; // Larger enemies are less affected
            }

            var direction = (targetEnemy.transform.position - Fox.transform.position).normalized;
            yield return PushTargetEnemyWithTongue(direction, force, damageOnHit);
            LethalMon.Log("PushTargetEnemyWithTongue finished.", LethalMon.LogType.Warning);
            pushTargetCoroutine = null;
        }

        internal IEnumerator PushTargetEnemyWithTongue(Vector3 direction, float force, int damageOnHit = 0)
        {
            if (targetEnemy == null) yield break;

            yield return ReachTargetEnemyWithTongue();

            LethalMon.Log("PushTargetEnemyWithTongue in direction " + direction + " with a force of " + force);

            var startPosition = targetEnemy.transform.position;
            var landingPosition = targetEnemy.transform.position + direction * force;

            var distanceShortenedByWall = 0f;
            if (Physics.Linecast(startPosition + Vector3.up, landingPosition + Vector3.up, out RaycastHit hit, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
            {
                // Obstacle in the way
                distanceShortenedByWall = Vector3.Distance(landingPosition, hit.point);
                landingPosition = hit.point - direction * 0.5f - Vector3.up;
            }

            targetEnemy.enabled = false;
            targetEnemy.agent.enabled = false;

            if (damageOnHit > 0)
            {
                DropBlood(targetEnemy.transform.position, damageOnHit, damageOnHit);
                targetEnemy.HitEnemy(damageOnHit, null, true);
            }
            else
            {
                targetEnemy.creatureSFX.PlayOneShot(targetEnemy.enemyType.hitBodySFX);
                WalkieTalkie.TransmitOneShotAudio(targetEnemy.creatureSFX, targetEnemy.enemyType.hitBodySFX);
            }

            var totalFlyingTime = force / 20f;
            yield return PushTargetEnemyTo(landingPosition, totalFlyingTime, Fox.LateUpdate); // LateUpdate to update tongue

            if (distanceShortenedByWall > 0f)
            {
                var timer = 0f;
                var timeStunned = Mathf.Min(distanceShortenedByWall / 10f, 0.3f);
                LethalMon.Log("PushTargetEnemyWithTongue -> Hit wall. Stunning for " + timeStunned);
                while (timer < timeStunned)
                {
                    timer += Time.deltaTime;
                    if (Fox.tongueLengthNormalized > 0f)
                        Fox.LateUpdate();
                    yield return null;
                }
            }
            while (Fox.tongueLengthNormalized > 0f && Fox.tongueTarget == null)
            {
                Fox.LateUpdate();
                yield return null;
            }

            if (targetEnemy != null && !targetEnemy.isEnemyDead)
            {
                if (targetEnemy.agent != null)
                    targetEnemy.agent.enabled = true;
                targetEnemy.enabled = true;
            }

            Fox.tongueLengthNormalized = 0f;
            Fox.LateUpdate(); // Update tongue

            Fox.spitParticle.Stop();
            LethalMon.Log("PushTargetEnemyWithTongue -> Parameterised coroutine finished");
        }

        internal IEnumerator ReachTargetEnemyWithTongue()
        {
            if (targetEnemy == null) yield break;

            Fox.creatureAnimator.SetBool("ShootTongue", value: true);
            Fox.creatureVoice.PlayOneShot(Fox.shootTongueSFX);
            Fox.tongueLengthNormalized = 0f;
            Fox.tongueTarget = targetEnemy!.transform;

            while (Fox.tongueLengthNormalized < 1f)
            {
                Fox.LookAtPosition(targetEnemy.transform.position);
                Fox.LateUpdate(); // to update the tongue
                yield return null;
            }

            Fox.creatureAnimator.SetBool("ShootTongue", value: false);
            Fox.tongueTarget = null;
        }

        internal IEnumerator PushTargetEnemyTo(Vector3 targetPosition, float duration, Action? onUpdate)
        {
            if (targetEnemy == null) yield break;

            var startPosition = targetEnemy.transform.position;
            var timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                onUpdate?.Invoke();
                TeleportEnemy(targetEnemy, Vector3.Lerp(startPosition, targetPosition, timer / duration));
                yield return null;
            }
        }
        #endregion

        #region HideSpores
        [ServerRpc(RequireOwnership = false)]
        public void SpawnHidingMoldServerRpc(Vector3 position)
        {
            SpawnHidingMoldClientRpc(position);
        }

        [ClientRpc]
        public void SpawnHidingMoldClientRpc(Vector3 position)
        {
            LethalMon.Log("SpawnHidingMoldClientRpc");

            var moldSpore = Instantiate(MoldSpreadManager.moldPrefab, position, Quaternion.Euler(new Vector3(0f, UnityEngine.Random.Range(-180f, 180f), 0f)), MoldSpreadManager.moldContainer);
            foreach (var meshRenderer in moldSpore.GetComponentsInChildren<MeshRenderer>())
            {
                foreach (var material in meshRenderer.materials)
                    material.color = new Color(Random.Range(0f, 0.8f), Random.Range(0.3f, 1f), 1f);
            }

            moldSpore.AddComponent<HidingMoldTrigger>().moldOwner = this;
            var scanNode = moldSpore.GetComponentInChildren<ScanNodeProperties>();
            if(scanNode != null)
            {
                scanNode.headerText = "Hiding Shroud";
                scanNode.subText = "Crouch to hide from enemies";
                scanNode.nodeType = 2;
            }

            MoldSpreadManager.generatedMold.Add(moldSpore);
            hidingSpores.Add(moldSpore);
            if(hidingSpores.Count > MaximumHidingSpores)
                DestroyHidingSpore(hidingSpores[0]);
        }

        [ServerRpc(RequireOwnership = false)]
        internal void DestroyHidingSporeServerRpc(int index)
        {
            DestroyHidingSporeClientRpc(index);
        }

        [ClientRpc]
        public void DestroyHidingSporeClientRpc(int index)
        {
            if(hidingSpores.Count >= index)
            {
                LethalMon.Log("Syncing error for hidingSpores. Index out of range.", LethalMon.LogType.Warning);
                return;
            }

            DestroyHidingSpore(hidingSpores[index]);
        }

        public void DestroyHidingSpore(GameObject hidingSpore)
        {
            hidingSpore.SetActive(value: false);
            hidingSpores.Remove(hidingSpore);
            Instantiate(MoldSpreadManager.destroyParticle, hidingSpore.transform.position + Vector3.up * 0.5f, Quaternion.identity, null);
            MoldSpreadManager.destroyAudio.Stop();
            MoldSpreadManager.destroyAudio.transform.position = hidingSpore.transform.position + Vector3.up * 0.5f;
            MoldSpreadManager.destroyAudio.Play();
            RoundManager.Instance.PlayAudibleNoise(MoldSpreadManager.destroyAudio.transform.position, 6f, 0.5f, 0, noiseIsInsideClosedShip: false, 99611);
        }

        [ServerRpc(RequireOwnership = false)]
        internal void SetPlayerHiddenInMoldServerRpc(ulong playerID, bool hide = true)
        {
            SetPlayerHiddenInMoldClientRpc(playerID, hide);
        }

        [ClientRpc]
        internal void SetPlayerHiddenInMoldClientRpc(ulong playerID, bool hide = true)
        {
            if (hide)
            {
                hidingPlayers.Add(playerID);
                LethalMon.Log($"Player {playerID} is now hiding.");
            }
            else
            {
                hidingPlayers.Remove(playerID);
                LethalMon.Log($"Player {playerID} is not hiding anymore.");
            }
        }


        internal class HidingMoldTrigger : MonoBehaviour
        {
            internal KidnapperFoxTamedBehaviour? moldOwner = null;
            internal bool localPlayerInsideMold = false, localPlayerHiding = false;

            private void OnTriggerEnter(Collider other)
            {
                if (!other.gameObject.TryGetComponent(out PlayerControllerB player) || player != Utils.CurrentPlayer)
                    return;

                foreach (var hidingPlayer in hidingPlayers)
                    LethalMon.Log("Hiding player: " + hidingPlayer, LethalMon.LogType.Warning);

                localPlayerInsideMold = true;

                if (player.isCrouching)
                    HideLocalPlayer();
            }

            private void OnTriggerStay()
            {
                if (!localPlayerInsideMold)
                    return;

                var isCrouching = Utils.CurrentPlayer.isCrouching;
                if (localPlayerHiding && !isCrouching)
                    UnhideLocalPlayer();
                else if (!localPlayerHiding && isCrouching)
                    HideLocalPlayer();
            }

            private void OnTriggerExit(Collider other)
            {
                if (!other.gameObject.TryGetComponent(out PlayerControllerB player) || player != Utils.CurrentPlayer)
                    return;

                localPlayerInsideMold = false;

                if(localPlayerHiding)
                    UnhideLocalPlayer();
            }

            private void OnDestroy()
            {
                if (localPlayerHiding)
                    UnhideLocalPlayer();
            }

            private void OnDisable()
            {
                if (localPlayerHiding)
                    UnhideLocalPlayer();
            }

            private void HideLocalPlayer()
            {
                localPlayerHiding = true;
                Utils.CurrentPlayer.drunkness = 0.3f;
                moldOwner!.SetPlayerHiddenInMoldServerRpc(Utils.CurrentPlayerID!.Value, true);
            }

            private void UnhideLocalPlayer()
            {
                localPlayerHiding = false;
                Utils.CurrentPlayer.drunkness = 0f;
                moldOwner!.SetPlayerHiddenInMoldServerRpc(Utils.CurrentPlayerID!.Value, false);
            }
        }
        #endregion

        #region Patches
        [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.PlayerIsTargetable))]
        [HarmonyPostfix]
        public static bool PlayerIsTargetablePostfix(bool __result, EnemyAI __instance, PlayerControllerB playerScript)
        {
            if (hidingPlayers != null && hidingPlayers.Contains(playerScript.playerClientId) && Vector3.Distance(__instance.transform.position, playerScript.transform.position) > 3f)
            {
                LethalMon.Log("Player inside hiding spore. Not targetable.");
                return false;
            }

            return __result;
        }
        #endregion
    }
}

