using LethalMon.Items;
using LethalMon.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LethalMon.PC;

public class ScanApp : PCApp
{
    #region AppComponents

    private readonly Button _scanButton;

    private readonly TextMeshProUGUI _errorText;
    
    private readonly TextMeshProUGUI _successText;

    private readonly Image _progressBar;
    #endregion

    #region Constants

    private const float ScanTime = 6f;
    
    private const float ProgressBarStep = 0.02829f;
    #endregion
    
    #region ScanCheckpoints
    private const float EmptyBallProgressCheckpoint = 0.3f;
    
    private const float MissingDnaProgressCheckpoint = 0.8f;
    #endregion
    
    private bool _lastScanUnlockedDexEntry;
    
    public ScanApp(GameObject screen) : base(screen, screen.transform.Find("Window/ScanMenu").gameObject, "Scan")
    {
        _scanButton = screen.transform.Find("Window/ScanMenu/ScanButton").GetComponent<Button>();
        _errorText = screen.transform.Find("Window/ScanMenu/ErrorText").GetComponent<TextMeshProUGUI>();
        _successText = screen.transform.Find("Window/ScanMenu/SuccessText").GetComponent<TextMeshProUGUI>();
        _progressBar = screen.transform.Find("Window/ScanMenu/ProgressBar").GetComponent<Image>();
        
        _scanButton.onClick.AddListener(Scan);

        CleanUp();
    }
    
    private void CleanUp()
    {
        _lastScanUnlockedDexEntry = false;
        _errorText.SetText("");
        _successText.SetText("");
        _progressBar.fillAmount = 0;
    }

    public override void Show()
    {
        CleanUp();
        
        base.Show();
    }

    private void Scan()
    {
        PC pc = PC.Instance;

        if (pc._currentOperationCoroutine != null)
        {
            return;
        }
        
        if (pc.GetCurrentPlacedBall() == null)
        {
            ScanError("No ball detected!");
            return;
        }

        CleanUp();
        pc.ProcessOperation(ScanCallback, ScanTime, ProgressBarStep);
    }

    private void ScanError(string errorText)
    {
        _errorText.SetText(errorText);
        _progressBar.fillAmount = 0f;
        PC.Instance.StopOperation();
        PC.Instance.PlayErrorSound();
    }

    private void ScanCallback(float progress)
    {
        _progressBar.fillAmount = progress;

        PC pc = PC.Instance;
        PokeballItem? currentBall = pc.GetCurrentPlacedBall();

        if (currentBall == null)
        {
            ScanError("Ball removed during scan!");
            return;
        }
        
        if (EmptyBallProgressCheckpoint > progress - ProgressBarStep && !currentBall.enemyCaptured || currentBall.enemyType == null)
        {
            ScanError("The ball is empty!");
            return;
        }
        
        if (progress + ProgressBarStep <= MissingDnaProgressCheckpoint)
        {
            return;
        }
        
        _lastScanUnlockedDexEntry = !SaveManager.IsDexEntryUnlocked(currentBall.enemyType.name);
        if (_lastScanUnlockedDexEntry)
        {
            SaveManager.UnlockDexEntry(currentBall.enemyType.name);
        }
        
        if (MissingDnaProgressCheckpoint > progress - ProgressBarStep && MissingDnaProgressCheckpoint < progress + ProgressBarStep && !currentBall.isDnaComplete)
        {
            string errorText =
                "Some DNA fragments are missing from the monster in the ball. The monster's DNA may have deteriorated with time or come from a duplicated monster.";
            if (_lastScanUnlockedDexEntry)
            {
                errorText += "\nThe monster has been added to the dex though.";
            }
            ScanError(errorText);
            return;
        }

        if (progress >= 1f)
        {
            bool unlockedDna = !SaveManager.IsDnaUnlocked(currentBall.enemyType.name);
            if (unlockedDna)
            {
                SaveManager.UnlockDna(currentBall.enemyType.name);
            }
            
            string successText = $"Scan of {Data.CatchableMonsters[currentBall.enemyType.name].DisplayName} successful!";
            if (_lastScanUnlockedDexEntry)
            {
                successText += "\nThe monster has been added to the dex.";
            }
            if (unlockedDna)
            {
                successText += "\nThe monster's DNA has been decrypted with success.";
            }
            if (!_lastScanUnlockedDexEntry && !unlockedDna)
            {
                successText += "\nThe monster is already in the dex and its DNA has already been decrypted.";
            }
            _successText.SetText(successText);
            PC.Instance.PlaySuccessSound();
        }
    }
}