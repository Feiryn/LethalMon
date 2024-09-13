using System.Linq;
using LethalMon.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LethalMon.PC;

public class DuplicateChooseApp : PCApp
{
    #region AppComponents
    private readonly Button[] _monstersButtons;
    
    private readonly Button _nextPageButton;
    
    private readonly Button _previousPageButton;
    #endregion
    
    private readonly string[] _enemies;
    
    private int _currentPage;
    
    public string[] unlockedDnaEntries;
    
    public DuplicateChooseApp(GameObject screen) : base(screen, screen.transform.Find("Window/DuplicateChooseMenu").gameObject, "Duplication choice")
    {
        _currentPage = 0;
        
        _monstersButtons = screen.transform.Find("Window/DuplicateChooseMenu/Monsters").GetComponentsInChildren<Button>();
        _nextPageButton = screen.transform.Find("Window/DuplicateChooseMenu/NextPage").GetComponent<Button>();
        _previousPageButton = screen.transform.Find("Window/DuplicateChooseMenu/PreviousPage").GetComponent<Button>();

        _enemies = Data.CatchableMonsters.OrderBy(kvp => kvp.Value.DisplayName).Select(entry => entry.Key).ToArray();
        
        _nextPageButton.onClick.AddListener(NextPage);
        _previousPageButton.onClick.AddListener(PreviousPage);
    }
    
    public override void Show()
    {
        _currentPage = 0;
        UpdateMonstersButtons();
        HideOrShowNextPreviousButtons();
        
        base.Show();
    }
    
    private void UpdateMonstersButtons()
    {
        for (var i = 0; i < _monstersButtons.Length; i++)
        {
            var index = i + _currentPage * _monstersButtons.Length;
            if (index < _enemies.Length)
            {
                var enemy = _enemies[index];
                var button = _monstersButtons[i];
                button.gameObject.SetActive(true);
                Image avatar = button.transform.Find("Avatar").GetComponent<Image>();
                TextMeshProUGUI monsterName = button.GetComponentInChildren<TextMeshProUGUI>();
                
                if (unlockedDnaEntries.Contains(enemy))
                {
                    monsterName.text = Data.CatchableMonsters[enemy].DisplayName;
                    avatar.sprite = LethalMon.monstersSprites[enemy.ToLower()];
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => Duplicate(enemy));
                }
                else
                {
                    monsterName.text = "???";
                    avatar.sprite = LethalMon.monstersSprites["unknown"];
                    button.onClick.RemoveAllListeners();
                }
            }
            else
            {
                _monstersButtons[i].gameObject.SetActive(false);
            }
        }
    }
    
    public void UpdatePage(int page)
    {
        _currentPage = page;
        UpdateMonstersButtons();
        HideOrShowNextPreviousButtons();
    }
    
    private void NextPage()
    {
        _currentPage++;
        
        UpdatePage(_currentPage);
        PC.Instance.UpdateDuplicateChoosePageServerRpc(_currentPage);
    }
    
    private void PreviousPage()
    {
        _currentPage--;
        
        UpdatePage(_currentPage);
        PC.Instance.UpdateDuplicateChoosePageServerRpc(_currentPage);
    }
    
    private void HideOrShowNextPreviousButtons()
    {
        _nextPageButton.gameObject.SetActive(_enemies.Length > (_currentPage + 1) * _monstersButtons.Length);
        _previousPageButton.gameObject.SetActive(_currentPage > 0);
    }
    
    private void Duplicate(string enemy)
    {
        PC.Instance.duplicateApp.SelectedMonster = enemy;
        PC.Instance.SwitchToApp(PC.Instance.duplicateApp, false);
        PC.Instance.OpenDuplicateServerRpc(enemy);
    }
}