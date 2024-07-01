using System.Reflection;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.Throw
{
    public abstract class ThrowableItem : GrabbableObject
    {
        #region ThrowRpc

        [ServerRpc(RequireOwnership = false)]
        public void ThrowServerRpc(NetworkObjectReference playerThrownByReference)
        {
            ThrowClientRpc(playerThrownByReference);
        }
    
        [ClientRpc]
        public void ThrowClientRpc(NetworkObjectReference playerThrownByReference)
        {
            Debug.Log("SendThrowRpc server rpc received");
            if (!playerThrownByReference.TryGet(out NetworkObject playerNetworkObject))
            {
                Debug.LogError(this.gameObject.name + ": Failed to get player component (SendThrowRpc)");
                return;
            }

            if(playerNetworkObject.TryGetComponent( out PlayerControllerB player))
                playerThrownBy = player;
        }
        #endregion
        
        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            base.ItemActivate(used, buttonDown);
            if (base.IsOwner)
            {
                playerThrownBy = playerHeldBy;
                playerHeldBy.DiscardHeldObject(placeObject: true, null, GetItemThrowDestination());
                this.ThrowServerRpc(playerThrownBy.GetComponent<NetworkObject>());
            }
        }

        public override void EquipItem()
        {
            EnableItemMeshes(enable: true);
            isPocketed = false;
        }

        public abstract void TouchGround();
        
        public override void FallWithCurve()
        {
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
        }


        public override void Update()
        {
            base.Update();
        }

        public Vector3 GetItemThrowDestination()
        {
            Vector3 position = base.transform.position;
            Debug.DrawRay(playerHeldBy.gameplayCamera.transform.position, playerHeldBy.gameplayCamera.transform.forward, Color.yellow, 15f);
            itemThrowRay = new Ray(playerHeldBy.gameplayCamera.transform.position, playerHeldBy.gameplayCamera.transform.forward);
            position = ((!Physics.Raycast(itemThrowRay, out itemHit, 12f, 268437761, QueryTriggerInteraction.Ignore)) ? itemThrowRay.GetPoint(10f) : itemThrowRay.GetPoint(itemHit.distance - 0.05f));
            Debug.DrawRay(position, Vector3.down, Color.blue, 15f);
            itemThrowRay = new Ray(position, Vector3.down);
            if (Physics.Raycast(itemThrowRay, out itemHit, 30f, 268437761, QueryTriggerInteraction.Ignore))
            {
                return itemHit.point + Vector3.up * (this.itemProperties.verticalOffset + 0.05f);
            }
            return itemThrowRay.GetPoint(30f);
        }

        public AnimationCurve itemFallCurve;

        public AnimationCurve itemVerticalFallCurve;

        public AnimationCurve itemVerticalFallCurveNoBounce;

        public RaycastHit itemHit;

        public Ray itemThrowRay;

        public PlayerControllerB playerThrownBy;
    }
}