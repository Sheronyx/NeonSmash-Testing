using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameStartCoordinator : MonoBehaviour
{
    [Header("References (optional, auto-find if empty)")]
    [SerializeField] private CountdownUI countdownUI;      // Default Countdown-Overlay
    [SerializeField] private MixedPointSpawner spawner;   // <- starker Typ!
    [SerializeField] private MonoBehaviour playerInput;     // z.B. PlayerInputHandler

    [Header("Skin-Varianten (Countdown pro Bundle)")]
    [SerializeField] private CountdownSkin[] skinCountdowns;

    [System.Serializable]
    public class CountdownSkin
    {
        public SkinTheme  theme;
        public CountdownUI countdownUI;
    }

    [Header("Options")]
    [SerializeField] private bool blockTimeScale = false;   // wenn true, Time.timeScale=0 während Countdown

    [Header("Top Bar (Infinity Mode: erst nach der Boost-Wahl einblenden)")]
    [SerializeField] private CanvasGroup topBarCanvasGroup;
    [SerializeField] private float topBarFadeInDuration = 0.35f;

    void Awake()
    {
if (!countdownUI)
    countdownUI = FindFirstObjectByType<CountdownUI>(FindObjectsInactive.Include);

        // Aktive Skin-Variante wählen (überschreibt das Default-Countdown).
        // Die nicht gewählten CountdownUI deaktivieren sich selbst in ihrem
        // Awake und werden nie gestartet.
        var active = SkinManager.Instance != null ? SkinManager.Instance.ActiveTheme : null;
        if (active != null && skinCountdowns != null)
        {
            foreach (var v in skinCountdowns)
                if (v != null && v.theme == active && v.countdownUI != null)
                    countdownUI = v.countdownUI;
        }

if (!spawner)
    spawner = FindFirstObjectByType<MixedPointSpawner>(FindObjectsInactive.Include);

        if (spawner)      spawner.StopSpawning();
        if (playerInput)  playerInput.enabled = false;
        if (blockTimeScale) Time.timeScale = 0f;

        // Top Bar bleibt unsichtbar, bis der Boost gewählt wurde (siehe BeginInfinityRun) — nur
        // im Infinity Mode, Multiplayer zeigt sie unverändert direkt von Anfang an.
        bool isInfinityMode = GlobalGameManager.Instance != null && GlobalGameManager.Instance.SelectedMode == GameMode.Infinity;
        if (isInfinityMode && topBarCanvasGroup != null)
        {
            topBarCanvasGroup.alpha          = 0f;
            topBarCanvasGroup.interactable   = false;
            topBarCanvasGroup.blocksRaycasts = false;
        }
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

        // Statt Zahlen-Countdown: die Feen fliegen vom Portal-Anker zu ihrer Ruheposition, danach
        // startet das Spiel genau wie sonst nach Ablauf des Countdowns (siehe HandleCountdownFinished).
        if (FairyArrivalSequence.Instance != null)
        {
            FairyArrivalSequence.Instance.Play(HandleCountdownFinished);
        }
        else if (countdownUI)
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

        NeonAnalytics.LogGameStart(
            GlobalGameManager.Instance ? GlobalGameManager.Instance.SelectedMode : GameMode.Infinity);

        bool isInfinity = GlobalGameManager.Instance != null && GlobalGameManager.Instance.SelectedMode == GameMode.Infinity;

        // Infinity Mode: erst Boost wählen lassen, danach beginnt der Run (kein Spawn davor). Nur
        // Infinity — Multiplayer läuft unverändert direkt weiter.
        if (isInfinity && BoostSelectionUI.Instance != null)
            BoostSelectionUI.Instance.Show(_ => BeginInfinityRun());
        else if (isInfinity && PhaseManager.Instance != null)
            BeginInfinityRun();
        else if (spawner)
            spawner.Begin();
    }

    private void BeginInfinityRun()
    {
        if (topBarCanvasGroup != null) StartCoroutine(Co_FadeInTopBar());

        if (PhaseManager.Instance != null) PhaseManager.Instance.BeginRun();
        else if (spawner) spawner.Begin();
    }

    private IEnumerator Co_FadeInTopBar()
    {
        topBarCanvasGroup.interactable   = true;
        topBarCanvasGroup.blocksRaycasts = true;

        float t = 0f;
        while (t < topBarFadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            topBarCanvasGroup.alpha = Mathf.Clamp01(t / topBarFadeInDuration);
            yield return null;
        }
        topBarCanvasGroup.alpha = 1f;
    }

    void OnDestroy()
    {
        MultiplayerGameSession.OnGameStarted -= StartCountdown;
        if (countdownUI) countdownUI.OnCountdownFinished -= HandleCountdownFinished;
        if (blockTimeScale) Time.timeScale = 1f;
    }
}
