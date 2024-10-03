using System.Linq;
using LethalMon.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LethalMon.PC;

internal class DexApp : PCApp
{
    #region AppComponents

    private readonly Button[] _monstersButtons;
    
    private readonly Button _nextPageButton;
    
    private readonly Button _previousPageButton;

    private readonly TextMeshProUGUI _monsterName;

    private readonly TextMeshProUGUI _captureRates;

    private readonly TextMeshProUGUI _behaviourDescription;

    private readonly Image _monsterImage;
    
    private readonly GameObject _rightColumn;
    #endregion

    private readonly string[] _enemies;
    
    private int _currentPage;
    
    public string[] unlockedDexEntries;

    public DexApp(GameObject screen) : base(screen, screen.transform.Find("Window/DexMenu").gameObject, "Dex")
    {
        _currentPage = 0;
        
        _monstersButtons = screen.transform.Find("Window/DexMenu/LeftColumn/MonstersList").GetComponentsInChildren<Button>();
        _nextPageButton = screen.transform.Find("Window/DexMenu/LeftColumn/NextPage").GetComponent<Button>();
        _previousPageButton = screen.transform.Find("Window/DexMenu/LeftColumn/PreviousPage").GetComponent<Button>();
        _monsterName = screen.transform.Find("Window/DexMenu/RightColumn/MonsterName").GetComponent<TextMeshProUGUI>();
        _captureRates = screen.transform.Find("Window/DexMenu/RightColumn/CaptureRates").GetComponent<TextMeshProUGUI>();
        _behaviourDescription = screen.transform.Find("Window/DexMenu/RightColumn/Behaviour").GetComponent<TextMeshProUGUI>();
        _monsterImage = screen.transform.Find("Window/DexMenu/RightColumn/MonsterImage").GetComponent<Image>();
        _rightColumn = screen.transform.Find("Window/DexMenu/RightColumn").gameObject;

        _enemies = Registry.CatchableEnemies.OrderBy(kvp => kvp.Value.DisplayName).Select(entry => entry.Key).ToArray();
        
        _nextPageButton.onClick.AddListener(NextPage);
        _previousPageButton.onClick.AddListener(PreviousPage);
    }

    public override void Show()
    {
        _rightColumn.SetActive(false);
        _currentPage = 0;
        UpdateMonstersButtons();
        HideOrShowNextPreviousButtons();
        
        base.Show();
    }

    public void UpdateMonsterInfo(string enemyName)
    {
        _rightColumn.SetActive(true);
        
        if (unlockedDexEntries.Contains(enemyName))
        {
            CatchableEnemy.CatchableEnemy catchableEnemy = Registry.GetCatchableEnemy(enemyName)!;
            _monsterName.text = catchableEnemy.DisplayName;
            _captureRates.text = $"Pokeball: {Mathf.Floor(catchableEnemy.GetCaptureProbability(0) * 100)}%\n\nGreat ball: {Mathf.Floor(catchableEnemy.GetCaptureProbability(1) * 100)}%\n\nUltra ball: {Mathf.Floor(catchableEnemy.GetCaptureProbability(2) * 100)}%\n\nMaster ball: {Mathf.Floor(catchableEnemy.GetCaptureProbability(3) * 100)}%";
            _behaviourDescription.text = catchableEnemy.BehaviourDescription;
            _monsterImage.sprite = Registry.GetEnemySprite(enemyName);
        }
        else
        {
            _monsterName.text = "??????";
            _captureRates.text = $"Pokeball: ???%\n\nGreat ball: ???%\n\nUltra ball: ???%\n\nMaster ball: ???%";
            _behaviourDescription.text = "??????";
            _monsterImage.sprite = Registry.FallbackSprite;
        }
    }

    private void UpdateMonstersButtons()
    {
        for (int i = 0; i < _monstersButtons.Length; ++i)
        {
            int index = i + _currentPage * _monstersButtons.Length;
            if (index >= _enemies.Length)
            {
                _monstersButtons[i].gameObject.SetActive(false);
                continue;
            }
            
            _monstersButtons[i].gameObject.SetActive(true);
            _monstersButtons[i].onClick.RemoveAllListeners();
            _monstersButtons[i].onClick.AddListener(() =>
            {
                UpdateMonsterInfo(_enemies[index]);
                PC.Instance.LoadDexEntryServerRpc(_enemies[index]);
            });
            _monstersButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = unlockedDexEntries.Contains(_enemies[index]) ? Registry.GetCatchableEnemy(_enemies[index])!.DisplayName : "??????";
        }
    }

    private void HideOrShowNextPreviousButtons()
    {
        _nextPageButton.gameObject.SetActive(_enemies.Length - (_currentPage + 1) * _monstersButtons.Length > 0);
        _previousPageButton.gameObject.SetActive(_currentPage - 1 >= 0);
    }

    public void UpdatePage(int page)
    {
        _currentPage = page;
        UpdateMonstersButtons();
        HideOrShowNextPreviousButtons();
    }

    private void NextPage()
    {
        if (_enemies.Length - (_currentPage + 1) * _monstersButtons.Length < 0)
            return;
        
        _currentPage++;
        UpdateMonstersButtons();
        HideOrShowNextPreviousButtons();
        PC.Instance.DexUpdatePageServerRpc(_currentPage);
    }

    private void PreviousPage()
    {
        if (_currentPage - 1 < 0)
            return;
        
        _currentPage--;
        UpdateMonstersButtons();
        HideOrShowNextPreviousButtons();
        PC.Instance.DexUpdatePageServerRpc(_currentPage);
    }
}