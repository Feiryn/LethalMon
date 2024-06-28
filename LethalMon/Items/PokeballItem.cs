using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using LethalMon.AI;
using LethalMon.Throw;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LethalMon.Items;

public abstract class PokeballItem : ThrowableItem
{
    private EnemyAI? enemyAI = null;
    
    private EnemyType? enemyType = null;

    private CatchableEnemy.CatchableEnemy? catchableEnemy = null;

    private bool captureSuccess = false;

    private int captureRounds = 1;

    private int currentCaptureRound = 0;

    private bool enemyCaptured = false;

    private int captureStrength;

    private BallType ballType;
    
    public PokeballItem(BallType ballType, int captureStrength)
    {
        this.ballType = ballType;
        this.captureStrength = captureStrength;
    }

    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        if (StartOfRound.Instance.shipHasLanded || StartOfRound.Instance.testRoom != null)
        {
            base.ItemActivate(used, buttonDown);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Collided with " + other.gameObject.name);

        Debug.Log("Pokeball has an enemy captured: " + this.enemyCaptured);
        Debug.Log("Pokeball was thrown by: " + this.playerThrownBy);
        
        if ((this.NetworkManager.IsHost || this.NetworkManager.IsServer) && !this.enemyCaptured && this.playerThrownBy != null)
        {
            EnemyAI? enemyToCapture = other.GetComponentInParent<EnemyAI>();
            if (enemyToCapture != null)
            {
                if (Data.CatchableMonsters.TryGetValue(enemyToCapture.enemyType.name,
                        out CatchableEnemy.CatchableEnemy catchable))
                {
                    this.CaptureEnemy(enemyToCapture, catchable);   
                }
                else
                {
                    Debug.Log(enemyToCapture.enemyType.name + " is not catchable");
                }
            }
        }
    }
    
    internal static void InitializeRPCS()
    {
        NetworkManager.__rpc_func_table.Add(1173420115u, __rpc_handler_1173420115);
        NetworkManager.__rpc_func_table.Add(291057008u, __rpc_handler_291057008);
        NetworkManager.__rpc_func_table.Add(626404720u, __rpc_handler_626404720);
    }

    #region CaptureAnimation

    public void SendPlayCaptureAnimationPacket(NetworkObjectReference enemy, int roundsNumber, bool catchSuccess)
    {
        ClientRpcParams rpcParams = default(ClientRpcParams);
        FastBufferWriter writer = this.__beginSendClientRpc(1173420115u, rpcParams, RpcDelivery.Reliable);
        writer.WriteValueSafe(in enemy);
        writer.WriteValueSafe(roundsNumber);
        writer.WriteValueSafe(catchSuccess ? 1 : 0);
        this.__endSendClientRpc(ref writer, 1173420115u, rpcParams, RpcDelivery.Reliable);
        Debug.Log("Play capture animation client rpc send finished");
    }
    
    [ClientRpc]
    public void PlayCaptureAnimationClientRpc(NetworkObjectReference enemy, int roundsNumber, bool catchSuccess)
    {
        Debug.Log("Play capture animation client rpc received");
        
        if (enemy.TryGet(out NetworkObject enemyAINetworkObject))
        {
            this.enemyAI = enemyAINetworkObject.gameObject.GetComponent<EnemyAI>();
            this.enemyType = this.enemyAI.enemyType;
            this.captureSuccess = catchSuccess;
            this.captureRounds = roundsNumber;
            this.currentCaptureRound = 0;
            this.PlayCaptureAnimation();
        }
        else
        {
            Debug.LogError(this.gameObject.name + ": Failed to get network object from network object reference (Capture animation RPC)");
        }
    }

    public void PlayCaptureAnimation()
    {
        this.targetFloorPosition = this.transform.localPosition; // Stop moving
        this.currentCaptureRound = 0;
        this.grabbable = false; // Make it ungrabbable
        this.grabbableToEnemies = false;
        
        this.enemyAI!.gameObject.SetActive(false); // Hide enemy

        this.PlayCaptureAnimationAnimator();
    }

    public void PlayCaptureAnimationAnimator()
    {
        Animator animator = this.GetComponent<Animator>(); // Play capture animation
        animator.Play("Base Layer.Capture", 0);
    }
    
