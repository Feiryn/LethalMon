using System;
using System.Collections;
using GameNetcodeStuff;
using LethalMon.Items;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LethalMon.PC;

public class PC : NetworkBehaviour
{
    internal static GameObject? pcPrefab = null;

    internal static PC Instance;

    private static AudioClip _errorSound;
    
    private static AudioClip _successSound;

    private static AudioClip _scanSound;
    
    private ParticleSystem _particleSystem;

    private ParticleSystem ParticleSystem
    {
        get
        {
            if (_particleSystem == null)
            {
                _particleSystem = this.transform.Find("BeamParticle")?.GetComponent<ParticleSystem>()!;
                
                // Copy material from player's beam up particle system
                _particleSystem.GetComponent<Renderer>().material = Utils.CurrentPlayer.beamUpParticle.GetComponent<Renderer>().material;
            }

            return _particleSystem;
        }
    }

    #region Constants
    public static readonly string UnlockableName = "LethalMon PC";
        
    private const float CursorSpeed = 0.001f;
    
    private const float CursorMinX = -0.795f;
    
    private const float CursorMaxX = 0.798f;
    
    private const float CursorMinY = -0.445f;
    
    private const float CursorMaxY = 0.45f;
    #endregion
    
    #region Components
    private InteractTrigger _ballPlaceInteractTrigger;
    
    private InteractTrigger _screenInteractTrigger;
    
    private PlayerActions _playerActions;

    private PlayerControllerB _currentPlayer;
    
    private RectTransform _cursor;

    private GameObject _screen;

    private Button[] _desktopButtons;
    
    private AudioSource _audioSource;

    private PlaceableShipObject _placeableShipObject;
    #endregion
    
    #region PCApp
    private Button _appCloseButton;
    
    private PCApp? _currentApp;
    
    private DexApp _dexApp;
    
    private ScanApp _scanApp;
    #endregion

    private static int _backupRenderTextureWidth;
    
    private static int _backupRenderTextureHeight;
    
    internal Coroutine? _currentOperationCoroutine { get; private set; }

    private PokeballItem? _placedBall;

    public void Start()
    {
        LethalMon.Log("PC Start");

        Instance = this;
        
        // Load the triggers from the prefab and set missing properties
        InteractTrigger[] interactTriggers = GetComponentsInChildren<InteractTrigger>();
        _ballPlaceInteractTrigger = interactTriggers[1];
        _screenInteractTrigger = interactTriggers[0];
        Sprite hoverIcon = GameObject.Find("StartGameLever")?.GetComponent<InteractTrigger>()?.hoverIcon!;
        Sprite disabledIcon = GameObject.Find("StartGameLever")?.GetComponent<InteractTrigger>()?.disabledHoverIcon!;
        foreach (var interactTrigger in interactTriggers)
        {
            interactTrigger.hoverIcon = hoverIcon;
            interactTrigger.disabledHoverIcon = disabledIcon;
            interactTrigger.holdingInteractEvent = new InteractEventFloat();
            interactTrigger.onInteract = new InteractEvent();
            interactTrigger.onInteractEarly = new InteractEvent();
            interactTrigger.onStopInteract = new InteractEvent();
            interactTrigger.onCancelAnimation = new InteractEvent();
            interactTrigger.enabled = true;

        }
        _ballPlaceInteractTrigger.onInteract.AddListener(OnBallPlaceInteract);
        _screenInteractTrigger.onInteractEarly.AddListener(StartUsing); // Early because it has a special animation
        
        // Player actions
        _playerActions = new PlayerActions();
        
        // Load screen components
        _cursor = gameObject.transform.Find("Screen/Cursor")?.GetComponent<RectTransform>()!;
        _screen = gameObject.transform.Find("Screen")?.gameObject!;
        _audioSource = gameObject.transform.Find("Screen")?.GetComponent<AudioSource>()!;
        
        // Assign buttons to functions
        _desktopButtons = new Button[4];
        _desktopButtons[0] = gameObject.transform.Find("Screen/MainMenu/TutorialButton").GetComponent<Button>();
        _desktopButtons[1] = gameObject.transform.Find("Screen/MainMenu/DexButton").GetComponent<Button>();
        _desktopButtons[1].onClick = FunctionToButtonClickEvent(OnDexButtonClick);
        _desktopButtons[2] = gameObject.transform.Find("Screen/MainMenu/ScanButton").GetComponent<Button>();
        _desktopButtons[2].onClick = FunctionToButtonClickEvent(OnScanButtonClick);
        _desktopButtons[3] = gameObject.transform.Find("Screen/MainMenu/DuplicateButton").GetComponent<Button>();
        
        // Load PC apps
        _appCloseButton = _screen.transform.Find("Window/CloseButton").GetComponent<Button>();
        _appCloseButton.onClick.AddListener(CloseCurrentApp);
        _dexApp = new DexApp(_screen);
        _dexApp.Hide();
        _scanApp = new ScanApp(_screen);
        _scanApp.Hide();
        
        // Load the placeable ship object
        _placeableShipObject = GetComponentInChildren<PlaceableShipObject>();
        Terminal terminal = FindObjectOfType<Terminal>();
        if (terminal != null)
        {
            transform.position = terminal.transform.position + new Vector3(-2, 0, 0);
            transform.parent = terminal.transform.parent;
            _placeableShipObject.placeObjectSFX = terminal.placeableObject.placeObjectSFX;
        }

        // Stop particles
        transform.Find("BeamParticle")?.GetComponent<ParticleSystem>()!.gameObject.SetActive(false);
    }

