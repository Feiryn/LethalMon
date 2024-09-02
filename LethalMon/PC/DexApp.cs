using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LethalMon.PC;

public class DexApp : PCApp
{
    #region MenuComponents

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

        _enemies = Data.CatchableMonsters.OrderBy(kvp => kvp.Value.DisplayName).Select(entry => entry.Key).ToArray();
        
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

    private void UpdateMonsterInfo(string enemyName)
    {
        _rightColumn.SetActive(true);
        CatchableEnemy.CatchableEnemy catchableEnemy = Data.CatchableMonsters[enemyName];
        _monsterName.text = catchableEnemy.DisplayName;
        _captureRates.text = $"Pokeball: {Mathf.Floor(catchableEnemy.GetCaptureProbability(0) * 100)}%\n\nGreat ball: {Mathf.Floor(catchableEnemy.GetCaptureProbability(1) * 100)}%\n\nUltra ball: {Mathf.Floor(catchableEnemy.GetCaptureProbability(2) * 100)}%\n\nMaster ball: {Mathf.Floor(catchableEnemy.GetCaptureProbability(3) * 100)}%";
        _behaviourDescription.text = catchableEnemy.BehaviourDescription;
        _monsterImage.sprite = LethalMon.monstersSprites[enemyName.ToLower()];
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
            _monstersButtons[i].GetComponentInChildren<TextMeshProUGUI>().text = Data.CatchableMonsters[_enemies[index]].DisplayName;
            _monstersButtons[i].onClick.RemoveAllListeners();
            _monstersButtons[i].onClick.AddListener(() => UpdateMonsterInfo(_enemies[index]));
        }
    }

    private void HideOrShowNextPreviousButtons()
    {
        _nextPageButton.gameObject.SetActive(_enemies.Length - (_currentPage + 1) * _monstersButtons.Length > 0);
        _previousPageButton.gameObject.SetActive(_currentPage - 1 >= 0);
    }

    private void NextPage()
    {
        if (_enemies.Length - (_currentPage + 1) * _monstersButtons.Length < 0)
            return;
        
        _currentPage++;
        UpdateMonstersButtons();
        HideOrShowNextPreviousButtons();
    }

    private void PreviousPage()
    {
        if (_currentPage - 1 < 0)
            return;
        
        _currentPage--;
        UpdateMonstersButtons();
        HideOrShowNextPreviousButtons();
    }
}