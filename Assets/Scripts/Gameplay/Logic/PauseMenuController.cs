using UnityEngine;

public class PauseMenuController : MonoBehaviour
{
    public static bool IsPaused = false;

    public GameObject pauseMenuUI;

    [Header("Tutorial Canvases (werden beim Pausieren versteckt)")]
    [SerializeField] private Canvas[] tutorialCanvases;

    public void ShowPauseMenu()
    {
        pauseMenuUI.SetActive(true);

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
        ResumeGame();

        SceneFader.Instance.LoadScene("MainMenuScene");
    }
}