    private static Button.ButtonClickedEvent FunctionToButtonClickEvent(UnityAction action)
    {
        var buttonClickedEvent = new Button.ButtonClickedEvent();
        buttonClickedEvent.AddListener(action);
        return buttonClickedEvent;
    }

    private static void HighQualityCamera()
    {
        // todo do not do anything if not default settings or too high already
        // todo small render distance so low end computers won't burn

        Camera camera = Utils.CurrentPlayer.gameplayCamera;
        if (camera.pixelWidth != 860 || camera.pixelHeight != 520)
        {
            LethalMon.Log("Detected custom resolution mod, don't change the target texture");
            return;
        }
        
        // Backup the target texture (even if it's the default one, we will later maybe support custom settings...
        _backupRenderTextureWidth = camera.targetTexture.width;
        _backupRenderTextureHeight = camera.targetTexture.height;
        
        // Release the target texture and double the resolution
        camera.targetTexture.Release();
        
        // Let's assume that the user has a 1920x1080. Higher resolutions can make PCs heat
        camera.targetTexture.width = 1920;
        camera.targetTexture.height = 1080;
    }

    private static void RollbackHighQualityCamera()
    {
        Camera camera = Utils.CurrentPlayer.gameplayCamera;
        
        // Release the target texture and double the resolution
        camera.targetTexture.Release();
        
        // Let's assume that the user has a 1920x1080. Higher resolutions can make PCs heat
        camera.targetTexture.width = _backupRenderTextureWidth;
        camera.targetTexture.height = _backupRenderTextureHeight;
    }
    
    public void OnBallPlaceInteract(PlayerControllerB player)
    {
        LethalMon.Log("Ball place interact");

        GrabbableObject heldItem = player.ItemSlots[player.currentItemSlot];
        if (heldItem != null && heldItem is PokeballItem item && GetCurrentPlacedBall() == null)
        {
            _placedBall = item;
            player.DiscardHeldObject(true, this.GetComponent<NetworkObject>(), _ballPlaceInteractTrigger.transform.localPosition + Vector3.up * item.itemProperties.verticalOffset);
        }
    }

    public PokeballItem? GetCurrentPlacedBall()
    {
        if (_placedBall != null)
        {
            return _placedBall;
        }
        
        return null;
    }
    
    public void RemovePlacedBall()
    {
        _placedBall = null;
    }
    
    public void StartUsing(PlayerControllerB player)
    {
        // Don't trust Zeekerss code... the player is always null on an interact early event
        player = Utils.CurrentPlayer;
        
        try
        {
            _currentPlayer = player;
            
            player.inTerminalMenu = true;

            HUDManager.Instance.ChangeControlTip(0,
                StartOfRound.Instance.localPlayerUsingController ? "Quit PC : [Start]" : "Quit PC : [TAB]",
                clearAllOther: true);
            
            _playerActions.Movement.OpenMenu.performed += PressEsc;
            _playerActions.Movement.Look.performed += Look_performed;
            _playerActions.Movement.Use.performed += LeftClick_performed;
            _playerActions.Enable();
            player.playerActions.Movement.Move.Disable();
            player.playerActions.Movement.Look.Disable();

            HighQualityCamera();
        }
        catch
        {
            StopUsing();
        }
    }
    
    public void StopUsing()
    {
        if (_currentPlayer != null)
        {
            _playerActions.Movement.OpenMenu.performed -= PressEsc;
            _playerActions.Movement.Look.performed -= Look_performed;
            _playerActions.Movement.Use.performed -= LeftClick_performed;
            _playerActions.Disable();
            _currentPlayer.playerActions.Movement.Move.Enable();
            _currentPlayer.playerActions.Movement.Look.Enable();
            
            _currentPlayer.inTerminalMenu = false;
            _screenInteractTrigger.StopSpecialAnimation();
            
            RollbackHighQualityCamera();
        }
    }
    
    public void CloseCurrentApp()
    {
        if (_currentApp != null && _currentOperationCoroutine == null)
        {
            _currentApp.Hide();
            _currentApp = null;
            foreach (var desktopButton in _desktopButtons)
            {
                desktopButton.gameObject.SetActive(true);
            }
        }
    }
    
    public void SwitchToApp(PCApp app)
    {
        app.Show();
        _currentApp = app;
        foreach (var desktopButton in _desktopButtons)
        {
            desktopButton.gameObject.SetActive(false);
        }
    }
    
    public void PressEsc(InputAction.CallbackContext context)
    {
        if (_currentApp != null && _currentOperationCoroutine == null)
        {
            CloseCurrentApp();
        }
        else
        {
            StopUsing();
        }
    }
    
