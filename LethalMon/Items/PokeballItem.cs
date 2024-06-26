using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        
        if (Utils.IsHost && !this.enemyCaptured && this.playerThrownBy != null)
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

            if (Utils.IsHost)
                this.GetComponent<NetworkObject>().Despawn(true);
        }
    }

    public override void OnDestroy()
    {
        if (!this.captureSuccess && !this.enemyCaptured)
        {
            if (this.enemyAI != null)
            {
                this.enemyAI.gameObject.SetActive(true); // Show enemy

                if (Utils.IsHost)
                    this.catchableEnemy!.CatchFailBehaviour(this.enemyAI!, this.playerThrownBy);
            }
        }
        
        base.OnDestroy();
    }

    #endregion

    #region CustomAiSpawning

    [ServerRpc(RequireOwnership = false)]
    public void ReplaceWithCustomAiServerRpc(NetworkObjectReference networkObjectReference, string customAiName, ulong ownerClientId)
    {
        ReplaceWithCustomAiClientRpc(networkObjectReference, customAiName, ownerClientId);
    }
    
    [ClientRpc]
    public void ReplaceWithCustomAiClientRpc(NetworkObjectReference networkObjectReference, string customAiName, ulong ownerClientId)
    {
        Debug.Log("ReplaceWithCustomAi client rpc received");
        if (!networkObjectReference.TryGet(out NetworkObject networkObject))
        {
            Debug.LogError(this.gameObject.name + ": Failed to get network object from network object reference (Capture animation RPC)");
            return;
        }

        if(!networkObject.TryGetComponent(out EnemyAI enemyAi))
        {
            Debug.LogError(this.gameObject.name + ": Failed to get enemyAi (Capture animation RPC)");
            return;
        }

        if (!Data.CatchableMonsters.TryGetValue(customAiName, out CatchableEnemy.CatchableEnemy catchableEnemy))
        {
            Debug.Log("Custom AI name not found (maybe mod version mismatch)");
            return;
        }

        CustomAI newAi = catchableEnemy.AddAiComponent(networkObject.gameObject);
        // Dirty, but we need to add the CustomAi in order to be able to process the RPCs
        ((List<NetworkBehaviour>)networkObject.GetType()
            .GetField("m_ChildNetworkBehaviours", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(networkObject))
            .Add(newAi);
        CopyProperties(enemyAi, newAi);
        newAi.CopyProperties(enemyAi);
        newAi.ownClientId = ownerClientId;
        enemyAi.enabled = false;
        Destroy(this.gameObject);
    }
    #endregion

    #region SyncContent

    [ServerRpc(RequireOwnership = false)]
    public void SyncContentServerRpc(NetworkObjectReference networkObjectReference, string enemyTypeName)
    {
        SyncContentClientRpc(networkObjectReference, enemyTypeName);
    }
    
    [ClientRpc]
    public void SyncContentClientRpc(NetworkObjectReference networkObjectReference, string enemyTypeName)
    {
        Debug.Log("SyncContentPacket client rpc received");
        if (!networkObjectReference.TryGet(out NetworkObject networkObject))
        {
            Debug.LogError(this.gameObject.name + ": Failed to get network object from network object reference (SyncContentPacket RPC)");
            return;
        }

        if(!networkObject.TryGetComponent(out PokeballItem pokeballItem))
        {
            Debug.LogError(this.gameObject.name + ": Failed to get pokeball item (SyncContentPacket RPC)");
            return;
        }

        EnemyType enemyType = Resources.FindObjectsOfTypeAll<EnemyType>().First(type => type.name == enemyTypeName);
        pokeballItem.SetCaughtEnemy(enemyType);
    }
    #endregion
    
    public override void TouchGround()
    {
        Debug.Log("Touch ground");

        if (base.IsHost && this.playerThrownBy != null && this.enemyCaptured)
        {
            if (Utils.GetPlayerPet(this.playerThrownBy) != null)
            {
                HUDManager.Instance.AddTextMessageClientRpc("You already have a monster out!");
            }
            else
            {
                EnemyType typeToSpawn = this.enemyType!;

                GameObject gameObject = Object.Instantiate(typeToSpawn.enemyPrefab, this.transform.position,
                    Quaternion.Euler(new Vector3(0, 0f, 0f)));

                EnemyAI enemyAi = gameObject.GetComponent<EnemyAI>();
                CustomAI newAi = this.catchableEnemy!.AddAiComponent(gameObject);
                CopyProperties(enemyAi, newAi);
                newAi.CopyProperties(enemyAi);
                newAi.ownerPlayer = this.playerThrownBy;
                newAi.ownClientId = this.playerThrownBy.playerClientId;
                newAi.ballType = this.ballType;
                newAi.ballValue = this.scrapValue;
                newAi.scrapPersistedThroughRounds = this.scrapPersistedThroughRounds;
                newAi.alreadyCollectedThisRound = RoundManager.Instance.scrapCollectedThisRound.Contains(this);
                
                enemyAi.enabled = false;
                gameObject.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
                ReplaceWithCustomAiServerRpc(gameObject.GetComponent<NetworkObject>(), this.enemyType!.name, newAi.ownClientId);
                Destroy(this.gameObject);
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
            this.SyncContentServerRpc(this.GetComponent<NetworkObject>(), this.enemyType.name);
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