    private void CaptureEnemy(EnemyAI enemyAI, CatchableEnemy.CatchableEnemy catchable)
    {
        Debug.Log("Start to capture " + enemyAI.name);
        this.playerThrownBy = null;
        this.catchableEnemy = catchable;
        this.enemyAI = enemyAI;
        this.enemyType = enemyAI.enemyType;
        double captureProbability = catchable.GetCaptureProbability(this.captureStrength);
        this.captureSuccess = Data.Random.NextDouble() < captureProbability;
        this.captureRounds = this.captureSuccess ? 3 : Data.Random.Next(1, 4);
        
        SendPlayCaptureAnimationPacket(this.enemyAI.GetComponent<NetworkObject>(), this.captureRounds, this.captureSuccess);
    }

    public void CaptureEnd(string message)
    {
        Debug.Log("Capture animation end");

        // Test if we need to play the animation more times
        if (this.currentCaptureRound + 1 < this.captureRounds)
        {
            Debug.Log("Play the animation again");
            
            this.currentCaptureRound++;
            PlayCaptureAnimationAnimator();
        }
        else if (this.captureSuccess)
        {
            Debug.Log("Capture success");

            this.SetCaughtEnemy(this.enemyType);
            
            this.FallToGround();
            this.playerThrownBy = null;
            this.grabbable = true;
            this.grabbableToEnemies = true;
        }
        else
        {
            Debug.Log("Capture failed");

            if (base.NetworkManager.IsServer || base.NetworkManager.IsHost)
            {
                this.GetComponent<NetworkObject>().Despawn(true);
            }
        }
    }

    public override void OnDestroy()
    {
        if (!this.captureSuccess && !this.enemyCaptured)
        {
            if (this.enemyAI != null)
            {
                this.enemyAI.gameObject.SetActive(true); // Show enemy

                if (base.NetworkManager.IsServer || base.NetworkManager.IsHost)
                {
                    this.catchableEnemy!.CatchFailBehaviour(this.enemyAI!, this.playerThrownBy);
                }
            }
        }
        
        base.OnDestroy();
    }

    private static void __rpc_handler_1173420115(NetworkBehaviour target, FastBufferReader reader,
        __RpcParams rpcParams)
    {
        NetworkManager networkManager = target.NetworkManager;
        if (networkManager != null && networkManager.IsListening)
        {
            Debug.Log("Execute RPC handler " + MethodBase.GetCurrentMethod().Name);
            Traverse rpcExecStage = Traverse.Create(target).Field("__rpc_exec_stage");
            reader.ReadValueSafe(out NetworkObjectReference enemy);
            reader.ReadValueSafe(out int roundsNumber);
            reader.ReadValueSafe(out int captureSuccess);
            rpcExecStage.SetValue(__RpcExecStage.Client);
            ((PokeballItem) target).PlayCaptureAnimationClientRpc(enemy, roundsNumber, captureSuccess == 1);
            rpcExecStage.SetValue(__RpcExecStage.None);
        }
    }
    
    #endregion
    
    #region CustomAiSpawning

    public void SendReplaceWithCustomAiPacket(NetworkObjectReference networkObjectReference, string customAiName, ulong ownerClientId)
    {
        ClientRpcParams rpcParams = default(ClientRpcParams);
        FastBufferWriter writer = this.__beginSendClientRpc(291057008u, rpcParams, RpcDelivery.Reliable);
        writer.WriteValueSafe(in networkObjectReference);
        writer.WriteValueSafe(customAiName);
        writer.WriteValueSafe(ownerClientId);
        this.__endSendClientRpc(ref writer, 291057008u, rpcParams, RpcDelivery.Reliable);
        Debug.Log("ReplaceWithCustomAi client rpc send finished");
    }
    
    [ClientRpc]
    public void ReplaceWithCustomAiClientRpc(NetworkObjectReference networkObjectReference, string customAiName, ulong ownerClientId)
    {
        Debug.Log("ReplaceWithCustomAi client rpc received");
        if (networkObjectReference.TryGet(out NetworkObject networkObject))
        {
            if (Data.CatchableMonsters.TryGetValue(customAiName, out CatchableEnemy.CatchableEnemy catchableEnemy))
            {
                if (networkObject.gameObject.TryGetComponent(out TamedEnemyBehaviour tamedBehaviour))
                {
                    tamedBehaviour.SwitchToCustomBehaviour(TamedEnemyBehaviour.CustomBehaviour.TamedFollowing);
                    tamedBehaviour.ownClientId = ownerClientId;
                }
            }
            else
            {
                Debug.Log("Custom AI name not found (maybe mod version mismatch)");
            }
        }
        else
        {
            Debug.LogError(this.gameObject.name + ": Failed to get network object from network object reference (Capture animation RPC)");
        }
    }

