using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LethalMon.PC;

public class TutorialApp : PCApp
{
    #region AppComponents
    private readonly Button _nextPageButton;
    
    private readonly Button _previousPageButton;

    private readonly GameObject[] _tutorialPages;
    #endregion
    
    #region Properties
    private int _currentPage;
    #endregion
    
    public TutorialApp(GameObject screen) : base(screen, screen.transform.Find("Window/TutorialMenu").gameObject, "Tutorial")
    {
        _currentPage = 0;
        
        _tutorialPages = new GameObject[3];
        for (var i = 0; i < _tutorialPages.Length; i++)
        {
            _tutorialPages[i] = screen.transform.Find($"Window/TutorialMenu/Page{i + 1}").gameObject;
        }
        
        // Change tutorial page depending on save type
        UpdateTutorialPage2();
        
        _nextPageButton = screen.transform.Find("Window/TutorialMenu/NextPage").GetComponent<Button>();
        _previousPageButton = screen.transform.Find("Window/TutorialMenu/PreviousPage").GetComponent<Button>();

        _nextPageButton.onClick.AddListener(NextPage);
        _previousPageButton.onClick.AddListener(PreviousPage);
    }
    
    public override void Show()
    {
        _currentPage = 0;
        UpdatePage(_currentPage);
        HideOrShowNextPreviousButtons();
        
        base.Show();
    }
    
    private void NextPage()
    {
        _currentPage++;

        UpdatePage(_currentPage);
        PC.Instance.TutorialUpdatePageServerRpc(_currentPage);
    }
    
    private void PreviousPage()
    {
        _currentPage--;
        
        UpdatePage(_currentPage);
        PC.Instance.TutorialUpdatePageServerRpc(_currentPage);
    }
    
    private void HideOrShowNextPreviousButtons()
    {
        _nextPageButton.gameObject.SetActive(_currentPage < _tutorialPages.Length - 1);
        _previousPageButton.gameObject.SetActive(_currentPage > 0);
    }

    public void UpdatePage(int page)
    {
        for (var i = 0; i < _tutorialPages.Length; i++)
        {
            _tutorialPages[i].SetActive(i == page);
        }
        
        HideOrShowNextPreviousButtons();
    }

    public void UpdateTutorialPage2()
    {
        if (ModConfig.Instance.values.PcGlobalSave)
        {
            _tutorialPages[2].transform.Find("Desc").GetComponent<TextMeshProUGUI>().text = "PC session is per employee.\n\nIt means that all employees have different dex and duplicate entries, depending on what they scanned individually.";
        }
        else
        {
            _tutorialPages[2].transform.Find("Desc").GetComponent<TextMeshProUGUI>().text = "PC session is shared between all employees.\n\nIt means that all employees have the same dex and duplicate entries.";
        }
    }
}