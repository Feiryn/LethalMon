using System.Linq;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LethalMon.PC;

public class PC : NetworkBehaviour
{
    internal static GameObject? pcPrefab = null;

    #region Constants
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
    #endregion
    
    #region PCApp
    private Button _appCloseButton;
    
    private DexApp? _currentApp;
    
    private DexApp _dexApp;
    #endregion

    public void Start()
    {
        LethalMon.Log("PC Start");
        
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
        
        // Assign buttons to functions
        gameObject.transform.Find("Screen/MainMenu/DexButton").GetComponent<Button>().onClick = FunctionToButtonClickEvent(OnDexButtonClick);
        
        // Load PC apps
        _appCloseButton = _screen.transform.Find("Window/CloseButton").GetComponent<Button>();
        _appCloseButton.onClick.AddListener(CloseCurrentApp);
        _dexApp = new DexApp(_screen);
        _dexApp.Hide();
    }

    private static Button.ButtonClickedEvent FunctionToButtonClickEvent(UnityAction action)
    {
        var buttonClickedEvent = new Button.ButtonClickedEvent();
        buttonClickedEvent.AddListener(action);
        return buttonClickedEvent;
    }
    
    public void OnBallPlaceInteract(PlayerControllerB player)
    {
        LethalMon.Log("Ball place interact");
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
        }
    }
    
    public void CloseCurrentApp()
    {
        if (_currentApp != null)
        {
            _currentApp.Hide();
            _currentApp = null;
        }
    }
    
    public void PressEsc(InputAction.CallbackContext context)
    {
        if (_currentApp != null)
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
        Vector3 buttonMin = _screen.transform.InverseTransformPoint(new Vector2(rectTransform.position.x - rectTransform.rect.width / 2, rectTransform.position.y - rectTransform.rect.height / 2));
        Vector3 buttonMax = _screen.transform.InverseTransformPoint(new Vector2(rectTransform.position.x + rectTransform.rect.width / 2, rectTransform.position.y + rectTransform.rect.height / 2));
        Vector3 cursorPosition = _screen.transform.InverseTransformPoint(_cursor.position);
        return cursorPosition.x >= buttonMin.x && cursorPosition.x <= buttonMax.x && cursorPosition.y >= buttonMin.y && cursorPosition.y <= buttonMax.y;
    }
    
    public void OnDexButtonClick()
    {
        _dexApp.Show();
        _currentApp = _dexApp;
    }
    
    internal static void LoadAssets(AssetBundle assetBundle)
    {
        pcPrefab = assetBundle.LoadAsset<GameObject>("Assets/PC/PC.prefab");
        pcPrefab.AddComponent<PC>();
        
        LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(pcPrefab);
    }
}