    public void Look_performed(InputAction.CallbackContext context)
    {
        Vector2 move = context.ReadValue<Vector2>();
        _cursor.anchoredPosition = new Vector2(Mathf.Clamp(_cursor.anchoredPosition.x + move.x * CursorSpeed, CursorMinX, CursorMaxX), Mathf.Clamp(_cursor.anchoredPosition.y + move.y * CursorSpeed, CursorMinY, CursorMaxY));
    }
    
    public void LeftClick_performed(InputAction.CallbackContext context)
    {
        LethalMon.Log("Left click performed");

        foreach (var button in _screen.GetComponentsInChildren<Button>())
        {
            if (button.IsActive() && IsCursorOnButton(button))
            {
                button.onClick.Invoke();
            }
        }
    }

    private bool IsCursorOnButton(Button button)
    {
        RectTransform rectTransform = button.GetComponent<RectTransform>();
        Vector3 buttonMin = _screen.transform.InverseTransformPoint(new Vector2(rectTransform.position.x + rectTransform.rect.width / 2, rectTransform.position.y - rectTransform.rect.height / 2));
        Vector3 buttonMax = _screen.transform.InverseTransformPoint(new Vector2(rectTransform.position.x - rectTransform.rect.width / 2, rectTransform.position.y + rectTransform.rect.height / 2));
        Vector3 cursorPosition = _screen.transform.InverseTransformPoint(_cursor.position);
        return cursorPosition.x >= buttonMin.x && cursorPosition.x <= buttonMax.x && cursorPosition.y >= buttonMin.y && cursorPosition.y <= buttonMax.y;
    }
    
    public void OnDexButtonClick()
    {
        SwitchToApp(_dexApp);
    }

    public void OnScanButtonClick()
    {
        SwitchToApp(_scanApp);
    }
    
    private IEnumerator ProcessOperationCoroutine(Action<float> callback, float duration, float callbackInterval)
    {
        PlayScanSoundLoop();
        PlayScanParticle();
        float time = 0;
        float timeBetweenCallbacks = duration * callbackInterval;
        while (time < duration)
        {
            callback(time / duration);
            time += timeBetweenCallbacks;
            yield return new WaitForSeconds(timeBetweenCallbacks);
        }

        _currentOperationCoroutine = null;
        StopScanSoundLoop();
        StopScanParticle();
        callback(1);
    }

    public void ProcessOperation(Action<float> callback, float duration, float callbackInterval)
    {
        _currentOperationCoroutine = StartCoroutine(ProcessOperationCoroutine(callback, duration, callbackInterval));
    }
    
    public void StopOperation()
    {
        if (_currentOperationCoroutine != null)
        {
            StopScanSoundLoop();
            StopScanParticle();
            StopCoroutine(_currentOperationCoroutine);
            _currentOperationCoroutine = null;
        }
    }
    
    public void PlayErrorSound()
    {
        _audioSource.PlayOneShot(_errorSound);
    }
    
    public void PlaySuccessSound()
    {
        _audioSource.PlayOneShot(_successSound);
    }
    
    public void PlayScanParticle()
    {
        ParticleSystem.gameObject.SetActive(true);
    }
    
    public void StopScanParticle()
    {
        ParticleSystem.gameObject.SetActive(false);
    }

    public void PlayScanSoundLoop()
    {
        _audioSource.loop = true;
        _audioSource.clip = _scanSound;
        _audioSource.Play();
    }
    
    public void StopScanSoundLoop()
    {
        _audioSource.loop = false;
        _audioSource.Stop();
    }
    
    internal static void LoadAssets(AssetBundle assetBundle)
    {
        pcPrefab = assetBundle.LoadAsset<GameObject>("Assets/PC/PC.prefab");
        pcPrefab.AddComponent<PC>();
        
        _errorSound = assetBundle.LoadAsset<AudioClip>("Assets/PC/Sounds/error.mp3");
        _successSound = assetBundle.LoadAsset<AudioClip>("Assets/PC/Sounds/success.mp3");
        _scanSound = assetBundle.LoadAsset<AudioClip>("Assets/PC/Sounds/scan.mp3");
        
        LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(pcPrefab);
    }

    internal static void AddToShip()
    {
        StartOfRound.Instance.unlockablesList.unlockables.Add(new UnlockableItem
        {
            alreadyUnlocked = true,
            alwaysInStock = false,
            canBeStored = false,
            hasBeenMoved = false,
            hasBeenUnlockedByPlayer = false,
            headCostumeObject = null,
            inStorage = false,
            jumpAudio = null,
            maxNumber = 1,
            placedPosition = Vector3.zero,
            placedRotation = Vector3.zero,
            prefabObject = pcPrefab,
            spawnPrefab = true,
            suitMaterial = null,
            unlockableName = UnlockableName,
            unlockableType = 1,
            IsPlaceable = true,
            shopSelectionNode = null,
            lowerTorsoCostumeObject = null,
            unlockedInChallengeFile = true
        });
    }
}