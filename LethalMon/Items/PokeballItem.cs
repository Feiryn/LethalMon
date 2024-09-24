using System;
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
using static LethalMon.Utils;

namespace LethalMon.Items;

public abstract class PokeballItem : ThrowableItem, IAdvancedSaveableItem
{
    #region Properties
    private EnemyAI? enemyAI = null;
    
    internal EnemyType? enemyType = null;

    private CatchableEnemy.CatchableEnemy? catchableEnemy = null;

    private bool captureSuccess = false;

    private int captureRounds = 1;

    private int currentCaptureRound = 0;

    internal bool enemyCaptured = false;

    private readonly int captureStrength;

    private readonly BallType ballType;

    public Dictionary<string, Tuple<float, DateTime>> cooldowns = [];
    
    public bool isDnaComplete = false;

    public string enemySkinRegistryId = string.Empty;

    internal Animator? animator;

    internal AudioSource? audioSource;

    internal static AudioClip? BeepSFX = null;
    internal static AudioClip? SuccessSFX = null;
    internal static AudioClip? FailureSFX = null;
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

        audioSource = gameObject.GetComponent<AudioSource>();
        animator = gameObject.GetComponent<Animator>();
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

        LethalMon.Log("Collided with " + other.gameObject.name);

        //LethalMon.Log("Pokeball has an enemy captured: " + this.enemyCaptured);
        LethalMon.Log("Pokeball was thrown by: " + this.playerThrownBy);

        EnemyAI? enemyToCapture = other.GetComponentInParent<EnemyAI>();
        TamedEnemyBehaviour? behaviour = other.GetComponentInParent<TamedEnemyBehaviour>();
        if (enemyToCapture == null || enemyToCapture.isEnemyDead || behaviour == null || behaviour.IsTamed) return;

