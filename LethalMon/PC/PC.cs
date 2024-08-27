using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace LethalMon.PC;

public class PC : NetworkBehaviour
{
    internal static GameObject? pcPrefab = null;

    #region Components
    private InteractTrigger _ballPlaceInteractTrigger;
    
    private InteractTrigger _screenInteractTrigger;
    #endregion

    public void Start()
    {
        LethalMon.Log("PC Start");
        
        // Load the triggers from the prefab and set missing properties
        InteractTrigger[] interactTriggers = GetComponentsInChildren<InteractTrigger>();
        _ballPlaceInteractTrigger = interactTriggers[0];
        _screenInteractTrigger = interactTriggers[1];
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
        _screenInteractTrigger.onInteract.AddListener(StartUsing);

        Terminal terminal = FindObjectOfType<Terminal>();
        LethalMon.Log("Terminal dump");
        LethalMon.Log(Newtonsoft.Json.JsonConvert.SerializeObject(terminal));
    }
    
    public void OnBallPlaceInteract(PlayerControllerB player)
    {
        LethalMon.Log("Ball place interact");
    }
    
    public void StartUsing(PlayerControllerB player)
    {
        LethalMon.Log("Start using");

        try
        {
            player.inTerminalMenu = true;

            HUDManager.Instance.ChangeControlTip(0,
                StartOfRound.Instance.localPlayerUsingController ? "Quit PC : [Start]" : "Quit PC : [TAB]",
                clearAllOther: true);
        }
        catch
        {
            StopUsing(player);
        }
    }
    
    public void StopUsing(PlayerControllerB player)
    {
        LethalMon.Log("Stop using");
        player.inTerminalMenu = false;
    }
    
    internal static void LoadAssets(AssetBundle assetBundle)
    {
        pcPrefab = assetBundle.LoadAsset<GameObject>("Assets/PC/PC.prefab");
        pcPrefab.AddComponent<PC>();
        
        LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(pcPrefab);
    }
}