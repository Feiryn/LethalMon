using System;
using LethalMon.Items;
using LethalMon.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LethalMon.PC;

public class DuplicateApp : PCApp
{
    #region AppComponents
    private readonly Button _duplicateButton;

    private readonly TextMeshProUGUI _errorText;
    
    private readonly TextMeshProUGUI _successText;

    private readonly Image _progressBar;
    #endregion
    
    #region Constants
    private const float DuplicateTime = 10f;
    
    private const float ProgressBarStep = 0.02829f;
    
    private const float NotEmptyBallProgressCheckpoint = 0.3f;
    #endregion

    public string SelectedMonster = string.Empty;
    
    public DuplicateApp(GameObject screen) : base(screen, screen.transform.Find("Window/DuplicateMenu").gameObject, "Duplicate")
    {
        _duplicateButton = screen.transform.Find("Window/DuplicateMenu/DuplicateButton").GetComponent<Button>();
        _errorText = screen.transform.Find("Window/DuplicateMenu/ErrorText").GetComponent<TextMeshProUGUI>();
        _successText = screen.transform.Find("Window/DuplicateMenu/SuccessText").GetComponent<TextMeshProUGUI>();
        _progressBar = screen.transform.Find("Window/DuplicateMenu/ProgressBar").GetComponent<Image>();
        
        _duplicateButton.onClick.AddListener(Duplicate);

        CleanUp();
    }
    
    private void CleanUp()
    {
        _errorText.SetText("");
        _successText.SetText("");
        _progressBar.fillAmount = 0;
        
        if (SelectedMonster != string.Empty)
        {
            CatchableEnemy.CatchableEnemy enemy = Data.CatchableMonsters[SelectedMonster];
            _duplicateButton.GetComponentInChildren<TextMeshProUGUI>().text = "> Duplicate <\nMonster: " + enemy.DisplayName + "\nPrice: " + enemy.DuplicationPrice + " ▮";
        }
    }
    
    public override void Show()
    {
        CleanUp();
        
        base.Show();
    }
    
    private void Duplicate()
    {
        PC pc = PC.Instance;

        if (pc._currentOperationCoroutine != null)
        {
            return;
        }

        CleanUp();
        
        if (pc.GetCurrentPlacedBall() == null)
        {
            DuplicationError("No ball detected!");
            return;
        }
        
        if (Object.FindObjectOfType<Terminal>().groupCredits < Data.CatchableMonsters[SelectedMonster].DuplicationPrice)
        {
            DuplicationError("Not enough credits!");
            return;
        }
        
        pc.ProcessOperation(DuplicateCallback, DuplicateTime, ProgressBarStep);
    }
    
    private void DuplicationError(string errorText)
    {
        _errorText.SetText(errorText);
        _progressBar.fillAmount = 0f;
        PC.Instance.StopOperation();
        PC.Instance.PlayErrorSound();
    }
    
    
    private void DuplicateCallback(float progress)
    {
        _progressBar.fillAmount = progress;

        PC pc = PC.Instance;
        PokeballItem? currentBall = pc.GetCurrentPlacedBall();

        if (currentBall == null)
        {
            DuplicationError("Ball removed during duplication!");
            return;
        }
        
        if (NotEmptyBallProgressCheckpoint > progress - ProgressBarStep && currentBall.enemyCaptured || currentBall.enemyType != null)
        {
            DuplicationError("The ball is not empty!");
            return;
        }

        if (progress >= 1f)
        {
            int price = Data.CatchableMonsters[SelectedMonster].DuplicationPrice;
            
            if (Object.FindObjectOfType<Terminal>().groupCredits < price)
            {
                DuplicationError("Not enough credits!");
                return;
            }
            
            currentBall.SetCaughtEnemyServerRpc(SelectedMonster, price);
            
            string successText = $"Duplication of {Data.CatchableMonsters[SelectedMonster].DisplayName} successful!";
            _successText.SetText(successText);
            PC.Instance.PlaySuccessSound();
        }
    }
    
    public override void Hide()
    {
        base.Hide();
        
        if (SelectedMonster != string.Empty)
        {
            PC.Instance.SwitchToApp(PC.Instance.duplicateChooseApp);
        }
    }
}