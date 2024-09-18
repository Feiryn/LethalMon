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
}