        if (Data.CatchableMonsters.TryGetValue(enemyToCapture.enemyType.name,
                out CatchableEnemy.CatchableEnemy catchable))
        {
            if (catchable.CanBeCapturedBy(enemyToCapture, playerThrownBy))
            {
                this.CaptureEnemy(enemyToCapture, catchable);
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
                this.enemyAI.gameObject.SetActive(true); // Show enemy
                if (ModelReplacementAPICompatibility.Instance.Enabled)
                    ModelReplacementAPICompatibility.FindCurrentReplacementModelIn(this.enemyAI.gameObject, isEnemy: true)?.SetActive(true);

                Data.CatchableMonsters[this.enemyType!.name].CatchFailBehaviour(this.enemyAI!, this.lastThrower!);

                if (FailureSFX != null)
                    Utils.PlaySoundAtPosition(gameObject.transform.position, FailureSFX); // Can't use audioSource as it gets destroyed

                if (Utils.IsHost)
                {
                    if (Utils.Random.NextDouble() < 0.5) // todo make it configurable
                    {
                        GameObject? spawnPrefab = BallTypeMethods.GetPrefab(ballType);
                        
                        if (spawnPrefab == null)
                        {
                            LethalMon.Log("Pokeball prefabs not loaded correctly.", LethalMon.LogType.Error);
                        }
                        else
                        {
                            GameObject? ball = Instantiate(spawnPrefab, this.enemyAI.transform.position, Quaternion.Euler(new Vector3(0, 0f, 0f)));
                            PokeballItem pokeballItem = ball.GetComponent<PokeballItem>();
                            pokeballItem.fallTime = 0f;
                            pokeballItem.scrapPersistedThroughRounds = scrapPersistedThroughRounds;
                            pokeballItem.SetScrapValue(scrapValue);
                            ball.GetComponent<NetworkObject>().Spawn(false);
                            pokeballItem.FallToGround();
                            pokeballItem.cooldowns = cooldowns;
                        }
                    }
                }
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
            tamedBehaviour.SwitchToTamingBehaviour(TamedEnemyBehaviour.TamingBehaviour.TamedFollowing);
            var enemyPosition = tamedBehaviour.Enemy.transform.position;
            tamedBehaviour.Enemy.SetDestinationToPosition(enemyPosition);
            tamedBehaviour.Enemy.transform.rotation = Quaternion.LookRotation(this.playerThrownBy.transform.position - enemyPosition);
            tamedBehaviour.SetCooldownTimers(cooldowns);
            tamedBehaviour.isDnaComplete = isDnaComplete;
            tamedBehaviour.ForceEnemySkinRegistryId = enemySkinRegistryId;

            gameObject.GetComponentInChildren<NetworkObject>().Spawn(destroyWithScene: true);
            CallTamedEnemyServerRpc(gameObject.GetComponent<NetworkObject>(), this.enemyType!.name, this.playerThrownBy.NetworkObject);
            Destroy(this.gameObject);
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
        this.startFallingPosition = this.transform.localPosition;
        this.targetFloorPosition = this.transform.localPosition;
        this.fallTime = 1f; // Stop moving
        this.currentCaptureRound = 0;
        this.grabbable = false; // Make it ungrabbable
        this.grabbableToEnemies = false;
        
        Data.CatchableMonsters[this.enemyAI!.enemyType.name].BeforeCapture(this.enemyAI, playerThrownBy!);
        this.enemyAI!.gameObject.SetActive(false); // Hide enemy
        if (ModelReplacementAPICompatibility.Instance.Enabled)
            ModelReplacementAPICompatibility.FindCurrentReplacementModelIn(this.enemyAI.gameObject, isEnemy: true)?.SetActive(false);

        this.PlayCaptureAnimationAnimator();
    }

    public void PlayCaptureAnimationAnimator()
    {
        if (animator == null) return;
        
        animator.Play("Base Layer.Capture", 0); // Play capture animation

        PlayCaptureBeepSound();
        Invoke(nameof(PlayCaptureBeepSound), 1.5f * animator.speed);
    }

    public void PlayCaptureBeepSound()
    {
        if (audioSource != null && BeepSFX != null)
            audioSource.PlayOneShot(BeepSFX);
    }
    
    private void CaptureEnemy(EnemyAI enemyAI, CatchableEnemy.CatchableEnemy catchable)
    {
        LethalMon.Log("Start to capture " + enemyAI.name);
        this.playerThrownBy = null;
        this.catchableEnemy = catchable;
        this.enemyAI = enemyAI;
        this.enemyType = enemyAI.enemyType;
        this.enemySkinRegistryId = EnemySkinRegistryCompatibility.GetEnemySkinId(enemyAI);
        
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
        
        // Make bracken release the enemy is held
        foreach (var tamedEnemyBehaviour in FindObjectsOfType<TamedEnemyBehaviour>())
        {
            if (tamedEnemyBehaviour is FlowermanTamedBehaviour flowermanTamedBehaviour && flowermanTamedBehaviour.GrabbedEnemyAi == enemyAI)
            {
                flowermanTamedBehaviour.ReleaseEnemy();
                flowermanTamedBehaviour.ReleaseEnemyServerRpc();
            }
        }
        
        PlayCaptureAnimationServerRpc(this.enemyAI.GetComponent<NetworkObject>(), this.captureRounds, this.captureSuccess);
    }

    public void CaptureEnd(/*string message*/)
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

            this.SetCaughtEnemyServerRpc(this.enemyType!.name, this.enemySkinRegistryId);

            this.isDnaComplete = true;
            this.playerThrownBy = null;
            this.FallToGround();
            this.grabbable = true;
            this.grabbableToEnemies = true;
        }
        else
        {
            LethalMon.Log("Capture failed");

            if (Utils.IsHost)
                this.GetComponent<NetworkObject>().Despawn(true);
        }
    }
    #endregion

    #region Methods
    public static void LoadAudio(AssetBundle assetBundle)
    {
        BeepSFX    = assetBundle.LoadAsset<AudioClip>("Assets/Audio/Balls/beep.ogg");
        SuccessSFX = assetBundle.LoadAsset<AudioClip>("Assets/Audio/Balls/success.ogg");
        FailureSFX = assetBundle.LoadAsset<AudioClip>("Assets/Audio/Balls/fail.ogg");
    }

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

        tamedBehaviour.ownerPlayer = ownerPlayer;
        tamedBehaviour.ForceEnemySkinRegistryId = enemySkinRegistryId;
        tamedBehaviour.SwitchToTamingBehaviour(TamedEnemyBehaviour.TamingBehaviour.TamedFollowing);
        HUDManagerPatch.UpdateTamedMonsterAction(tamedBehaviour.FollowingBehaviourDescription);
        tamedBehaviour.OnCallFromBall();
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

        EnemyType enemyType = EnemyTypes.First(type => type.name == enemyTypeName);
        SetCaughtEnemy(enemyType, enemySkinRegistryId);

        if (audioSource != null && SuccessSFX != null)
            audioSource.PlayOneShot(SuccessSFX);

        if (price != 0)
            FindObjectOfType<Terminal>().groupCredits -= price;
    }
    #endregion

    public object GetAdvancedItemDataToSave()
    {
        return new PokeballSaveData
        {
            enemyType = enemyType?.name,
            isDnaComplete = isDnaComplete,
            enemySkinRegistryId = enemySkinRegistryId
        };
    }

    public void LoadAdvancedItemData(object data)
    {
        if (data is PokeballSaveData { enemyType: not null } saveData)
        {
            SetCaughtEnemy(EnemyTypes.First(type => type.name == saveData.enemyType), saveData.enemySkinRegistryId);
            isDnaComplete = saveData.isDnaComplete;
        }
    }
}
