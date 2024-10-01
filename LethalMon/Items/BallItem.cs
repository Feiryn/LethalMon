using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameNetcodeStuff;
using LethalLib.Modules;
using LethalMon.Behaviours;
using LethalMon.Compatibility;
using LethalMon.Patches;
using LethalMon.Save;
using LethalMon.Throw;
using Unity.Netcode;
using UnityEngine;
using NetworkPrefabs = LethalLib.Modules.NetworkPrefabs;

namespace LethalMon.Items;

public abstract class BallItem : ThrowableItem, IAdvancedSaveableItem
{
    #region EnemyProperties
    private EnemyAI? enemyAI = null;
    
    internal EnemyType? enemyType = null;

    private CatchableEnemy.CatchableEnemy? catchableEnemy = null;
    
    public string enemySkinRegistryId = string.Empty;
    #endregion

    #region CaptureProperties
    private bool captureSuccess = false;

    private int captureRounds = 1;

    internal bool enemyCaptured = false;

    private readonly int captureStrength;
    #endregion
    
    #region BallProperties
    private readonly BallType ballType;
    
    public Dictionary<string, Tuple<float, DateTime>> cooldowns = [];
    
    public bool isDnaComplete = false;
    
    internal AudioSource? audioSource;
    #endregion
    
    #region Initialization
    public BallItem(BallType ballType, int captureStrength)
    {
        this.ballType = ballType;
        this.captureStrength = captureStrength;
    }

    internal static Item? InitBallPrefab<T>(AssetBundle assetBundle, string assetPath, int scrapRarity = 1) where T : BallItem
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

        NetworkPrefabs.RegisterNetworkPrefab(ballItem.spawnPrefab);

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

