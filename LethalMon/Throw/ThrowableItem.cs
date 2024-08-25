using System.Collections;
using System.Collections.Generic;
using GameNetcodeStuff;
using LethalMon.Items;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.Throw
{
    public abstract class ThrowableItem : GrabbableObject
    {
        #region Parameters
        protected virtual float MaxFallTime => 30f;

        protected virtual float TimeStep => 0.01f;
        
        protected virtual Vector3 Gravity => Physics.gravity;
        
        protected virtual float ItemRadius => itemProperties.verticalOffset;
        
        protected virtual int LayerMask
        {
            get
            {
                return _layerMask ??= Utils.LayerMasks.ToInt([
                    Utils.LayerMasks.Mask.Default,
                    Utils.LayerMasks.Mask.Room,
                    Utils.LayerMasks.Mask.Colliders,
                    Utils.LayerMasks.Mask.Railing,
                    /*Utils.LayerMasks.Mask.InteractableObject,
                    Utils.LayerMasks.Mask.PhysicsObject,
                    Utils.LayerMasks.Mask.Terrain,
                    Utils.LayerMasks.Mask.PlaceableShipObjects,
                    Utils.LayerMasks.Mask.PlacementBlocker,
                    Utils.LayerMasks.Mask.CompanyCruiser,*/
                    
                ]);
            }
        }

        protected virtual float BounceCoefficient => 0.2f;
        
        protected virtual float ThrowForce => 20f;
        #endregion
        
        #region Properties
        public PlayerControllerB? playerThrownBy;

        public PlayerControllerB? lastThrower;

        private float _totalFallTime;
        
        private float _throwTime;

        private Vector3 _initialVelocity;
        
        private int? _layerMask;

        private Vector3? _hitPointNormal;
        #endregion

        #region ThrowRpc

        [ServerRpc(RequireOwnership = false)]
        public void ThrowServerRpc(NetworkObjectReference playerThrownByReference)
        {
            ThrowClientRpc(playerThrownByReference);
        }
    
        [ClientRpc]
        public void ThrowClientRpc(NetworkObjectReference playerThrownByReference)
        {
            LethalMon.Log("SendThrowRpc server rpc received");
            if (!playerThrownByReference.TryGet(out NetworkObject playerNetworkObject))
            {
                LethalMon.Log(this.gameObject.name + ": Failed to get player component (SendThrowRpc)", LethalMon.LogType.Error);
                return;
            }

            if (playerNetworkObject.TryGetComponent(out PlayerControllerB player))
            {
                playerThrownBy = player;
                lastThrower = playerThrownBy;
            }
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
                playerHeldBy.DiscardHeldObject(placeObject: true, null, GetItemThrowDestination(ThrowForce, out _initialVelocity, out _totalFallTime, out _hitPointNormal));
                this.ThrowServerRpc(playerThrownBy.GetComponent<NetworkObject>());
            }
        }

        // public abstract void TouchGround();
        
        public override void FallWithCurve()
        {
            if (_totalFallTime == 0)
            {
                this.fallTime = 1;
                return;
            }
            
            _throwTime += Time.deltaTime;
            Vector3 previousPosition = this.transform.localPosition;
            this.transform.localPosition = this.startFallingPosition + _initialVelocity * _throwTime + 0.5f * Gravity * _throwTime * _throwTime;
            this.fallTime = _throwTime / _totalFallTime;
            
            // todo make it rotate
            
            if (this.fallTime >= 1)
            {
                // todo play hit song

                if (_hitPointNormal != null)
                {
                    Vector3 velocityBefore = (this.transform.localPosition - previousPosition) / Time.deltaTime;
                    Vector3 velocityAfter = velocityBefore - 2 * Vector3.Dot(velocityBefore, _hitPointNormal.Value) * _hitPointNormal.Value;
                    velocityAfter *= BounceCoefficient;

                    // Does it hit the ground with a small velocity magnitude?
                    LethalMon.Log("Velocity after magnitude: " + velocityAfter.magnitude);
                    if (Physics.Raycast(this.transform.localPosition, Vector3.down, out var hitPoint, 30f, LayerMask,
                            QueryTriggerInteraction.Ignore))
                    {
                        if (Vector3.Distance(hitPoint.point, this.transform.localPosition) <= ItemRadius + TimeStep * Gravity.y && velocityAfter.magnitude < 0.5f)
                        {
                            this.playerThrownBy = null;
                            return;
                        }
                    }

                    // Make it bounce
                    this.startFallingPosition = this.transform.localPosition;
                    this.fallTime = 0;
                    this._initialVelocity = velocityAfter;
                    this._throwTime = 0;
                    GetSphereProjectileCollisionPoint(this.transform.position, _initialVelocity, Gravity, MaxFallTime, TimeStep, ItemRadius, out _totalFallTime, out _hitPointNormal);
                    this.targetFloorPosition = this.startFallingPosition + _initialVelocity * _totalFallTime + 0.5f * Gravity * _totalFallTime * _totalFallTime;
                    this.startFallingPosition = base.transform.parent.InverseTransformPoint(base.transform.position);
                }

                this.playerThrownBy = null;
            }
        }

        private Vector3 GetSphereProjectileCollisionPoint(Vector3 startPosition, Vector3 initialVelocity, Vector3 gravity, float maxTime, float timeStep, float radius, out float totalFallTime, out Vector3? hitPointNormal)
        {
            Vector3 previousPosition = startPosition;

            for (float t = timeStep; t < maxTime; t += timeStep)
            {
                Vector3 newPosition = startPosition + initialVelocity * t + 0.5f * gravity * t * t;
                
#if DEBUG
                var lineRenderer = new GameObject("Line").AddComponent<LineRenderer>();
                lineRenderer.startColor = Color.red;
                lineRenderer.endColor = Color.red;
                lineRenderer.startWidth = 0.01f;
                lineRenderer.endWidth = 0.01f;
                lineRenderer.positionCount = 2;
                lineRenderer.useWorldSpace = true;    
                lineRenderer.SetPosition(0, previousPosition);
                lineRenderer.SetPosition(1, newPosition);
#endif    


                if (Physics.Raycast(previousPosition, (newPosition - previousPosition).normalized, out var hitPoint, Vector3.Distance(previousPosition, newPosition), LayerMask, QueryTriggerInteraction.Ignore))
                {
                    // We hit something, now we go back until the distance between this position and another on the curve is more than the radius
                    Vector3 hitPosition = newPosition;
                    hitPointNormal = hitPoint.normal;
                    for (float i = t - timeStep; i > 0; i -= timeStep)
                    {
                        var goBackPosition = startPosition + initialVelocity * i + 0.5f * gravity * i * i;
                        if (Vector3.Distance(hitPosition, goBackPosition) > radius)
                        {
                            totalFallTime = i;
                            return goBackPosition;
                        }
                    }
                    
                    // We reached the beginning of the curve, so we return the start position
                    totalFallTime = 0;
                    return startPosition;
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
            Vector3 playerVelocity = playerHeldBy.oldPlayerPosition - playerHeldBy.transform.position;
            
            initialVelocity = (playerHeldBy.gameplayCamera.transform.forward + playerVelocity) * force;
            
            return GetSphereProjectileCollisionPoint(this.transform.localPosition, initialVelocity, Gravity, MaxFallTime, TimeStep, ItemRadius, out totalFallTime, out hitPointNormal);
        }
    }
}