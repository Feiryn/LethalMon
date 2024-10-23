using LethalMon.Items;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace LethalMon.PC;

internal class DuplicateApp : PCApp
{
    #region AppComponents
    private readonly Button _duplicateButton;

    private readonly TextMeshProUGUI _errorText;
    
    private readonly TextMeshProUGUI _successText;

    private readonly Image _progressBar;
    #endregion
    
    #region Constants
    public const float DuplicationTime = 10f;
    
    public const float ProgressBarStep = 0.02829f;
    
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
    
    public void CleanUp()
    {
        _errorText.SetText("");
        _successText.SetText("");
        _progressBar.fillAmount = 0;
        
        if (SelectedMonster != string.Empty)
        {
            CatchableEnemy.CatchableEnemy enemy = Registry.GetCatchableEnemy(SelectedMonster)!;
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
            DuplicationError("No ball detected!", true);
            return;
        }
        
        if (Object.FindObjectOfType<Terminal>().groupCredits < Registry.GetCatchableEnemy(SelectedMonster)!.DuplicationPrice)
        {
            DuplicationError("Not enough credits!", true);
            return;
        }
        
        PC.Instance.DuplicationStartServerRpc();
    }
    
    public void DuplicationError(string errorText, bool callRpc = false)
    {
        _errorText.SetText(errorText);
        _progressBar.fillAmount = 0f;
        PC.Instance.StopOperation();
        PC.Instance.PlayErrorSound();
        
        if (callRpc)
        {
            PC.Instance.DuplicationErrorServerRpc(errorText);
        }
    }
    
    public void DuplicationSuccess(string successText)
    {
        _successText.SetText(successText);
        PC.Instance.PlaySuccessSound();
    }
    
    public void FillProgressBar(float progress)
    {
        _progressBar.fillAmount = progress;
    }
    
    public void DuplicateCallback(float progress)
    {
        FillProgressBar(progress);

        PC pc = PC.Instance;
        BallItem? currentBall = pc.GetCurrentPlacedBall();

        if (currentBall == null)
        {
            DuplicationError("Ball removed during duplication!", true);
            return;
        }
        
        if (NotEmptyBallProgressCheckpoint > progress - ProgressBarStep && currentBall.enemyCaptured || currentBall.enemyType != null)
        {
            DuplicationError("The ball is not empty!", true);
            return;
        }

        if (progress >= 1f)
        {
            int price = Registry.GetCatchableEnemy(SelectedMonster)!.DuplicationPrice;
            
            if (Object.FindObjectOfType<Terminal>().groupCredits < price)
            {
                DuplicationError("Not enough credits!", true);
                return;
            }
            
            currentBall.SetCaughtEnemyServerRpc(SelectedMonster, string.Empty, price);
            
            string successText = $"Duplication of {Registry.GetCatchableEnemy(SelectedMonster)!.DisplayName} successful!";
            
            DuplicationSuccess(successText);
            PC.Instance.DuplicationSuccessServerRpc(successText);
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