using UnityEngine;

public class PauseMenuController : MonoBehaviour
{
    public static bool IsPaused = false;

    public GameObject pauseMenuUI;

    [Header("Tutorial Canvases (werden beim Pausieren versteckt)")]
    [SerializeField] private Canvas[] tutorialCanvases;

    // Setzt das aktive Pause-Panel (Skin-Variante). Buttons der jeweiligen
    // Variante werden im Inspector auf diesen Controller verdrahtet. Das Panel
    // bleibt versteckt, bis pausiert wird.
    public void SetActivePanel(GameObject panel)
    {
        if (panel == null) return;
        pauseMenuUI = panel;
        pauseMenuUI.SetActive(false);
    }

    public void ShowPauseMenu()
    {
        if (MultiplayerManager.IsMultiplayerGame) return;
        pauseMenuUI.SetActive(true);

        // Falls gerade ein Leben verloren wurde, läuft LivesManager u.U. noch eine Ramp-Up-
        // Coroutine (Time.timeScale langsam zurück auf 1), die sonst diese Pause sofort wieder
        // aufheben würde (siehe LivesManager.CancelRampUp-Kommentar).
        LivesManager.Instance?.CancelRampUp();

        Time.timeScale = 0f;
        AudioListener.pause = true;

        IsPaused = true;

        if (PlayerInputHandler.Instance != null)
            PlayerInputHandler.Instance.ResetTouch();

        SetTutorialCanvasesVisible(false);
    }

    public void ResumeGame()
    {
        pauseMenuUI.SetActive(false);

        Time.timeScale = 1f;
        AudioListener.pause = false;

        IsPaused = false;

        SetTutorialCanvasesVisible(true);
    }

    private void SetTutorialCanvasesVisible(bool visible)
    {
        if (tutorialCanvases == null) return;
        foreach (var c in tutorialCanvases)
            if (c != null) c.enabled = visible;
    }

    public void OpenSettings()
    {
        Debug.Log("Einstellungen geöffnet – Funktion folgt.");
    }

    public void ReturnToMainMenu()
    {
        int score = ScoreManager.Instance ? ScoreManager.Instance.CurrentScore : 0;
        GameMode mode = GlobalGameManager.Instance ? GlobalGameManager.Instance.SelectedMode : GameMode.Infinity;
        NeonAnalytics.LogPauseQuit(mode, score);

        ResumeGame();

        SceneFader.Instance.LoadScene("MainMenuScene");
    }
}