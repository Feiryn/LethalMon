using GameNetcodeStuff;
using HarmonyLib;
using LethalMon.Items;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Unity.Netcode;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace LethalMon.Behaviours
{
#if DEBUG
    internal class KidnapperFoxTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        internal BushWolfEnemy fox { get; private set; }

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

        internal bool controlTipEnabled = false;

        internal List<GameObject> hidingSpores = new List<GameObject>();
        internal readonly int MaximumHidingSpores = 3;
        #endregion

        #region Custom behaviours
        internal enum CustomBehaviour
        {
            TestBehavior = 1
        }
        internal override List<Tuple<string, Action>>? CustomBehaviourHandler => new()
        {
            { new (CustomBehaviour.TestBehavior.ToString(), OnTestBehavior) }
        };

        /*internal override void InitCustomBehaviour(int behaviour)
        {
            // OWNER ONLY
            base.InitCustomBehaviour(behaviour);

            switch ((CustomBehaviour)behaviour)
            {
                case CustomBehaviour.TestBehavior:
                    break;

                default:
                    break;
            }
        }*/

        internal void OnTestBehavior()
        {
            /* USE THIS SOMEWHERE TO ACTIVATE THE CUSTOM BEHAVIOR
                *   SwitchToCustomBehaviour((int)CustomBehaviour.TestBehavior);
            */
    }
    #endregion

    #region Action Keys
    private List<ActionKey> _actionKeys = new List<ActionKey>()
        {
            new ActionKey() { actionKey = ModConfig.Instance.ActionKey1, description = "Spawn hiding shroud" }
        };
        internal override List<ActionKey> ActionKeys => _actionKeys;

        internal override void ActionKey1Pressed()
        {
            base.ActionKey1Pressed();

            GenerateHidingMoldAt(fox.transform.position);
        }
        #endregion

        #region Base Methods

        internal void Awake()
        {
#if DEBUG
            ownerPlayer = Utils.AllPlayers.Where((p) => p.playerClientId == 0ul).First();
            MoldSpreadManager.GenerateMold(ownerPlayer.transform.position, 2); // Needed so it doesn't get yeeted again at Start
#endif
        }
        internal override void Start()
        {
            base.Start();

            fox = (Enemy as BushWolfEnemy)!;

#if DEBUG
            // Debug
            isOutsideOfBall = true;
            SwitchToTamingBehaviour(TamingBehaviour.TamedFollowing);
            if (Utils.IsHost)
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true);
#endif
        }

        /*internal override void InitTamingBehaviour(TamingBehaviour behaviour)
        {
            // OWNER ONLY
            base.InitTamingBehaviour(behaviour);

            switch (behaviour)
            {
                case TamingBehaviour.TamedFollowing:
                    break;

                case TamingBehaviour.TamedDefending:
                    break;

                default: break;
            }
        }*/

        internal override void OnTamedFollowing()
        {
            // OWNER ONLY
            base.OnTamedFollowing();

            fox.CalculateAnimationDirection(fox.maxAnimSpeed);
            fox.AddProceduralOffsetToLimbsOverTerrain();

#if DEBUG
            if (!controlTipEnabled)
            {
                EnableActionKeyControlTip(ModConfig.Instance.ActionKey1, true); // Move to InitTamingBehaviour later
                controlTipEnabled = true;

                fox.EnableEnemyMesh(enable: true);
                fox.agent.enabled = true;
                fox.agent.speed = 6f;
            }
#endif
        }

        public override void MoveTowards(Vector3 position)
        {
            LethalMon.Log("MoveTowards " + position);
            base.MoveTowards(position);
        }

        internal override void OnTamedDefending()
        {
            // OWNER ONLY
            base.OnTamedDefending();
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            // HOST ONLY
            base.OnEscapedFromBall(playerWhoThrewBall);
        }

        internal override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            // ANY CLIENT
            base.OnUpdate(update, doAIInterval);
        }

        internal override void DoAIInterval()
        {
            // ANY CLIENT, every EnemyAI.updateDestinationInterval, if OnUpdate.doAIInterval = true
            base.DoAIInterval();
        }

        public override PokeballItem RetrieveInBall(Vector3 position)
        {
            // ANY CLIENT
            for (int i = hidingSpores.Count - 1; i >= 0; --i)
                DestroyHidingSpore(hidingSpores[i]);

            return base.RetrieveInBall(position);
        }
        #endregion

        #region Methods
        internal void GenerateHidingMoldAt(Vector3 position)
        {
            var moldSpore = Instantiate(MoldSpreadManager.moldPrefab, position, Quaternion.Euler(new Vector3(0f, UnityEngine.Random.Range(-180f, 180f), 0f)), MoldSpreadManager.moldContainer);
            foreach(var meshRenderer in moldSpore.GetComponentsInChildren<MeshRenderer>())
            {
                foreach(var material in meshRenderer.materials)
                {
                    material.color = new Color(UnityEngine.Random.Range(0f, 0.5f), UnityEngine.Random.Range(0.3f, 0.9f), 1f);
                    LethalMon.Log("Changing material: " + material.color.ToString());
                    LethalMon.Log("Intended material: " + Color.blue.ToString());
                }
            }
            MoldSpreadManager.generatedMold.Add(moldSpore);
            hidingSpores.Add(moldSpore);
            if(hidingSpores.Count > MaximumHidingSpores)
                DestroyHidingSpore(hidingSpores.First());
        }

        internal void DestroyHidingSpore(GameObject hidingSpore)
        {
            hidingSpores.Remove(hidingSpore);
            hidingSpore.SetActive(value: false);
            Instantiate(MoldSpreadManager.destroyParticle, hidingSpore.transform.position + Vector3.up * 0.5f, Quaternion.identity, null);
            // todo: Use Utils.Play ...
            MoldSpreadManager.destroyAudio.Stop();
            MoldSpreadManager.destroyAudio.transform.position = hidingSpore.transform.position + Vector3.up * 0.5f;
            MoldSpreadManager.destroyAudio.Play();

            RoundManager.Instance.PlayAudibleNoise(MoldSpreadManager.destroyAudio.transform.position, 6f, 0.5f, 0, noiseIsInsideClosedShip: false, 99611);
        }
        #endregion

        #region RPCs
        [ServerRpc(RequireOwnership = false)]
        public void TestServerRpc(float someParameter, Vector3 anotherParameter)
        {
            // HOST ONLY
            TestClientRpc(someParameter, anotherParameter);
        }

        [ClientRpc]
        public void TestClientRpc(float someParameter, Vector3 anotherParameter)
        {
            // ANY CLIENT (HOST INCLUDED)
        }
        #endregion
    }
#endif
}

