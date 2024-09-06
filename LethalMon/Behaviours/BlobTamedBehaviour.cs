using GameNetcodeStuff;
using System.Collections.Generic;
using System;
using LethalMon.Items;
using Unity.Netcode;
using UnityEngine;
using System.Numerics;
using Vector3 = UnityEngine.Vector3;

namespace LethalMon.Behaviours
{
    internal class BlobTamedBehaviour : TamedEnemyBehaviour
    {
        #region Properties
        private BlobAI? _blob = null; // Replace with enemy class
        internal BlobAI Blob
        {
            get
            {
                if (_blob == null)
                    _blob = (Enemy as BlobAI)!;

                return _blob;
            }
        }

        internal const string PhysicsObjectName = "PhysicsRootObject";
        internal static bool PhysicsRegionAdded = false;
        private GameObject? _physicsRootObject = null;
        private PlayerPhysicsRegion? _physicsRegion = null;

        internal override string DefendingBehaviourDescription => "You can change the displayed text when the enemy is defending by something more precise... Or remove this line to use the default one";

        internal override bool CanDefend => false; // You can return false to prevent the enemy from switching to defend mode in some cases (if already doing another action or if the enemy can't defend at all)
        #endregion

        #region Base Methods
        internal override void Start()
        {
            base.Start();

            if (IsTamed)
            {
                Blob.timeSinceHittingLocalPlayer = 0f;

                _physicsRootObject = Blob.transform.Find(PhysicsObjectName)?.gameObject;
                if (_physicsRootObject != null)
                {
                    LethalMon.Log("Found root object.");
                    _physicsRootObject.SetActive(true);
                    Blob.enabled = false;

                    Physics.IgnoreCollision(_physicsRootObject.GetComponent<BoxCollider>(), Utils.CurrentPlayer.playerCollider);
                    _physicsRegion = _physicsRootObject.GetComponentInChildren<PlayerPhysicsRegion>();
                    if (_physicsRegion != null)
                    {
                        LethalMon.Log("Found physics region.");
                        _physicsRegion.disablePhysicsRegion = IsOwnerPlayer; // don't transport owner
                    }

                    Invoke(nameof(AdjustPhysicsObjectScale), 2f); // Update scale when blob if fully sized
                }
            }
        }

        internal static void AddPhysicsSectionToPrefab()
        {
            if (PhysicsRegionAdded) return;

            var enemyType = Utils.GetEnemyType(Utils.Enemy.Blob);
            if (enemyType == null)
            {
                LethalMon.Log("Unable to get blob prefab.", LethalMon.LogType.Error);
                return;
            }

            LethalMon.Log("Tamed Blob: CreatePhysicsRegion");

            // Root object
#if DEBUG
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

        internal override void OnTamedFollowing()
        {
            // OWNER ONLY
            base.OnTamedFollowing();
        }

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            // ANY CLIENT
            base.OnEscapedFromBall(playerWhoThrewBall);

            Blob.transform.localScale *= 1.5f;
        }

        internal override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            // ANY CLIENT
            base.OnUpdate(update, doAIInterval);

            Blob.Update();

            if(Blob.IsOwner)
                Blob.agent.stoppingDistance = 0f;

            Blob.timeSinceHittingLocalPlayer = 0f; // friendly
        }

        internal void AdjustPhysicsObjectScale()
        {
            if (_physicsRootObject == null) return;

            LethalMon.Log("Tamed Blob: Update physics object scale.");
            _physicsRootObject.transform.SetParent(null, true);
            _physicsRootObject.transform.localScale = Vector3.Scale(new(3f, 0.5f, 3f), Blob.transform.localScale);
            _physicsRootObject.transform.position = Blob.transform.position;
            _physicsRootObject.transform.SetParent(Blob.transform, true);
        }

        public override PokeballItem? RetrieveInBall(Vector3 position)
        {
            // ANY CLIENT

            if (_physicsRootObject != null)
            {
                var items = Blob.GetComponentsInChildren<GrabbableObject>();
                LethalMon.Log(items.Length + " items in the physics region.");
                foreach (var item in items)
                {
                    item.transform.SetParent(null, true);
                    item.parentObject = null;

                    // Inspired by PlayerControllerB.SetObjectAsNoLongerHeld
                    /*var dropPos = item.GetItemFloorPosition();
                    var droppedInElevator = !StartOfRound.Instance.shipBounds.bounds.Contains(dropPos);
                    if (droppedInElevator)
                    {
                        item.targetFloorPosition = StartOfRound.Instance.elevatorTransform.InverseTransformPoint(dropPos);
                        item.transform.SetParent(StartOfRound.Instance.elevatorTransform, true);
                    }
                    else
                    {
                        item.targetFloorPosition = StartOfRound.Instance.propsContainer.InverseTransformPoint(dropPos);
                        item.transform.SetParent(StartOfRound.Instance.propsContainer, true);
                    }

                    item.transform.localScale = item.originalScale;
                    item.fallTime = 0f;
                    item.startFallingPosition = item.transform.parent.InverseTransformPoint(item.transform.position);
                    item.EnablePhysics(enable: true);
                    item.EnableItemMeshes(enable: true);*/
                }
            }

            return base.RetrieveInBall(position);
        }
        #endregion
    }
}
