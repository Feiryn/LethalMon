﻿using GameNetcodeStuff;
using System.Collections.Generic;
using LethalMon.Items;
using Unity.Netcode;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;
using System.Linq;
using System;

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

        internal const int ItemCarryCount = 4;

        internal const string PhysicsObjectName = "PhysicsRootObject";
        internal static bool PhysicsRegionAdded = false;
        private GameObject? _physicsRootObject = null;
        private BoxCollider? _physicsCollider = null;
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

                    _physicsCollider = _physicsRootObject.GetComponent<BoxCollider>();
                    Physics.IgnoreCollision(_physicsCollider, Utils.CurrentPlayer.playerCollider);
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

        internal override void OnEscapedFromBall(PlayerControllerB playerWhoThrewBall)
        {
            base.OnEscapedFromBall(playerWhoThrewBall);

            Blob.transform.localScale *= 1.5f;
        }

        internal override void OnUpdate(bool update = false, bool doAIInterval = true)
        {
            base.OnUpdate(update, doAIInterval);

            Blob.Update();

            if(Blob.agent != null)
                Blob.agent.stoppingDistance = 0f;

            Blob.timeSinceHittingLocalPlayer = 0f; // keeps it friendly
        }

        internal override void DoAIInterval()
        {
            base.DoAIInterval();

            var carriedItems = CarriedItems;
            if (carriedItems.Count > ModConfig.Instance.values.BlobMaxItems)
            {
                for(int i = carriedItems.Count - 1; i >= ItemCarryCount; --i)
                    DropItem(carriedItems[i]);
            }
        }
        #endregion

        #region Methods
        internal void AdjustPhysicsObjectScale()
        {
            if (_physicsRootObject == null) return;

            LethalMon.Log("Tamed Blob: Update physics object scale.");
            _physicsRootObject.transform.SetParent(null, true);
            _physicsRootObject.transform.localScale = Vector3.Scale(new(3f, 0.5f, 3f), Blob.transform.localScale);
            _physicsRootObject.transform.position = Blob.transform.position;
            _physicsRootObject.transform.SetParent(Blob.transform, true);
        }

        internal void DropItem(GrabbableObject item)
        {
            item.parentObject = null;
            item.transform.SetParent(StartOfRound.Instance.propsContainer, worldPositionStays: true);

            var colliderEnabled = _physicsCollider != null && _physicsCollider.enabled;
            var targetFloorPosition = item.GetItemFloorPosition(colliderEnabled ? new Vector3(item.transform.position.x, _physicsCollider!.bounds.center.y - _physicsCollider.bounds.size.y / 2f - 0.1f, item.transform.position.z) : default);

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

        public override PokeballItem? RetrieveInBall(Vector3 position)
        {
            var carriedItems = CarriedItems;

            LethalMon.Log(carriedItems.Count + " items in the physics region.");
            foreach (var item in carriedItems)
                DropItem(item);

            return base.RetrieveInBall(position);
        }

        public override bool CanBeTeleported()
        {
            return CarriedItems.Count == 0;
        }
        #endregion
    }
}
