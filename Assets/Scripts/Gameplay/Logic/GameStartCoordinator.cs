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
        if (spawner)     spawner.enabled      = false;
        if (playerInput) playerInput.enabled  = false;
        if (blockTimeScale) Time.timeScale = 0f;

        if (MultiplayerManager.IsMultiplayerGame)
        {
            // Countdown erst starten wenn beide Seiten in der Szene sind
            if (MultiplayerGameSession.IsGameStarted)
                StartCountdown();
            else
                MultiplayerGameSession.OnGameStarted += StartCountdown;
        }
        else
        {
            StartCountdown();
        }
    }

    private void StartCountdown()
    {
        MultiplayerGameSession.OnGameStarted -= StartCountdown;

        if (countdownUI)
        {
            countdownUI.OnCountdownFinished += HandleCountdownFinished;
            countdownUI.StartCountdown();
        }
        else
        {
            HandleCountdownFinished();
        }
    }

    private void HandleCountdownFinished()
    {
        if (blockTimeScale) Time.timeScale = 1f;
        if (playerInput)    playerInput.enabled = true;
        if (countdownUI)    countdownUI.OnCountdownFinished -= HandleCountdownFinished;
        if (ScoreManager.Instance) ScoreManager.Instance.ResetScore();

        var tmc = FindFirstObjectByType<TimeModeController>();
        if (tmc) tmc.BeginAfterCountdown();

        if (spawner) spawner.Begin();
    }

    void OnDestroy()
    {
        MultiplayerGameSession.OnGameStarted -= StartCountdown;
        if (countdownUI) countdownUI.OnCountdownFinished -= HandleCountdownFinished;
        if (blockTimeScale) Time.timeScale = 1f;
    }
}