        audioSource = gameObject.GetComponent<AudioSource>();
    }
    public override void ItemActivate(bool used, bool buttonDown = true)
    {
#if DEBUG
        base.ItemActivate(used, buttonDown);
        return;
#else
        if (StartOfRound.Instance.shipHasLanded || StartOfRound.Instance.testRoom != null)
            base.ItemActivate(used, buttonDown);
#endif
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!Utils.IsHost || this.enemyCaptured || this.playerThrownBy == null) return;

        LethalMon.Log("Ball collided with " + other.gameObject.name);

        EnemyAI? enemyToCapture = other.GetComponentInParent<EnemyAI>();
        TamedEnemyBehaviour? behaviour = other.GetComponentInParent<TamedEnemyBehaviour>();
        if (enemyToCapture == null || enemyToCapture.isEnemyDead || behaviour == null || behaviour.IsTamed) return;

        if (Data.CatchableMonsters.TryGetValue(enemyToCapture.enemyType.name,
                out CatchableEnemy.CatchableEnemy catchable))
        {
            if (catchable.CanBeCapturedBy(enemyToCapture, playerThrownBy))
            {
                this.BallCollidedWithEnemy(enemyToCapture, catchable);
            }
            else
                LethalMon.Log(enemyToCapture.enemyType.name + " is not catchable by the player who threw the ball");
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
                this.CaptureFailed(enemyAI);
            }
        }
        else if(enemyAI != null)
        {
            RoundManager.Instance.SpawnedEnemies.Remove(enemyAI);
            if(Utils.IsHost && enemyAI.IsSpawned)
                enemyAI.GetComponent<NetworkObject>().Despawn(true);
        }

        base.OnDestroy();
    }

    public override void OnHitGround()
    {
        LethalMon.Log("Touch ground");

        if (!this.enemyCaptured || this.playerThrownBy == null) return;
        
        TamedEnemyBehaviour? playerPet = Utils.GetPlayerPet(this.playerThrownBy);
        
        if (playerPet != null)
        {
            if (this.playerThrownBy == Utils.CurrentPlayer)
            {
                LethalMon.Logger.LogInfo("You already have a monster out!");
                HUDManager.Instance.DisplayTip("LethalMon", "You already have a monster out!");
            }

            return;
        }

        if (Utils.IsHost)
        {
            this.ReleaseTamedMon(this.playerThrownBy);
        }
    }

    public override void SetControlTipsForItem()
    {
        string[] toolTips = itemProperties.toolTips;
        if (toolTips.Length < 1)
        {
            LethalMon.Log("Ball control tips array length is too short to set tips!", LethalMon.LogType.Error);
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
    #endregion

    #region Saving
    // Keep it here in case of advanced saves doesn't work or if an old save is loaded
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

        if (saveData != 0 && !this.enemyCaptured && Data.CatchableMonsters.Count(entry => entry.Value.Id == saveData) != 0)
        {
            KeyValuePair<string, CatchableEnemy.CatchableEnemy> catchable = Data.CatchableMonsters.First(entry => entry.Value.Id == saveData);
            EnemyType type = Resources.FindObjectsOfTypeAll<EnemyType>().First(type => type.name == catchable.Key);
            SetCaughtEnemy(type, string.Empty);
        }
    }
    
    public object GetAdvancedItemDataToSave()
    {
        return new BallSaveData
        {
            enemyType = enemyType?.name,
            isDnaComplete = isDnaComplete,
            enemySkinRegistryId = enemySkinRegistryId
        };
    }

    public void LoadAdvancedItemData(object data)
    {
        if (data is BallSaveData { enemyType: not null } saveData)
        {
            SetCaughtEnemy(Utils.EnemyTypes.First(type => type.name == saveData.enemyType), saveData.enemySkinRegistryId);
            isDnaComplete = saveData.isDnaComplete;
        }
    }
    #endregion
    
    #region CaptureMethods
    // All clients
    private void CaptureFailed(EnemyAI enemy)
    {
        enemy.gameObject.SetActive(true); // Show enemy
        if (ModelReplacementAPICompatibility.Instance.Enabled)
            ModelReplacementAPICompatibility.FindCurrentReplacementModelIn(enemy.gameObject, isEnemy: true)?.SetActive(true);

        Data.CatchableMonsters[this.enemyType!.name].CatchFailBehaviour(this.enemyAI!, this.lastThrower!);

        Utils.SpawnPoofCloudAt(this.transform.position);

        if (Utils.IsHost)
        {
            if (Utils.Random.NextDouble() < ModConfig.Instance.values.KeepBallAfterCaptureFailureProbability)
            {
                GameObject? spawnPrefab = BallTypeMethods.GetPrefab(ballType);
                        
                if (spawnPrefab == null)
                {
                    LethalMon.Log("Ball prefabs not loaded correctly.", LethalMon.LogType.Error);
                }
                else
                {
                    GameObject? ball = Instantiate(spawnPrefab, enemy.transform.position, Quaternion.Euler(new Vector3(0, 0f, 0f)));
                    BallItem ballItem = ball.GetComponent<BallItem>();
                    ballItem.fallTime = 0f;
                    ballItem.scrapPersistedThroughRounds = scrapPersistedThroughRounds;
                    ballItem.SetScrapValue(scrapValue);
                    ball.GetComponent<NetworkObject>().Spawn(false);
                    ballItem.FallToGround();
                    ballItem.cooldowns = cooldowns;
                }
            }
        }
    }

    // Host only
    private void ReleaseTamedMon(PlayerControllerB thrower)
    {
        EnemyType typeToSpawn = this.enemyType!;

        GameObject gameObj = Instantiate(typeToSpawn.enemyPrefab, this.transform.position,
            Quaternion.Euler(new Vector3(0, 0f, 0f)));
        
        if (!gameObj.TryGetComponent(out TamedEnemyBehaviour tamedBehaviour))
        {
            LethalMon.Logger.LogWarning("TouchGround: TamedEnemyBehaviour not found");
            return;
        }

        LethalMon.Logger.LogInfo("TouchGround: TamedEnemyBehaviour found");
        tamedBehaviour.ballType = this.ballType;
        tamedBehaviour.ballValue = this.scrapValue;
        tamedBehaviour.scrapPersistedThroughRounds = this.scrapPersistedThroughRounds;
        tamedBehaviour.alreadyCollectedThisRound = RoundManager.Instance.scrapCollectedThisRound.Contains(this);
        tamedBehaviour.SwitchToTamingBehaviour(TamedEnemyBehaviour.TamingBehaviour.TamedFollowing);
        var enemyPosition = tamedBehaviour.Enemy.transform.position;
        tamedBehaviour.Enemy.SetDestinationToPosition(enemyPosition);
        tamedBehaviour.Enemy.transform.rotation = Quaternion.LookRotation(thrower.transform.position - enemyPosition);
        tamedBehaviour.SetCooldownTimers(cooldowns);
        tamedBehaviour.isDnaComplete = isDnaComplete;
        tamedBehaviour.ForceEnemySkinRegistryId = enemySkinRegistryId;
        
        gameObj.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
        gameObj.SetActive(false);
        
        CallTamedEnemyServerRpc(gameObj.GetComponent<NetworkObject>(), this.enemyType!.name, thrower.NetworkObject);
    }
    
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
        this.PlayCaptureAnimation();
    }
    
    // All clients
    public void PlayCaptureAnimation()
    {
        this.startFallingPosition = this.transform.localPosition;
        this.targetFloorPosition = this.transform.localPosition;
        this.fallTime = 1f; // Stop moving
        this.grabbable = false; // Make it ungrabbable
        this.grabbableToEnemies = false;
        
        Data.CatchableMonsters[this.enemyAI!.enemyType.name].BeforeCapture(this.enemyAI, playerThrownBy!);
        this.enemyAI!.gameObject.SetActive(false); // Hide enemy
        if (ModelReplacementAPICompatibility.Instance.Enabled)
            ModelReplacementAPICompatibility.FindCurrentReplacementModelIn(this.enemyAI.gameObject, isEnemy: true)?.SetActive(false);

        float animationTime = this.StartMonsterGoesInsideBallAnimation();
        StartCoroutine(this.ExecuteAfterTime(animationTime, () =>
        {
            this.EndMonsterGoesInsideBallAnimation();
            StartCoroutine(this.BallShakesCoroutine(() =>
            {
                if (this.captureSuccess)
                {
                    float animationTimeCaptureSuccess = this.StartCaptureSuccessAnimation();
                    StartCoroutine(this.ExecuteAfterTime(animationTimeCaptureSuccess, () =>
                    {
                        this.EndCaptureSuccess();
                        this.EndCaptureSuccessAnimation();
                    }));
                }
                else
                {
                    this.EndCaptureFail();
                }
            }));
        }));
    }
    
    // Host only
    private void BallCollidedWithEnemy(EnemyAI enemy, CatchableEnemy.CatchableEnemy catchable)
    {
        LethalMon.Log("Start to capture " + enemy.name);
        this.playerThrownBy = null;
        this.catchableEnemy = catchable;
        this.enemyAI = enemy;
        this.enemyType = enemy.enemyType;
        if (EnemySkinRegistryCompatibility.Instance.Enabled)
        {
            this.enemySkinRegistryId = EnemySkinRegistryCompatibility.GetEnemySkinId(enemy);
        }
        
        CalculateCaptureRounds(catchable);

        // Make bracken release the enemy is held
        foreach (var tamedEnemyBehaviour in FindObjectsOfType<TamedEnemyBehaviour>())
        {
            if (tamedEnemyBehaviour is FlowermanTamedBehaviour flowermanTamedBehaviour && flowermanTamedBehaviour.GrabbedEnemyAi == enemy)
            {
                flowermanTamedBehaviour.ReleaseEnemy();
                flowermanTamedBehaviour.ReleaseEnemyServerRpc();
            }
        }
        
        PlayCaptureAnimationServerRpc(this.enemyAI.GetComponent<NetworkObject>(), this.captureRounds, this.captureSuccess);
    }

    private void CalculateCaptureRounds(CatchableEnemy.CatchableEnemy catchable)
    {
        float captureProbability = catchable.GetCaptureProbability(this.captureStrength, this.enemyAI);
        float shakeProbability = Mathf.Pow(captureProbability, 1f / 3f); // Cube root
        LethalMon.Log("Total capture probability: " + captureProbability + ". Each shake has probability of " + shakeProbability);
        this.captureRounds = 1;
        this.captureSuccess = false;
        for (int i = 0; i < 3; ++i)
        {
            float randomValue = (float)Data.Random.NextDouble();
            LethalMon.Log("Got random value " + randomValue + " for shake n°" + (i + 2));
            if (randomValue < shakeProbability)
            {
                if (i == 2)
                {
                    this.captureSuccess = true;
                }
                else
                {
                    this.captureRounds++;
                }
            }
            else
            {
                break;
            }
        }
    }

    private void EndCaptureSuccess()
    {
        this.SetCaughtEnemyServerRpc(this.enemyType!.name, this.enemySkinRegistryId);

        this.isDnaComplete = true;
        this.playerThrownBy = null;
        this.FallToGround();
        this.grabbable = true;
        this.grabbableToEnemies = true;
    }

    private void EndCaptureFail()
    {
        if (Utils.IsHost)
            this.GetComponent<NetworkObject>().Despawn(true);
    }

    private IEnumerator BallShakesCoroutine(Action callback)
    {
        for (int i = 0; i < this.captureRounds; ++i)
        {
            float animationTime = this.StartCaptureShakeAnimation();
            yield return new WaitForSeconds(animationTime);
            this.EndCaptureShakeAnimation();
        }

        callback();
    }
    
    private IEnumerator ExecuteAfterTime(float time, Action action)
    {
        yield return new WaitForSeconds(time);
        action();
    } 

    public override void StartThrowing()
    {
        base.StartThrowing();
        
        this.StartThrowAnimation();
    }

    public override void EndThrowing()
    {
        base.EndThrowing();
        
        this.EndThrowAnimation();
    }

    /// <summary>
    /// Function called when a ball is thrown
    /// </summary>
    protected abstract void StartThrowAnimation();

    /// <summary>
    /// Function called when a ball finished to be thrown (either by hitting the ground or an enemy)
    /// </summary>
    protected abstract void EndThrowAnimation();
    
    /// <summary>
    /// Function called when a monster starts to go inside the ball after being hit by a ball
    /// </summary>
    /// <returns>Animation time</returns>
    protected abstract float StartMonsterGoesInsideBallAnimation();
    
    /// <summary>
    /// Function called when a monster finished to go inside the ball after being hit by a ball
    /// </summary>
    protected abstract void EndMonsterGoesInsideBallAnimation();

    /// <summary>
    /// Function called when an animation shake is started
    /// </summary>
    /// <returns>Animation time</returns>
    protected abstract float StartCaptureShakeAnimation();
    
    /// <summary>
    /// Function called when an animation shake is finished
    /// </summary>
    protected abstract void EndCaptureShakeAnimation();
    
    /// <summary>
    /// Function called when a monster starts to be caught successfully by a ball
    /// </summary>
    /// <returns>Animation time</returns>
    protected abstract float StartCaptureSuccessAnimation();
    
    /// <summary>
    /// Function called when a monster finished to be caught successfully by a ball
    /// </summary>
    protected abstract void EndCaptureSuccessAnimation();
    
    /// <summary>
    /// Function called when a monster starts to be release by someone from a ball
    /// </summary>
    /// <returns>Animation time</returns>
    protected abstract float StartReleaseAnimation();
    
    /// <summary>
    /// Function called when a monster finished to be release by someone from a ball
    /// </summary>
    protected abstract void EndReleaseAnimation();
    #endregion
    
    #region BallMethods
    public void SetCaughtEnemy(EnemyType enemyType, string enemySkinRegistryId)
    {
        this.enemyType = enemyType;
        this.catchableEnemy = Data.CatchableMonsters[this.enemyType.name];
        this.enemyCaptured = true;
        this.enemySkinRegistryId = enemySkinRegistryId;
        this.ChangeName();
    }

    private void ChangeName()
    {
        this.GetComponentInChildren<ScanNodeProperties>().headerText = this.GetName();
    }

    private string GetName()
    {
        var enemySkinRegistryName = !string.IsNullOrEmpty(enemySkinRegistryId) && EnemySkinRegistryCompatibility.Instance.Enabled ? EnemySkinRegistryCompatibility.GetSkinName(enemySkinRegistryId) : null;
        return this.itemProperties.itemName + " (" + this.catchableEnemy?.DisplayName + (!string.IsNullOrEmpty(enemySkinRegistryName) ? " - " + enemySkinRegistryName : string.Empty) + ")";
    }

    public override void GrabItem()
    {
        base.GrabItem();

        if (PC.PC.Instance.GetCurrentPlacedBall() == this)
        {
            PC.PC.Instance.RemoveBallServerRpc();
        }
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
        
        enemyNetworkObject.gameObject.SetActive(false);

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

        float animationTime = this.StartReleaseAnimation();
        StartCoroutine(this.ExecuteAfterTime(animationTime, () =>
        {
            this.EndReleaseAnimation();
            
            tamedBehaviour.ownerPlayer = ownerPlayer;
            tamedBehaviour.ForceEnemySkinRegistryId = enemySkinRegistryId;
            tamedBehaviour.SwitchToTamingBehaviour(TamedEnemyBehaviour.TamingBehaviour.TamedFollowing);
            HUDManagerPatch.UpdateTamedMonsterAction(tamedBehaviour.FollowingBehaviourDescription);
            enemyNetworkObject.gameObject.SetActive(true);
            tamedBehaviour.OnCallFromBall();
            
            Destroy(this.gameObject);
        }));
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetCaughtEnemyServerRpc(string enemyTypeName, string enemySkinRegistryId, int price = 0)
    {
        SetCaughtEnemyClientRpc(enemyTypeName, enemySkinRegistryId, price);
    }

    [ClientRpc]
    public void SetCaughtEnemyClientRpc(string enemyTypeName, string enemySkinRegistryId, int price)
    {
        LethalMon.Log("SyncContentPacket client rpc received (EnemyType: " + enemyTypeName + ", EnemySkinRegistryId: " + enemySkinRegistryId + ")");

        EnemyType enemyType = Utils.EnemyTypes.First(type => type.name == enemyTypeName);
        SetCaughtEnemy(enemyType, enemySkinRegistryId);

        if (price != 0)
            FindObjectOfType<Terminal>().groupCredits -= price;
    }
    #endregion
}