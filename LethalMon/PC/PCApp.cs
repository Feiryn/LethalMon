using TMPro;
using UnityEngine;

namespace LethalMon.PC;

public abstract class PCApp
{
    #region MenuComponents

    private readonly GameObject _window;

    private readonly TextMeshProUGUI _windowTitle;

    private readonly GameObject _menuGameObject;

    private readonly string _title;
    #endregion

    protected PCApp(GameObject screen, GameObject menuGameObject, string title)
    {
        _window = screen.transform.Find("Window")!.gameObject;
        _windowTitle = _window.transform.Find("WindowTitle")!.GetComponent<TextMeshProUGUI>();
        _menuGameObject = menuGameObject;
        _title = title;
    }
    
    public virtual void Show()
    {
        _window.SetActive(true);
        _menuGameObject.SetActive(true);
        _windowTitle.text = _title;
    }
    
    public virtual void Hide()
    {
        _window.SetActive(false);
        _menuGameObject.SetActive(false);
    }
}