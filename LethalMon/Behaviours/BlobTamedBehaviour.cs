using GameNetcodeStuff;
using System.Collections.Generic;
using LethalMon.Items;
using Unity.Netcode;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;
using System.Linq;

namespace LethalMon.Behaviours
{
    internal class BlobTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        private BlobAI? _blob = null;
        internal BlobAI Blob
        {
            get
            {
                if (_blob == null)
                    _blob = (Enemy as BlobAI)!;

                return _blob;
            }
        }

        private Vector3? _previousLocalScale = Vector3.zero;

        // PhysicsRegion
        internal const string PhysicsObjectName = "PhysicsRootObject";
        internal static bool PhysicsRegionAdded = false;
        private GameObject? _physicsRootObject = null;
        private BoxCollider? _physicsCollider = null;
        private PlayerPhysicsRegion? _physicsRegion = null;
        #endregion

        #region Base Methods
        public override void Start()
        {
            base.Start();

            if (IsTamed)
            {
                Blob.transform.localScale = new(1f, 0.5f, 1f);

                Blob.timeSinceHittingLocalPlayer = 0f;

                _physicsRootObject = Blob.transform.Find(PhysicsObjectName)?.gameObject;
                if (_physicsRootObject != null)
                {
                    //LethalMon.Log("Found root object.");
                    _physicsRootObject.SetActive(true);
                    Blob.enabled = false;

                    _physicsCollider = _physicsRootObject.GetComponent<BoxCollider>();
                    Physics.IgnoreCollision(_physicsCollider, Utils.CurrentPlayer.playerCollider);
                    _physicsRegion = _physicsRootObject.GetComponentInChildren<PlayerPhysicsRegion>();
                    if (_physicsRegion != null)
                    {
                        //LethalMon.Log("Found physics region.");
                        _physicsRegion.disablePhysicsRegion = IsOwnerPlayer; // don't transport owner
                    }

                    Invoke(nameof(AdjustPhysicsObjectScale), 2f); // Update scale when blob if fully sized
                }

                _previousLocalScale = Blob.transform.localScale;
                for (int i = Blob.maxDistanceForSlimeRays.Length - 1; i >= 0; i--)
                    Blob.maxDistanceForSlimeRays[i] = 1.5f;
            }
        }

        internal static void AddPhysicsSectionToPrefab()
        {
            if (PhysicsRegionAdded) return;

            var enemyTypes = Utils.GetEnemyTypes(Utils.Enemy.Blob);
            if (enemyTypes.Length == 0)
            {
                LethalMon.Log("Unable to get blob types.", LethalMon.LogType.Error);
                return;
            }

            LethalMon.Log("Tamed Blob: CreatePhysicsRegion");

            foreach (var enemyType in enemyTypes)
            {
                if (enemyType.enemyPrefab == null)
                {
                    LethalMon.Log("Unable to get blob prefab.");
                    continue;
                }

                // Root object
#if _DEBUG
                GameObject rootObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

                if (rootObject.TryGetComponent(out MeshRenderer meshRenderer))
                {
                    meshRenderer.material = new Material(Shader.Find("HDRP/Lit")) { color = Color.red };
                    meshRenderer.enabled = true;
                }
                rootObject.name = PhysicsObjectName;
                var rootCollider = rootObject.GetComponent<BoxCollider>();
#else
                var rootObject = new GameObject(PhysicsObjectName);
                var rootCollider = rootObject.AddComponent<BoxCollider>();
#endif
                rootObject.layer = (int)Utils.LayerMasks.Mask.Room;
                rootObject.transform.SetParent(enemyType.enemyPrefab.transform, false);
                rootCollider.isTrigger = false;

                // ItemDropCollider
                var itemDropColliderObject = new GameObject("ItemDropCollider");
                itemDropColliderObject.layer = (int)Utils.LayerMasks.Mask.Triggers;
                var itemDropCollider = itemDropColliderObject.AddComponent<BoxCollider>();
                itemDropCollider.isTrigger = true;
                itemDropColliderObject.transform.localScale = new(1f, 1.5f, 1f);
                itemDropColliderObject.transform.SetParent(rootObject.transform, false);

                var physicsRegionObject = new GameObject("PlayerPhysicsRegion");
                physicsRegionObject.layer = (int)Utils.LayerMasks.Mask.Triggers;
                var physicsCollider = physicsRegionObject.AddComponent<BoxCollider>();
                physicsCollider.isTrigger = true;
                physicsRegionObject.transform.localScale = new(1f, 1.5f, 1f);
                physicsRegionObject.transform.SetParent(rootObject.transform, false);

                var physicsRegion = physicsRegionObject.AddComponent<PlayerPhysicsRegion>();
                physicsRegion.parentNetworkObject = enemyType.enemyPrefab.GetComponent<NetworkObject>();
                physicsRegion.physicsTransform = enemyType.enemyPrefab.gameObject.transform;
                physicsRegion.allowDroppingItems = true;
                physicsRegion.itemDropCollider = itemDropCollider;
                physicsRegion.physicsCollider = physicsCollider;
                physicsRegion.disablePhysicsRegion = false;
                physicsRegion.priority = 1;
                physicsRegion.maxTippingAngle = 360;

                rootObject.SetActive(false);

                PhysicsRegionAdded = true;
            }
        }

