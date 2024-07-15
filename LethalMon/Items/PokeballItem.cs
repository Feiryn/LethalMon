using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameNetcodeStuff;
using LethalLib.Modules;
using LethalMon.Behaviours;
using LethalMon.Throw;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.Items;

public abstract class PokeballItem : ThrowableItem
{
    #region Properties
    private EnemyAI? enemyAI = null;
    
    private EnemyType? enemyType = null;

    private CatchableEnemy.CatchableEnemy? catchableEnemy = null;

    private bool captureSuccess = false;

    private int captureRounds = 1;

    private int currentCaptureRound = 0;

    internal bool enemyCaptured = false;

    private int captureStrength;

    private BallType ballType;
    #endregion

    #region Initialization
    public PokeballItem(BallType ballType, int captureStrength)
    {
        this.ballType = ballType;
        this.captureStrength = captureStrength;
    }

    internal static Item? InitBallPrefab<T>(AssetBundle assetBundle, string assetPath, int scrapRarity = 1) where T : PokeballItem
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

        return ballItem;
    }
    #endregion

    #region Base Methods
    public override void Start()
    {
        base.Start();
        for (int i = 0; i < propColliders.Length; i++)
            propColliders[i].excludeLayers = 0; // 0 = nothing, -1 = everything (default since v55)
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
        LethalMon.Log("Collided with " + other.gameObject.name);

        LethalMon.Log("Pokeball has an enemy captured: " + this.enemyCaptured);
        LethalMon.Log("Pokeball was thrown by: " + this.playerThrownBy);

        if (!Utils.IsHost || this.enemyCaptured || this.playerThrownBy == null) return;

        EnemyAI? enemyToCapture = other.GetComponentInParent<EnemyAI>();
        if (enemyToCapture == null) return;

        if (Data.CatchableMonsters.TryGetValue(enemyToCapture.enemyType.name,
                out CatchableEnemy.CatchableEnemy catchable))
        {
            this.CaptureEnemy(enemyToCapture, catchable);   
        }
        else
        {
            LethalMon.Log(enemyToCapture.enemyType.name + " is not catchable");
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
                    this.catchableEnemy!.CatchFailBehaviour(this.enemyAI!, this.lastThrower);
                }
            }
        }

        base.OnDestroy();
    }

    public override void TouchGround()
    {
        LethalMon.Log("Touch ground");

        if (base.IsHost && this.playerThrownBy != null && this.enemyCaptured)
        {
            LethalMon.Logger.LogInfo("Getting pet..");
            if (Utils.GetPlayerPet(this.playerThrownBy) != null)
            {
                LethalMon.Logger.LogInfo("You already have a monster out!");
                HUDManager.Instance.DisplayTip("LethalMon", "You already have a monster out!");
            }
            else
            {
                EnemyType typeToSpawn = this.enemyType!;

                GameObject gameObject = Instantiate(typeToSpawn.enemyPrefab, this.transform.position,
                    Quaternion.Euler(new Vector3(0, 0f, 0f)));

                //EnemyAI enemyAi = gameObject.GetComponent<EnemyAI>();
                if (!gameObject.TryGetComponent(out TamedEnemyBehaviour tamedBehaviour))
                {
                    LethalMon.Logger.LogWarning("TouchGround: TamedEnemyBehaviour not found");
                    return;
                }

                LethalMon.Logger.LogInfo("TouchGround: TamedEnemyBehaviour found");
                tamedBehaviour.ballType = this.ballType;
                tamedBehaviour.ballValue = this.scrapValue;
                tamedBehaviour.scrapPersistedThroughRounds = this.scrapPersistedThroughRounds;
                tamedBehaviour.alreadyCollectedThisRound = RoundManager.Instance.scrapCollectedThisRound.Contains(this);
                tamedBehaviour.isOutsideOfBall = true;
                tamedBehaviour.SwitchToTamingBehaviour(TamedEnemyBehaviour.TamingBehaviour.TamedFollowing);
                var enemyPosition = tamedBehaviour.Enemy.transform.position;
                tamedBehaviour.Enemy.SetDestinationToPosition(enemyPosition);
                tamedBehaviour.Enemy.transform.rotation = Quaternion.LookRotation(this.playerThrownBy.transform.position - enemyPosition);

                gameObject.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
                CallTamedEnemyServerRpc(gameObject.GetComponent<NetworkObject>(), this.enemyType!.name, this.playerThrownBy.NetworkObject);
                Destroy(this.gameObject);
            }
        }
    }

    public override void SetControlTipsForItem()
    {
        string[] toolTips = itemProperties.toolTips;
        if (toolTips.Length < 1)
        {
            LethalMon.Log("Pokeball control tips array length is too short to set tips!", LethalMon.LogType.Error);
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
            SetCaughtEnemyServerRpc(type.name);
        }
    }
    #endregion

    #region CaptureAnimation

    [ServerRpc(RequireOwnership = false)]
    public void PlayCaptureAnimationServerRpc(NetworkObjectReference enemy, int roundsNumber, bool catchSuccess)
    {
        PlayCaptureAnimationClientRpc(enemy, roundsNumber, catchSuccess);
    }
    
    [ClientRpc]
    public void PlayCaptureAnimationClientRpc(NetworkObjectReference enemy, int roundsNumber, bool catchSuccess)
    {
        LethalMon.Log("Play capture animation client rpc received");

        if (!enemy.TryGet(out NetworkObject enemyAINetworkObject))
        {
            LethalMon.Log(this.gameObject.name + ": Failed to get network object from network object reference (Capture animation RPC)", LethalMon.LogType.Error);
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
        LethalMon.Log("Start to capture " + enemyAI.name);
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
        LethalMon.Log("Capture animation end");

        // Test if we need to play the animation more times
        if (this.currentCaptureRound + 1 < this.captureRounds)
        {
            LethalMon.Log("Play the animation again");
            
            this.currentCaptureRound++;
            PlayCaptureAnimationAnimator();
        }
        else if (this.captureSuccess)
        {
            LethalMon.Log("Capture success");

            this.SetCaughtEnemyServerRpc(this.enemyType.name);

            this.playerThrownBy = null;
            this.FallToGround();
            this.grabbable = true;
            this.grabbableToEnemies = true;
        }
        else
        {
            LethalMon.Log("Capture failed");

            if (base.NetworkManager.IsServer || base.NetworkManager.IsHost)
            {
                this.GetComponent<NetworkObject>().Despawn(true);
            }
        }
    }
    #endregion

    #region Methods
    public void SetCaughtEnemy(EnemyType enemyType)
    {
        this.enemyType = enemyType;
        this.catchableEnemy = Data.CatchableMonsters[this.enemyType.name];
        this.enemyCaptured = true;
        this.ChangeName();
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
    #endregion

    #region RPCs

    [ServerRpc(RequireOwnership = false)]
    public void CallTamedEnemyServerRpc(NetworkObjectReference networkObjectReference, string enemyName, NetworkObjectReference ownerNetworkReference)
    {
        CallTamedEnemyClientRpc(networkObjectReference, enemyName, ownerNetworkReference);
    }

    [ClientRpc]
    public void CallTamedEnemyClientRpc(NetworkObjectReference networkObjectReference, string enemyName, NetworkObjectReference ownerNetworkReference)
    {
        LethalMon.Log("ReplaceWithCustomAi client rpc received");
        if (!networkObjectReference.TryGet(out NetworkObject enemyNetworkObject) || !ownerNetworkReference.TryGet(out NetworkObject ownerNetworkObject))
        {
            LethalMon.Log(this.gameObject.name + ": Failed to get network object from network object reference (Capture animation RPC)", LethalMon.LogType.Error);
            return;
        }

        if (!Data.CatchableMonsters.TryGetValue(enemyName, out CatchableEnemy.CatchableEnemy _))
        {
            LethalMon.Log("Enemy not catchable (maybe mod version mismatch).", LethalMon.LogType.Error);
            return;
        }

        if (!enemyNetworkObject.gameObject.TryGetComponent(out TamedEnemyBehaviour tamedBehaviour))
        {
            LethalMon.Log("CallTamedEnemy: No tamed enemy behaviour found.", LethalMon.LogType.Error);
            return;
        }

        if (!ownerNetworkObject.gameObject.TryGetComponent(out PlayerControllerB ownerPlayer))
        {
            LethalMon.Log("CallTamedEnemy: No owner found.", LethalMon.LogType.Error);
            return;
        }

        tamedBehaviour.SwitchToTamingBehaviour(TamedEnemyBehaviour.TamingBehaviour.TamedFollowing);
        tamedBehaviour.ownerPlayer = ownerPlayer;
        tamedBehaviour.ownClientId = ownerPlayer.playerClientId;
        tamedBehaviour.OnCallFromBall();
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetCaughtEnemyServerRpc(string enemyTypeName)
    {
        SetCaughtEnemyClientRpc(enemyTypeName);
    }

    [ClientRpc]
    public void SetCaughtEnemyClientRpc(string enemyTypeName)
    {
        LethalMon.Log("SyncContentPacket client rpc received");

        EnemyType enemyType = Resources.FindObjectsOfTypeAll<EnemyType>().First(type => type.name == enemyTypeName);
        SetCaughtEnemy(enemyType);
    }
    #endregion
}
