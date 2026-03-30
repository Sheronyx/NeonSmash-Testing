using UnityEngine;
using UnityEngine.SceneManagement;

public class GameStartCoordinator : MonoBehaviour
{
    [Header("References (optional, auto-find if empty)")]
    [SerializeField] private CountdownUI countdownUI;      // dein Countdown-Overlay
    [SerializeField] private MixedPointSpawner spawner;   // <- starker Typ!
    [SerializeField] private MonoBehaviour playerInput;     // z.B. PlayerInputHandler

    [Header("Options")]
    [SerializeField] private bool blockTimeScale = false;   // wenn true, Time.timeScale=0 während Countdown

    void Awake()
    {
if (!countdownUI) 
    countdownUI = FindFirstObjectByType<CountdownUI>(FindObjectsInactive.Include);

if (!spawner)     
    spawner = FindFirstObjectByType<MixedPointSpawner>(FindObjectsInactive.Include);

        if (spawner)      spawner.StopSpawning();
        if (playerInput)  playerInput.enabled = false;
        if (blockTimeScale) Time.timeScale = 0f;
    }

    void Start()
    {
        // Gameplay vorerst blocken
        if (spawner) spawner.enabled = false;
        if (playerInput)  playerInput.enabled  = false;

        if (blockTimeScale) Time.timeScale = 0f;

        if (countdownUI)
        {
            // abonnieren und starten
            countdownUI.OnCountdownFinished += HandleCountdownFinished;
            countdownUI.StartCountdown();
        }
        else
        {
            // Fallback: kein Countdown gefunden -> direkt starten
            HandleCountdownFinished();
        }
    }

    private void HandleCountdownFinished()
    {
        if (blockTimeScale) Time.timeScale = 1f;

        if (playerInput) playerInput.enabled = true;
        if (spawner) spawner.Begin();
        if (countdownUI) countdownUI.OnCountdownFinished -= HandleCountdownFinished;

        // Score für neuen Run zurücksetzen
        if (ScoreManager.Instance) ScoreManager.Instance.ResetScore();

        // Time-Mode-Timer starten (nur in Game_Time vorhanden)
        var tmc = FindFirstObjectByType<TimeModeController>();
        if (tmc) tmc.BeginAfterCountdown();
    }

    void OnDestroy()
    {
        if (countdownUI) countdownUI.OnCountdownFinished -= HandleCountdownFinished;
        if (blockTimeScale) Time.timeScale = 1f;
    }
}