        public override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            base.OnEscapedFromBall(playerWhoThrewBall);

            Blob.transform.localScale *= 1.5f;
        }

        internal void FixedUpdate()
        {
            if(IsTamed)
                Blob.FixedUpdate();
        }

        public override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            base.OnUpdate(update, doAIInterval);

            Blob.Update();

            if(Blob.agent != null)
                Blob.agent.stoppingDistance = 0f;

            Blob.timeSinceHittingLocalPlayer = 0f; // keeps it friendly
            Blob.movingTowardsTargetPlayer = false;
        }

        public override void DoAIInterval()
        {
            //base.DoAIInterval();

            var carriedItems = CarriedItems;
            if (carriedItems.Count > ModConfig.Instance.values.BlobMaxItems) // todo: find a way to only run this if someone drops an item
            {
                for(int i = carriedItems.Count - 1; i >= ModConfig.Instance.values.BlobMaxItems; --i)
                    DropItemServerRpc(carriedItems[i].NetworkObject);
            }

            if (_previousLocalScale != Blob.transform.localScale)
            {
                _previousLocalScale = Blob.transform.localScale;
                AdjustPhysicsObjectScale();
            }

            if (Enemy.moveTowardsDestination)
            {
                Enemy.agent.SetDestination(Enemy.destination);
            }
            Enemy.SyncPositionToClients();
        }
        #endregion

        #region Methods
        public void AdjustPhysicsObjectScale()
        {
            if (_physicsRootObject == null) return;

            //LethalMon.Log("Tamed Blob: Update physics object scale.");
            _physicsRootObject.transform.SetParent(null, true);
            _physicsRootObject.transform.localScale = Vector3.Scale(new(3f, 0.5f, 3f), Blob.transform.localScale);
            _physicsRootObject.transform.position = Blob.transform.position;
            _physicsRootObject.transform.SetParent(Blob.transform, true);
        }

        [ServerRpc]
        internal void DropItemServerRpc(NetworkObjectReference itemRef) => DropItemClientRpc(itemRef);

        [ClientRpc]
        internal void DropItemClientRpc(NetworkObjectReference itemRef)
        {
            if(!itemRef.TryGet(out NetworkObject networkObject) || !networkObject.TryGetComponent(out GrabbableObject item))
            {
                LethalMon.Log("Blob.DropItemClientRpc: Failed to drop item. Reference not found", LethalMon.LogType.Error);
                return;
            }

            //LethalMon.Log("DropItem: " + item.itemProperties.itemName);

            item.parentObject = null;
            item.transform.SetParent(StartOfRound.Instance.propsContainer, worldPositionStays: true);

            var colliderEnabled = _physicsCollider != null && _physicsCollider.enabled;
            var targetFloorPosition = item.GetItemFloorPosition(colliderEnabled ? new Vector3(item.transform.position.x, _physicsCollider!.bounds.center.y - _physicsCollider.bounds.size.y / 2f + item.itemProperties.verticalOffset, item.transform.position.z) : default);

            item.EnablePhysics(enable: true);
            item.fallTime = 0f;
            item.startFallingPosition = item.transform.parent.InverseTransformPoint(item.transform.position);
            item.targetFloorPosition = item.transform.parent.InverseTransformPoint(targetFloorPosition);
            item.transform.localScale = item.originalScale;
            item.floorYRot = -1;
        }

        public List<GrabbableObject> CarriedItems
        {
            get
            {
                if (_physicsRootObject == null || !_physicsRootObject.activeSelf)
                    return [];

                var items = Blob.GetComponentsInChildren<GrabbableObject>().Where(item => !item.isHeld && !item.isHeldByEnemy);
                return items.Any() ? items.ToList() : [];
            }
        }

        public override BallItem? RetrieveInBall(Vector3 position)
        {
            if (Blob.IsOwner)
            {
                var carriedItems = CarriedItems;

                //LethalMon.Log(carriedItems.Count + " items in the physics region.");
                foreach (var item in carriedItems)
                    DropItemServerRpc(item.NetworkObject);
            }

            return base.RetrieveInBall(position);
        }

        public override bool CanBeTeleported() => CarriedItems.Count == 0;
        #endregion
    }
}
