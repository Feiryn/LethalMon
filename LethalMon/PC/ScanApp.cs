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

    public const float ScanTime = 6f;
    
    public const float ProgressBarStep = 0.02829f;
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
    
    public void CleanUp()
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
            ScanError("No ball detected!", true);
            return;
        }

        PC.Instance.ScanStartServerRpc();
    }

    public void ScanError(string errorText, bool callRpc = false)
    {
        _errorText.SetText(errorText);
        _progressBar.fillAmount = 0f;
        PC.Instance.StopOperation();
        PC.Instance.PlayErrorSound();
        
        if (callRpc)
        {
            PC.Instance.ScanErrorServerRpc(errorText);
        }
    }

    public void ScanSuccess(string successText)
    {
        _successText.SetText(successText);
        PC.Instance.PlaySuccessSound();
    }
    
    public void FillProgressBar(float progress)
    {
        _progressBar.fillAmount = progress;
    }

    public void ScanCallback(float progress)
    {
        FillProgressBar(progress);
        
        PC pc = PC.Instance;
        PokeballItem? currentBall = pc.GetCurrentPlacedBall();

        if (currentBall == null)
        {
            ScanError("Ball removed during scan!", true);
            return;
        }
        
        if (EmptyBallProgressCheckpoint > progress - ProgressBarStep && !currentBall.enemyCaptured || currentBall.enemyType == null)
        {
            ScanError("The ball is empty!", true);
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
            ScanError(errorText, true);
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
            
            ScanSuccess(successText);
            PC.Instance.ScanSuccessServerRpc(successText);
        }
    }
}