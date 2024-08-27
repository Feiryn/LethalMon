using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.Throw
{
    public abstract class ThrowableItem : GrabbableObject
    {
        #region Parameters
        // The maximum time the item can fall
        protected virtual float MaxFallTime => 30f;

        // The time step used to calculate the item's trajectory
        protected virtual float TimeStep => 0.01f;
        
        // The gravity applied to the item
        protected virtual Vector3 Gravity => Physics.gravity * 1.5f;
        
        // The radius of the item
        protected virtual float ItemRadius => itemProperties.verticalOffset;
        
        // The layer mask used to detect collisions
        // InteractableObject can only be used for doors and entrances
        protected virtual int LayerMask
        {
            get
            {
                return _layerMask ??= Utils.LayerMasks.ToInt([
                    Utils.LayerMasks.Mask.Default,
                    Utils.LayerMasks.Mask.Room,
                    Utils.LayerMasks.Mask.Colliders,
                    Utils.LayerMasks.Mask.Railing,
                    Utils.LayerMasks.Mask.MiscLevelGeometry,
                    Utils.LayerMasks.Mask.CompanyCruiser,
                    Utils.LayerMasks.Mask.MapHazards,
                    Utils.LayerMasks.Mask.InteractableObject,
                    Utils.LayerMasks.Mask.DecalStickableSurface
                ]);
            }
        }

        // The coefficient of restitution
        protected virtual float BounceCoefficient => 0.2f;
        
        // The force of the throw
        protected virtual float ThrowForce => 20f;
        #endregion
        
        #region Properties
        // The player that threw the item
        public PlayerControllerB? playerThrownBy;

        // The last player that threw the item
        public PlayerControllerB? lastThrower;

        // The trajectory total fall time
        private float _totalFallTime;
        
        // The time the item has been thrown
        private float _throwTime;

        // The initial velocity of the item
        private Vector3 _initialVelocity;
        
        // Layer mask cache
        private int? _layerMask;

        // The collision surface normal vector
        private Vector3? _hitPointNormal;

        // Used to prevent the ball to be thrown with the base code RPC
        private bool _throwCorrectlyInitialized = false;
        #endregion

        #region Patches

        private static ThrowableItem? _lastDroppedItem;

        private static List<ThrowableItem>? _dropAllHeldItems;
        
        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DiscardHeldObject))]
        [HarmonyPrefix]
        private static void DiscardHeldObjectPrefix(PlayerControllerB __instance, bool placeObject = false/*, NetworkObject parentObjectTo = null, Vector3 placePosition = default(Vector3), bool matchRotationOfParent = true*/)
        {
            // If dropped without being thrown, we save the item for the postfix
            if (__instance.currentlyHeldObjectServer is ThrowableItem throwableItem && !placeObject)
            {
                _lastDroppedItem = throwableItem;
            }
        }
        
        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DiscardHeldObject))]
        [HarmonyPostfix]
        private static void DiscardHeldObjectPostfix(PlayerControllerB __instance, bool placeObject = false/*, NetworkObject parentObjectTo = null, Vector3 placePosition = default(Vector3), bool matchRotationOfParent = true*/)
        {
            // If dropped without being thrown, we make it fall to the ground after all the other logic
            if (!placeObject)
            {
                _lastDroppedItem?.FallToGround();
            }
        }
        
        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DropAllHeldItems))]
        [HarmonyPrefix]
        private static void DropAllHeldItemsPrefix(PlayerControllerB __instance, bool itemsFall = true/*, bool disconnecting = false*/)
        {
            if (itemsFall)
            {
                _dropAllHeldItems = __instance.ItemSlots.OfType<ThrowableItem>().ToList();
            }
        }
        
        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DropAllHeldItems))]
        [HarmonyPostfix]
        private static void DropAllHeldItemsPostfix(PlayerControllerB __instance, bool itemsFall = true/*, bool disconnecting = false*/)
        {
            if (itemsFall && _dropAllHeldItems != null)
            {
                foreach (var item in _dropAllHeldItems)
                {
                    item.FallToGround();
                }
            }
        }
        #endregion
        
        #region ThrowRpc
        [ServerRpc(RequireOwnership = false)]
        public void ThrowServerRpc(NetworkObjectReference playerThrownByReference, float totalFallTime, Vector3 initialVelocity, Vector3 hitPointNormal, Vector3 startPosition, Vector3 targetPosition, bool inElevator, bool isInShip)
        {
            ThrowClientRpc(playerThrownByReference, totalFallTime, initialVelocity, hitPointNormal, startPosition, targetPosition, inElevator, isInShip);
        }
    
        [ClientRpc]
        public void ThrowClientRpc(NetworkObjectReference playerThrownByReference, float totalFallTime, Vector3 initialVelocity, Vector3 hitPointNormal, Vector3 startPosition, Vector3 targetPosition, bool inElevator, bool isInShip)
        {
            LethalMon.Log("SendThrowRpc server rpc received");
            if (!playerThrownByReference.TryGet(out NetworkObject playerNetworkObject))
            {
                LethalMon.Log(this.gameObject.name + ": Failed to get player component (SendThrowRpc)", LethalMon.LogType.Error);
                return;
            }

            _throwTime = 0;
            _totalFallTime = totalFallTime;
            _initialVelocity = initialVelocity;
            _hitPointNormal = hitPointNormal == Vector3.zero ? null : hitPointNormal;
            this.startFallingPosition = startPosition;
            this.targetFloorPosition = targetPosition;
            _throwCorrectlyInitialized = true;
            UpdateParent(inElevator, isInShip);
            if (playerNetworkObject.TryGetComponent(out PlayerControllerB player))
            {
                playerThrownBy = player;
                lastThrower = playerThrownBy;
            }
        }
        
        [ServerRpc(RequireOwnership = false)]
        public void FallToGroundServerRpc(float totalFallTime, Vector3 initialVelocity, Vector3 hitPointNormal, Vector3 startPosition, Vector3 targetPosition, bool inElevator, bool isInShip)
        {
            FallToGroundClientRpc(totalFallTime, initialVelocity, hitPointNormal, startPosition, targetPosition, inElevator, isInShip);
        }
        
        [ClientRpc]
        public void FallToGroundClientRpc(float totalFallTime, Vector3 initialVelocity, Vector3 hitPointNormal, Vector3 startPosition, Vector3 targetPosition, bool inElevator, bool isInShip)
        {
            _throwTime = 0;
            _totalFallTime = totalFallTime;
            _initialVelocity = initialVelocity;
            _hitPointNormal = hitPointNormal == Vector3.zero ? null : hitPointNormal;
            this.startFallingPosition = startPosition;
            this.targetFloorPosition = targetPosition;
            UpdateParent(inElevator, isInShip);
            _throwCorrectlyInitialized = true;
        }
        #endregion
        
        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            base.ItemActivate(used, buttonDown);

            if (base.IsOwner)
            {
                playerThrownBy = playerHeldBy;
                lastThrower = playerThrownBy;
                _throwTime = 0;
                hasHitGround = true; // Prevents the base code from calling OnHitGround
                playerHeldBy.DiscardHeldObject(placeObject: true, null, GetItemThrowDestination(ThrowForce, out _initialVelocity, out _totalFallTime, out _hitPointNormal));
                this.ThrowServerRpc(playerThrownBy.GetComponent<NetworkObject>(), _totalFallTime, _initialVelocity, _hitPointNormal ?? Vector3.zero, this.startFallingPosition, this.targetFloorPosition, this.isInElevator, this.isInShipRoom);
            }
        }
        
        public override void FallWithCurve()
        {
            if (!_throwCorrectlyInitialized)
            {
                return;
            }
            
            if (_totalFallTime == 0)
            {
                this.fallTime = 1;
                StopMovingAfterHittingGround();
                return;
            }
            
            _throwTime += Time.deltaTime;
            Vector3 previousPosition = this.transform.localPosition;
            this.transform.localPosition = this.startFallingPosition + _initialVelocity * _throwTime + 0.5f * Gravity * _throwTime * _throwTime;
            this.fallTime = _throwTime / _totalFallTime;
            
            // todo make it rotate
            
            // The item finished falling
            if (this.fallTime >= 1)
            {
                OnHitSurface();

                if (_hitPointNormal != null)
                {
                    Vector3 velocityBefore = (this.transform.localPosition - previousPosition) / Time.deltaTime;
                    Vector3 velocityAfter = velocityBefore - 2 * Vector3.Dot(velocityBefore, _hitPointNormal.Value) * _hitPointNormal.Value;
                    velocityAfter *= BounceCoefficient;

                    // Does it hit the ground with a small velocity magnitude?
                    if (Physics.Raycast(this.transform.position, Vector3.down, out var hitPoint, 30f, LayerMask,
                            QueryTriggerInteraction.Ignore))
                    {
                        if (Vector3.Distance(hitPoint.point, this.transform.position) <= ItemRadius + Mathf.Abs(TimeStep * Gravity.y) && velocityAfter.magnitude < 0.5f)
                        {
                            StopMovingAfterHittingGround();
                            return;
                        }
                    }

                    // Make it bounce
                    this.fallTime = 0;
                    this._initialVelocity = velocityAfter;
                    this._throwTime = 0;
                    this.targetFloorPosition = GetSphereProjectileCollisionPoint(this.transform.position, _initialVelocity, Gravity, MaxFallTime, TimeStep, ItemRadius, out _totalFallTime, out _hitPointNormal);
                    UpdateParent();
                    this.targetFloorPosition = !isInElevator ? StartOfRound.Instance.propsContainer.InverseTransformPoint(this.targetFloorPosition) : StartOfRound.Instance.elevatorTransform.InverseTransformPoint(this.targetFloorPosition);
                    this.startFallingPosition = this.transform.localPosition;
                    return;
                }
                
                StopMovingAfterHittingGround();
            }
        }

        public new void FallToGround(bool randomizePosition = false)
        {
            this.fallTime = 0;
            this._throwTime = 0;
            this.startFallingPosition = this.transform.localPosition;
            this._initialVelocity = Vector3.zero;
            this.targetFloorPosition = GetSphereProjectileCollisionPoint(this.transform.position, Vector3.zero, Gravity,
                MaxFallTime, TimeStep, ItemRadius, out _totalFallTime, out _hitPointNormal);
            UpdateParent();
            this.targetFloorPosition = !isInElevator ? StartOfRound.Instance.propsContainer.InverseTransformPoint(this.targetFloorPosition) : StartOfRound.Instance.elevatorTransform.InverseTransformPoint(this.targetFloorPosition);
            this.FallToGroundServerRpc(_totalFallTime, _initialVelocity, _hitPointNormal ?? Vector3.zero, this.startFallingPosition, this.targetFloorPosition, this.isInElevator, this.isInShipRoom);
        }

        public void OnHitSurface()
        {
            PlayDropSFX();
        }

        private void UpdateParent()
        {
            bool inElevator = StartOfRound.Instance.shipBounds.bounds.Contains(this.transform.position);
            UpdateParent(inElevator, inElevator && StartOfRound.Instance.shipInnerRoomBounds.bounds.Contains(this.transform.position));
        }
        
        private void UpdateParent(bool inElevator, bool inShipRoom)
        {
            if (inElevator)
            {
                base.transform.SetParent(StartOfRound.Instance.elevatorTransform, worldPositionStays: true);
                this.isInElevator = true;
                this.isInShipRoom = inShipRoom;
            }
            else
            {
                base.transform.SetParent(StartOfRound.Instance.propsContainer, worldPositionStays: true);
                this.isInElevator = false;
                this.isInShipRoom = false;
            }
        }
        
        private void StopMovingAfterHittingGround()
        {
            LethalMon.Log("Stop moving");
            
            OnHitGround();
            
            this.playerThrownBy = null;

            UpdateParent();
            
            GameNetworkManager.Instance.localPlayerController.SetItemInElevator(this.isInElevator, this.isInShipRoom, this);
            
            hasHitGround = true;
            _throwCorrectlyInitialized = false;
        }
        
        private Vector3 GetSphereProjectileCollisionPoint(Vector3 startPosition, Vector3 initialVelocity, Vector3 gravity, float maxTime, float timeStep, float radius, out float totalFallTime, out Vector3? hitPointNormal)
        {
            Vector3 previousPosition = startPosition;

            for (float t = timeStep; t < maxTime; t += timeStep)
            {
                Vector3 newPosition = startPosition + initialVelocity * t + 0.5f * gravity * t * t;
                
#if DEBUG
                // Really useful for debugging trajectories
                /*
                var lineRenderer = new GameObject("Line").AddComponent<LineRenderer>();
                lineRenderer.startColor = Color.red;
                lineRenderer.endColor = Color.red;
                lineRenderer.startWidth = 0.01f;
                lineRenderer.endWidth = 0.01f;
                lineRenderer.positionCount = 2;
                lineRenderer.useWorldSpace = true;    
                lineRenderer.SetPosition(0, previousPosition);
                lineRenderer.SetPosition(1, newPosition);
                */
#endif    
                
                // Check if we hit a collider
                if (Physics.Raycast(previousPosition, (newPosition - previousPosition).normalized, out var hitPoint, Vector3.Distance(previousPosition, newPosition), LayerMask, QueryTriggerInteraction.Ignore))
                {
                    var interactTrigger = hitPoint.collider.GetComponentInParent<InteractTrigger>();
                    var entrance = hitPoint.collider.GetComponentInParent<EntranceTeleport>();
                    if (hitPoint.collider.gameObject.layer != (int) Utils.LayerMasks.Mask.InteractableObject || entrance != null || (interactTrigger != null && FindObjectsOfType<DoorLock>().Any(dl => dl.doorTrigger == interactTrigger)))
                    {
                        // We hit something, now we go back until the distance between this position and another on the curve is more than the radius
                        hitPointNormal = hitPoint.normal;
                        for (float i = t - timeStep; i > 0; i -= timeStep)
                        {
                            var goBackPosition = startPosition + initialVelocity * i + 0.5f * gravity * i * i;
                            float distanceToHitPlane =
                                Mathf.Abs(Vector3.Dot(hitPoint.normal, hitPoint.point - goBackPosition));
                            if (distanceToHitPlane > radius)
                            {
                                totalFallTime = i;
                                return goBackPosition;
                            }
                        }

                        // We reached the beginning of the curve, so we return the start position
                        totalFallTime = 0;
                        return startPosition;
                    }
                }
                
                previousPosition = newPosition;
            }
            
            // No collider found, don't look farther than the max time
            totalFallTime = maxTime;
            hitPointNormal = null;
            return previousPosition;
        }
        
        private Vector3 GetItemThrowDestination(float force, out Vector3 initialVelocity, out float totalFallTime, out Vector3? hitPointNormal)
        {
            Vector3 playerVelocity = playerHeldBy.oldPlayerPosition - playerHeldBy.transform.localPosition;
        
            initialVelocity = (playerHeldBy.gameplayCamera.transform.forward + playerVelocity) * force;
            
            Vector3 startPosition = this.transform.parent == null ? this.transform.localPosition : this.transform.parent.TransformPoint(this.transform.localPosition);
            return GetSphereProjectileCollisionPoint(startPosition, initialVelocity, Gravity, MaxFallTime, TimeStep, ItemRadius, out totalFallTime, out hitPointNormal);
        }
    }
}