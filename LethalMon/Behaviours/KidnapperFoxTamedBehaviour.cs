using GameNetcodeStuff;
using HarmonyLib;
using LethalLib.Modules;
using LethalMon.Items;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
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

        internal static NetworkList<ulong> hidingPlayers = new NetworkList<ulong>();

        internal List<GameObject> hidingSpores = [];
        internal readonly int MaximumHidingSpores = 3;
        internal Vector3 lastHidingSporePosition = Vector3.zero;

        internal readonly float IdleTimeTillSpawningSpore = 3f;
        internal float idleTime = 0f;
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
           // Fox.creatureAnimator.SetBool("ReelingPlayerIn", value: true);

            /*Fox.creatureAnimator.SetTrigger("MatingCall");
            int num = Random.Range(0, Fox.callsClose.Length);
            Fox.callClose.PlayOneShot(Fox.callsClose[num]);
            Fox.callFar.PlayOneShot(Fox.callsFar[num]);
            RoundManager.Instance.PlayAudibleNoise(base.transform.position, 20f, 0.6f, 0, noiseIsInsideClosedShip: false, 245403);*/
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
                    break;

                case TamingBehaviour.TamedDefending:
                    break;

                default: break;
            }
        }

        internal override void OnTamedFollowing()
        {
            // OWNER ONLY
            base.OnTamedFollowing();

            if(Fox.backedUpFromWatchingPlayer )

            Fox.CalculateAnimationDirection(Fox.maxAnimSpeed);
            Fox.AddProceduralOffsetToLimbsOverTerrain();

            if(Fox.agentLocalVelocity.x == 0f && Fox.agentLocalVelocity.z == 0f) // Idle
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
        }

        internal override void OnTamedDefending()
        {
            // OWNER ONLY
            base.OnTamedDefending();
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            // ANY CLIENT
            base.OnEscapedFromBall(playerWhoThrewBall);
        }

        public override PokeballItem? RetrieveInBall(Vector3 position)
        {
            // ANY CLIENT
            for (int i = hidingSpores.Count - 1; i >= 0; --i)
                DestroyHidingSpore(hidingSpores[i]);

            return base.RetrieveInBall(position);
        }
        #endregion

        #region Methods
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
            if (hidingPlayers.Contains(playerScript.playerClientId) && Vector3.Distance(__instance.transform.position, playerScript.transform.position) > 3f)
            {
                LethalMon.Log("Player inside hiding spore. Not targetable.");
                return false;
            }

            return __result;
        }
        #endregion
    }
}