    private static void __rpc_handler_291057008(NetworkBehaviour target, FastBufferReader reader,
        __RpcParams rpcParams)
    {
        NetworkManager networkManager = target.NetworkManager;
        if (networkManager != null && networkManager.IsListening && !(networkManager.IsServer || networkManager.IsHost))
        {
            Debug.Log("Execute RPC handler " + MethodBase.GetCurrentMethod().Name);
            reader.ReadValueSafe(out NetworkObjectReference networkObjectReference);
            reader.ReadValueSafe(out string customAiName);
            reader.ReadValueSafe(out ulong ownerClientId);
            ((PokeballItem) target).ReplaceWithCustomAiClientRpc(networkObjectReference, customAiName, ownerClientId);
        }
    }
    
    #endregion

    #region SyncContent

    public void SyncContentPacket(NetworkObjectReference networkObjectReference, string enemyTypeName)
    {
        ClientRpcParams rpcParams = default(ClientRpcParams);
        FastBufferWriter writer = this.__beginSendClientRpc(626404720u, rpcParams, RpcDelivery.Reliable);
        writer.WriteValueSafe(in networkObjectReference);
        writer.WriteValueSafe(enemyTypeName);
        this.__endSendClientRpc(ref writer, 626404720u, rpcParams, RpcDelivery.Reliable);
        Debug.Log("SyncContentPacket client rpc send finished");
    }
    
    [ClientRpc]
    public void SyncContentClientRpc(NetworkObjectReference networkObjectReference, string enemyTypeName)
    {
        Debug.Log("SyncContentPacket client rpc received");
        if (networkObjectReference.TryGet(out NetworkObject networkObject))
        {
            PokeballItem pokeballItem = networkObject.GetComponent<PokeballItem>();
            EnemyType enemyType = Resources.FindObjectsOfTypeAll<EnemyType>().First(type => type.name == enemyTypeName);
            pokeballItem.SetCaughtEnemy(enemyType);
        }
        else
        {
            Debug.LogError(this.gameObject.name + ": Failed to get network object from network object reference (SyncContentPacket RPC)");
        }
    }

    private static void __rpc_handler_626404720(NetworkBehaviour target, FastBufferReader reader,
        __RpcParams rpcParams)
    {
        NetworkManager networkManager = target.NetworkManager;
        if (networkManager != null && networkManager.IsListening && !(networkManager.IsServer || networkManager.IsHost))
        {
            Debug.Log("Execute RPC handler " + MethodBase.GetCurrentMethod().Name);
            reader.ReadValueSafe(out NetworkObjectReference networkObjectReference);
            reader.ReadValueSafe(out string enemyTypeName);
            ((PokeballItem) target).SyncContentClientRpc(networkObjectReference, enemyTypeName);
        }
    }
    
    #endregion
    
    public override void TouchGround()
    {
        Debug.Log("Touch ground");

        if (base.IsHost && this.playerThrownBy != null && this.enemyCaptured)
        {
            LethalMon.Logger.LogInfo("Getting pet..");
            if (Utils.GetPlayerPet(this.playerThrownBy) != null)
            {
                LethalMon.Logger.LogInfo("You already have a monster out!");
                HUDManager.Instance.AddTextMessageClientRpc("You already have a monster out!");
            }
            else
            {
                EnemyType typeToSpawn = this.enemyType!;

                GameObject gameObject = Instantiate(typeToSpawn.enemyPrefab, this.transform.position,
                    Quaternion.Euler(new Vector3(0, 0f, 0f)));

                EnemyAI enemyAi = gameObject.GetComponent<EnemyAI>();
                if (gameObject.TryGetComponent(out TamedEnemyBehaviour tamedBehaviour))
                {
                    LethalMon.Logger.LogInfo("TouchGround: TamedEnemyBehaviour found");
                    tamedBehaviour.ownerPlayer = this.playerThrownBy;
                    tamedBehaviour.ownClientId = this.playerThrownBy.playerClientId;
                    tamedBehaviour.ballType = this.ballType;
                    tamedBehaviour.ballValue = this.scrapValue;
                    tamedBehaviour.scrapPersistedThroughRounds = this.scrapPersistedThroughRounds;
                    tamedBehaviour.alreadyCollectedThisRound = RoundManager.Instance.scrapCollectedThisRound.Contains(this);
                    tamedBehaviour.isOutsideOfBall = true;
                    tamedBehaviour.SwitchToCustomBehaviour(TamedEnemyBehaviour.CustomBehaviour.TamedFollowing);

                    LethalMon.Logger.LogInfo("5");
                    gameObject.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
                    SendReplaceWithCustomAiPacket(gameObject.GetComponent<NetworkObject>(), this.enemyType!.name, tamedBehaviour.ownClientId);
                    Destroy(this.gameObject);
                }
                else
                    LethalMon.Logger.LogWarning("TouchGround: TamedEnemyBehaviour not found");
            }
        }
    }
    
