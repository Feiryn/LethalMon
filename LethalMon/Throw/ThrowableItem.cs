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
                    Utils.LayerMasks.Mask.InteractableObject,
                    Utils.LayerMasks.Mask.PhysicsObject,
                    Utils.LayerMasks.Mask.Terrain,
                    Utils.LayerMasks.Mask.PlaceableShipObjects,
                    Utils.LayerMasks.Mask.PlacementBlocker,
                    Utils.LayerMasks.Mask.CompanyCruiser,
                    
                ]);
            }
        }

        #endregion
        
        #region Properties
        public PlayerControllerB? playerThrownBy;

        public PlayerControllerB? lastThrower;

        private float _totalFallTime;
        
        private float _throwTime;

        private Vector3 _initialVelocity;
        
        private int? _layerMask;
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
                playerHeldBy.DiscardHeldObject(placeObject: true, null, GetItemThrowDestination(20f, out _initialVelocity, out _totalFallTime));
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
            this.transform.localPosition = this.startFallingPosition + _initialVelocity * _throwTime + 0.5f * Gravity * _throwTime * _throwTime;
            this.fallTime = _throwTime / _totalFallTime;
            
            LethalMon.Log("Throw time: " + _throwTime + ", total fall time: " + _totalFallTime + ", fall time: " + this.fallTime);
            
            // todo make it bounce or slide maybe
            // todo make it rotate
            
            if (this.fallTime >= 1)
            {
                if (Physics.Raycast(this.transform.position, Vector3.down, out var hitInfo, 30f, LayerMask, QueryTriggerInteraction.Ignore))
                {
                    if (hitInfo.distance > ItemRadius - TimeStep * Gravity.y)
                    {
                        // Make it fall
                        this.fallTime = 0;
                        this.startFallingPosition = this.transform.localPosition;
                        this._initialVelocity = Vector3.zero;
                        this._throwTime = 0;
                        this._totalFallTime = Mathf.Sqrt(Mathf.Abs((hitInfo.distance - ItemRadius) / (0.5f * Gravity.y)));
                        this.targetFloorPosition = this.startFallingPosition + _initialVelocity * this._totalFallTime + 0.5f * Gravity * this._totalFallTime * this._totalFallTime;
                        return;
                    }
                }
                
                this.playerThrownBy = null;
            }
            
            /*
            float magnitude = (this.startFallingPosition - this.targetFloorPosition).magnitude;
            this.transform.rotation = Quaternion.Lerp(this.transform.rotation, Quaternion.Euler(this.itemProperties.restingRotation.x, this.transform.eulerAngles.y, this.itemProperties.restingRotation.z), 14f * Time.deltaTime / magnitude);
            this.transform.localPosition = Vector3.Lerp(this.startFallingPosition, this.targetFloorPosition, FallCurve.fallCurve.Evaluate(this.fallTime));
            if (magnitude > 5f)
            {
                this.transform.localPosition = Vector3.Lerp(new Vector3(this.transform.localPosition.x, this.startFallingPosition.y, this.transform.localPosition.z), new Vector3(this.transform.localPosition.x, this.targetFloorPosition.y, this.transform.localPosition.z), FallCurve.verticalFallCurveNoBounce.Evaluate(this.fallTime));
            }
            else
            {
                this.transform.localPosition = Vector3.Lerp(new Vector3(this.transform.localPosition.x, this.startFallingPosition.y, this.transform.localPosition.z), new Vector3(this.transform.localPosition.x, this.targetFloorPosition.y, this.transform.localPosition.z), FallCurve.verticalFallCurve.Evaluate(this.fallTime));
            }
            this.fallTime += Mathf.Abs(Time.deltaTime * 12f / magnitude);
            
            if (this.fallTime > 1)
            {
                this.TouchGround();
                this.playerThrownBy = null;
            }
            */
        }

        private IEnumerator DebugThrowCoroutine(List<Vector3> positions)
        {
            List<GameObject> debugBalls = new List<GameObject>();
            
            foreach (var position in positions)
            {
                GameObject? debugBall = Instantiate(Pokeball.SpawnPrefab);
                if (debugBall != null)
                {
                    debugBall.GetComponent<GrabbableObject>().itemProperties.itemSpawnsOnGround = false;
                    debugBalls.Add(debugBall);
                    debugBall.transform.localScale *= 0.2f;
                    debugBall.transform.localPosition = position;
                }
            }
            
            yield return new WaitForSeconds(15f);
            
            foreach (var debugBall in debugBalls)
            {
                Destroy(debugBall);
            }
        }

        private Vector3 GetSphereProjectileCollisionPoint(Vector3 startPosition, Vector3 initialVelocity, Vector3 gravity, float maxTime, float timeStep, float radius, out float totalFallTime)
        {
            Vector3 previousPosition = startPosition;
            
#if DEBUG
            List<Vector3> positions = [];
#endif

            for (float t = timeStep; t < maxTime; t += timeStep)
            {
                Vector3 newPosition = startPosition + initialVelocity * t + 0.5f * gravity * t * t;
                
#if DEBUG
                positions.Add(newPosition);
#endif

                if (Physics.Linecast(previousPosition, newPosition, LayerMask, QueryTriggerInteraction.Ignore))
                {
                    // We hit something, now we go back until the distance between this position and another on the curve is more than the radius
                    Vector3 hitPosition = newPosition;

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
                
#if DEBUG
                //StartCoroutine(DebugThrowCoroutine(positions));
#endif
                previousPosition = newPosition;
            }
            
#if DEBUG
            //StartCoroutine(DebugThrowCoroutine(positions));
#endif
            
            // No collider found, don't look farther than the max time
            totalFallTime = maxTime;
            return previousPosition;
        }
        
        private Vector3 GetItemThrowDestination(float force, out Vector3 initialVelocity, out float totalFallTime)
        {
            Vector3 playerVelocity = playerHeldBy.oldPlayerPosition - playerHeldBy.transform.position;
            
            initialVelocity = (playerHeldBy.gameplayCamera.transform.forward + playerVelocity) * force;
            
            return GetSphereProjectileCollisionPoint(this.transform.localPosition, initialVelocity, Gravity, MaxFallTime, TimeStep, ItemRadius, out totalFallTime);
        }
    }
}