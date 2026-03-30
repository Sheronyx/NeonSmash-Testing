using UnityEngine;

public class PauseMenuController : MonoBehaviour
{
    public static bool IsPaused = false;

    public GameObject pauseMenuUI;

    public void ShowPauseMenu()
    {
        pauseMenuUI.SetActive(true);

        Time.timeScale = 0f;
        AudioListener.pause = true;

        IsPaused = true;

        if (PlayerInputHandler.Instance != null)
            PlayerInputHandler.Instance.ResetTouch();
    }

    public void ResumeGame()
    {
        pauseMenuUI.SetActive(false);

        Time.timeScale = 1f;
        AudioListener.pause = false;

        IsPaused = false;
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