    public static void CopyProperties(object source, object destination)
    {
        // If any this null throw an exception
        if (source == null || destination == null)
            throw new Exception("Source or/and Destination Objects are null");
        // Getting the Types of the objects
        Type typeDest = destination.GetType();
        Type typeSrc = source.GetType();

        // Iterate the Properties of the source instance and  
        // populate them from their desination counterparts  
        PropertyInfo[] srcProps = typeSrc.GetProperties();
        foreach (PropertyInfo srcProp in srcProps)
        {
            if (!srcProp.CanRead)
            {
                continue;
            }
            PropertyInfo targetProperty = typeDest.GetProperty(srcProp.Name);
            if (targetProperty == null)
            {
                continue;
            }
            if (!targetProperty.CanWrite)
            {
                continue;
            }
            if (targetProperty.GetSetMethod(true) != null && targetProperty.GetSetMethod(true).IsPrivate)
            {
                continue;
            }
            if (targetProperty.GetSetMethod() != null && (targetProperty.GetSetMethod().Attributes & MethodAttributes.Static) != 0)
            {
                continue;
            }
            if (!targetProperty.PropertyType.IsAssignableFrom(srcProp.PropertyType))
            {
                continue;
            }
            // Passed all tests, lets set the value
            targetProperty.SetValue(destination, srcProp.GetValue(source, null), null);
        }
    }

    private void ChangeName()
    {
        string name = this.GetName();
        this.GetComponentInChildren<ScanNodeProperties>().headerText = name;
    }
    
    private string GetName()
    {
        return this.itemProperties.itemName + " (" + this.catchableEnemy.DisplayName + ")";
    }

    public override void SetControlTipsForItem()
    {
        string[] toolTips = itemProperties.toolTips;
        if (toolTips.Length < 1)
        {
            Debug.LogError("Pokeball control tips array length is too short to set tips!");
            return;
        }
        if (this.enemyCaptured && this.enemyType != null)
        {
            toolTips[0] = "Enemy captured: " + this.enemyType.name;
        }
        else
        {
            toolTips[0] = "";
        }
        HUDManager.Instance.ChangeControlTipMultiple(toolTips, holdingItem: true, itemProperties);
    }

    public void SetCaughtEnemy(EnemyType enemyType)
    {
        this.enemyType = enemyType;
        this.catchableEnemy = Data.CatchableMonsters[this.enemyType.name];
        this.enemyCaptured = true;
        this.ChangeName();

        if (this.IsHost || this.IsServer)
        {
            this.SyncContentPacket(this.GetComponent<NetworkObject>(), this.enemyType.name);
        }
    }

    public override int GetItemDataToSave()
    {
        base.GetItemDataToSave();

        if (!this.enemyCaptured || this.catchableEnemy == null)
        {
            return -1;
        }
        else
        {
            return this.catchableEnemy.Id;
        }
    }

    public override void LoadItemSaveData(int saveData)
    {
        base.LoadItemSaveData(saveData);
        
        if (Data.CatchableMonsters.Count(entry => entry.Value.Id == saveData) != 0)
        {
            KeyValuePair<string, CatchableEnemy.CatchableEnemy> catchable = Data.CatchableMonsters.First(entry => entry.Value.Id == saveData);
            EnemyType type = Resources.FindObjectsOfTypeAll<EnemyType>().First(type => type.name == catchable.Key);
            this.SetCaughtEnemy(type);
        }
    }
}
