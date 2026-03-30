using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TimeModeController : MonoBehaviour
{
    [Header("Timer")]
    [SerializeField] private float timeLimitSeconds = 30f;
    [SerializeField] private TextMeshProUGUI timerLabel;
    [SerializeField] private Image ringTimerImage;

    [Header("Gameplay Refs")]
    [SerializeField] private MixedPointSpawner spawner;
    [SerializeField] private MonoBehaviour playerInput;

    [Header("Score")]
    [SerializeField] private ScoreManager scoreManager;

    private float timeLeft;
    private bool running;

    // Nach deinem Pre-Countdown aufrufen
    public void BeginAfterCountdown()
    {
        timeLeft = timeLimitSeconds;
        running = true;
        UpdateTimerUI();
    }

    private void Update()
    {
        if (!running) return;

        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            running = false;

            // SFX/Music für Time-Mode (falls gewünscht)
            SfxManager.Instance?.PlayTimeModeTimeUp();
            MusicManager.Instance?.ResetGameOnGameOver();

            EndGame();
        }
        UpdateTimerUI();
    }

    private void UpdateTimerUI()
    {
        int s = Mathf.CeilToInt(timeLeft);
        if (timerLabel)
        {
            timerLabel.text = s.ToString();
            UpdateVisual();
        }
    }

    private void UpdateVisual()
    {
        if (ringTimerImage) ringTimerImage.fillAmount = timeLeft / timeLimitSeconds;
    }

    private async void EndGame()
    {
        var sm = scoreManager != null ? scoreManager : ScoreManager.Instance;
        int score = sm != null ? sm.CurrentScore : 0;

        // Eingaben/Spawns stoppen (Komponenten nicht deaktivieren, damit Spawner-UI/Coroutines laufen)
        if (spawner) spawner.StopSpawning();
        if (playerInput) playerInput.enabled = false;

        // Time-Mode Leaderboard Upload
        try
        {
            bool uploaded = await HighscoreUploader.TrySubmitAsync(score, LeaderboardApi.TimeModeId);
            if (uploaded) Debug.Log($"[LB] Neuer Bestwert {score} zu '{LeaderboardApi.TimeModeId}' hochgeladen.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[LB] Upload fehlgeschlagen: {e.Message}");
        }

        // Spawner zeigt FINISHED-Sequence (Banner + Result)
        if (spawner) spawner.ShowFinishedFromTimeMode(score);
        else Debug.LogWarning("[TimeMode] Kein Spawner zugewiesen – FINISHED-Sequence kann nicht angezeigt werden.");
    }
}
