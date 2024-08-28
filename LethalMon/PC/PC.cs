using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

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
    }
    
    public void OnBallPlaceInteract(PlayerControllerB player)
    {
        LethalMon.Log("Ball place interact");
    }
    
    public void StartUsing(PlayerControllerB player)
    {
        // Don't trust Zeekerss code... the player is always null on an interact early event
        player = Utils.CurrentPlayer;
        
        LethalMon.Log("Start using");

        try
        {
            _currentPlayer = player;
            
            player.inTerminalMenu = true;

            HUDManager.Instance.ChangeControlTip(0,
                StartOfRound.Instance.localPlayerUsingController ? "Quit PC : [Start]" : "Quit PC : [TAB]",
                clearAllOther: true);
            
            _playerActions.Movement.OpenMenu.performed += PressEsc;
            _playerActions.Movement.Move.performed += Move_performed;
            _playerActions.Movement.Look.performed += Look_performed;
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
        LethalMon.Log("Stop using");
        
        if (_currentPlayer != null)
        {
            _playerActions.Movement.OpenMenu.performed -= PressEsc;
            _playerActions.Movement.Move.performed -= Move_performed;
            _playerActions.Movement.Look.performed -= Look_performed;
            _playerActions.Disable();
            _currentPlayer.playerActions.Movement.Move.Enable();
            _currentPlayer.playerActions.Movement.Look.Enable();
            
            _currentPlayer.inTerminalMenu = false;
            _screenInteractTrigger.StopSpecialAnimation();
        }
    }
    
    public void PressEsc(InputAction.CallbackContext context)
    {
        LethalMon.Log("Press ESC");
        StopUsing();
    }
    
    public void Move_performed(InputAction.CallbackContext context)
    {
        LethalMon.Log("Move performed");
        
        Vector2 move = context.ReadValue<Vector2>();
        LethalMon.Log($"Move: {move}");
    }
    
    public void Look_performed(InputAction.CallbackContext context)
    {
        LethalMon.Log("Look performed");
        
        Vector2 move = context.ReadValue<Vector2>();
        LethalMon.Log($"Look: {move}");
        _cursor.anchoredPosition = new Vector2(Mathf.Clamp(_cursor.anchoredPosition.x - move.x * CursorSpeed, CursorMinX, CursorMaxX), Mathf.Clamp(_cursor.anchoredPosition.y + move.y * CursorSpeed, CursorMinY, CursorMaxY));
    }
    
    internal static void LoadAssets(AssetBundle assetBundle)
    {
        pcPrefab = assetBundle.LoadAsset<GameObject>("Assets/PC/PC.prefab");
        pcPrefab.AddComponent<PC>();
        
        LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(pcPrefab);
    }
}