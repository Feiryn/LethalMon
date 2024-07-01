using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GameNetcodeStuff;
using HarmonyLib;
using LethalLib.Modules;
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

    internal static GameObject? InitBallPrefab<T>(AssetBundle assetBundle, string assetPath, int scrapRarity = 1) where T : PokeballItem
    {
        if (assetBundle == null) return null;

        var ballItem = assetBundle.LoadAsset<Item>(Path.Combine("Assets/Balls", assetPath));
        if (ballItem == null)
        {
            LethalMon.Log($"{assetPath} not found.", LethalMon.LogType.Error);
            return null;
        }

        T script = ballItem.spawnPrefab.AddComponent<T>();
        script.itemProperties = ballItem;
        script.grabbable = true;
        script.grabbableToEnemies = true;
        LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(ballItem.spawnPrefab);

        LethalLib.Modules.Items.RegisterScrap(ballItem, scrapRarity, Levels.LevelTypes.All);

        return ballItem.spawnPrefab;
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

    #region CaptureAnimation

    [ServerRpc(RequireOwnership = false)]
    public void PlayCaptureAnimationServerRpc(NetworkObjectReference enemy, int roundsNumber, bool catchSuccess)
    {
        PlayCaptureAnimationClientRpc(enemy, roundsNumber, catchSuccess);
    }
    
    [ClientRpc]
    public void PlayCaptureAnimationClientRpc(NetworkObjectReference enemy, int roundsNumber, bool catchSuccess)
    {
        Debug.Log("Play capture animation client rpc received");

        if (!enemy.TryGet(out NetworkObject enemyAINetworkObject))
        {
            Debug.LogError(this.gameObject.name + ": Failed to get network object from network object reference (Capture animation RPC)");
            return;
        }

        this.enemyAI = enemyAINetworkObject.gameObject.GetComponent<EnemyAI>();
        this.enemyType = this.enemyAI.enemyType;
        this.captureSuccess = catchSuccess;
        this.captureRounds = roundsNumber;
        this.currentCaptureRound = 0;
        this.PlayCaptureAnimation();
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
        
        PlayCaptureAnimationServerRpc(this.enemyAI.GetComponent<NetworkObject>(), this.captureRounds, this.captureSuccess);
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
    
    #endregion
    
    #region CustomAiSpawning

    public void CallTamedEnemyServerRpc(NetworkObjectReference networkObjectReference, string customAiName, ulong ownerClientId)
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
    public void CallTamedEnemyClientRpc(NetworkObjectReference networkObjectReference, string customAiName, ulong ownerClientId)
    {
        Debug.Log("ReplaceWithCustomAi client rpc received");
        if (!networkObjectReference.TryGet(out NetworkObject networkObject))
        {
            Debug.LogError(this.gameObject.name + ": Failed to get network object from network object reference (Capture animation RPC)");
            return;
        }

        if (!Data.CatchableMonsters.TryGetValue(customAiName, out CatchableEnemy.CatchableEnemy catchableEnemy))
        {
            Debug.Log("Custom AI name not found (maybe mod version mismatch)");
            return;
        }

        if (!networkObject.gameObject.TryGetComponent(out TamedEnemyBehaviour tamedBehaviour))
        {
            Debug.Log("CallTamedEnemy: No tamed enemy behaviour found.");
            return;
        }

        tamedBehaviour.SwitchToTamingBehaviour(TamedEnemyBehaviour.TamingBehaviour.TamedFollowing);
        tamedBehaviour.ownClientId = ownerClientId;
    }
    
    #endregion

    #region SyncContent

    public void SetCaughtEnemyServerRpc(string enemyTypeName)
    {
        SetCaughtEnemyClientRpc(enemyTypeName);
    }
    
    [ClientRpc]
    public void SetCaughtEnemyClientRpc(string enemyTypeName)
    {
        Debug.Log("SyncContentPacket client rpc received");

        EnemyType enemyType = Resources.FindObjectsOfTypeAll<EnemyType>().First(type => type.name == enemyTypeName);
        SetCaughtEnemy(enemyType);
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
                if (!gameObject.TryGetComponent(out TamedEnemyBehaviour tamedBehaviour))
                {
                    LethalMon.Logger.LogWarning("TouchGround: TamedEnemyBehaviour not found");
                    return;
                }

                LethalMon.Logger.LogInfo("TouchGround: TamedEnemyBehaviour found");
                tamedBehaviour.ownerPlayer = this.playerThrownBy;
                tamedBehaviour.ownClientId = this.playerThrownBy.playerClientId;
                tamedBehaviour.ballType = this.ballType;
                tamedBehaviour.ballValue = this.scrapValue;
                tamedBehaviour.scrapPersistedThroughRounds = this.scrapPersistedThroughRounds;
                tamedBehaviour.alreadyCollectedThisRound = RoundManager.Instance.scrapCollectedThisRound.Contains(this);
                tamedBehaviour.isOutsideOfBall = true;
                tamedBehaviour.SwitchToTamingBehaviour(TamedEnemyBehaviour.TamingBehaviour.TamedFollowing);

                gameObject.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
                CallTamedEnemyServerRpc(gameObject.GetComponent<NetworkObject>(), this.enemyType!.name, tamedBehaviour.ownClientId);
                Destroy(this.gameObject);
            }
        }
    }

    private void ChangeName()
    {
        string name = this.GetName();
        this.GetComponentInChildren<ScanNodeProperties>().headerText = name;
    }
    
    private string GetName()
    {
        return this.itemProperties.itemName + " (" + this.catchableEnemy?.DisplayName + ")";
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

        if (Utils.IsHost)
            this.SetCaughtEnemyServerRpc(this.enemyType.name);
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
