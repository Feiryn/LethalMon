using System.Collections.Generic;
using System.Reflection;
using GameNetcodeStuff;
using LethalMon.AI;
using LethalMon.Items;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.Throw
{
    public abstract class ThrowableItem : GrabbableObject
    {
        #region ThrowRpc

        internal static void InitializeRPCS()
        {
            NetworkManager.__rpc_func_table.Add(2055230283u, __rpc_handler_2055230283);
        }
        
        public void SendThrowPacket(NetworkObjectReference throwableItemNetworkObjectReference, NetworkObjectReference playerNetworkObjectReference)
        {
            ServerRpcParams rpcParams = default(ServerRpcParams);
            FastBufferWriter writer = this.__beginSendServerRpc(2055230283u, rpcParams, RpcDelivery.Reliable);
            writer.WriteValueSafe(in throwableItemNetworkObjectReference);
            writer.WriteValueSafe(in playerNetworkObjectReference);
            this.__endSendServerRpc(ref writer, 2055230283u, rpcParams, RpcDelivery.Reliable);
            Debug.Log("SendThrowPacket server rpc send finished");
        }
    
        [ServerRpc]
        public void SendThrowRpc(NetworkObjectReference throwableItemNetworkObjectReference, NetworkObjectReference playerNetworkObjectReference)
        {
            Debug.Log("SendThrowRpc server rpc received");
            if (throwableItemNetworkObjectReference.TryGet(out NetworkObject grabbableNetworkObject) && playerNetworkObjectReference.TryGet(out NetworkObject playerNetworkObject))
            {
                ThrowableItem throwableItem = grabbableNetworkObject.GetComponent<ThrowableItem>();
                PlayerControllerB player = playerNetworkObject.GetComponent<PlayerControllerB>();

                if (throwableItem != null && player != null)
                {
                    throwableItem.playerThrownBy = player;
                }
                else
                {
                    Debug.LogError(this.gameObject.name + ": Failed to get throwable item or player component (SendThrowRpc)");
                }
            }
            else
            {
                Debug.LogError(this.gameObject.name + ": Failed to get network object from network object reference (SendThrowRpc)");
            }
        }

        private static void __rpc_handler_2055230283(NetworkBehaviour target, FastBufferReader reader,
            __RpcParams rpcParams)
        {
            NetworkManager networkManager = target.NetworkManager;
            if (networkManager != null && networkManager.IsListening)
            {
                Debug.Log("Execute RPC handler " + MethodBase.GetCurrentMethod().Name);
                reader.ReadValueSafe(out NetworkObjectReference throwableItemNetworkObjectReference);
                reader.ReadValueSafe(out NetworkObjectReference playerNetworkObjectReference);
                ((ThrowableItem) target).SendThrowRpc(throwableItemNetworkObjectReference, playerNetworkObjectReference);
            }
        }
        
        #endregion
        
        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            base.ItemActivate(used, buttonDown);
            if (base.IsOwner)
            {
                playerThrownBy = playerHeldBy;
                playerHeldBy.DiscardHeldObject(placeObject: true, null, GetItemThrowDestination());
                this.SendThrowPacket(this.GetComponent<NetworkObject>(), playerThrownBy.GetComponent<NetworkObject>());
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
            // borrowed (and slightly modified) from lethal company
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
            position = ((!Physics.Raycast(itemThrowRay, out itemHit, 12f, StartOfRound.Instance.collidersAndRoomMaskAndDefault)) ? itemThrowRay.GetPoint(10f) : itemThrowRay.GetPoint(itemHit.distance - 0.05f));
            Debug.DrawRay(position, Vector3.down, Color.blue, 15f);

            // check if position is inside a collider, and position below is not
            if (Physics.OverlapSphere(position, 0.02f, StartOfRound.Instance.collidersAndRoomMaskAndDefault).Length > 0)
            {
                if(Physics.OverlapSphere(position + (Vector3.down * 0.05f), 0.02f, StartOfRound.Instance.collidersAndRoomMaskAndDefault).Length == 0)
                {
                    // set new position
                    position += (Vector3.down * 0.05f);
                }
            }

            itemThrowRay = new Ray(position, Vector3.down);
            if (Physics.Raycast(itemThrowRay, out itemHit, 30f, StartOfRound.Instance.collidersAndRoomMaskAndDefault))
            {
                return itemHit.point + Vector3.up * this.itemProperties.verticalOffset;
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