using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject tutorialUI;

    public void selectGame()
    {
        SceneFader.Instance.LoadScene("ModeSelectScene");
    }

    public void StartGame()
    {
        // Prüfe, ob Tutorial schon gezeigt wurde
        if (PlayerPrefs.GetInt("tutorial_shown", 0) == 0)
        {
            TutorialFlag.showTutorial = true;
            PlayerPrefs.SetInt("tutorial_shown", 1);
            PlayerPrefs.Save();
        }
        else
        {
            TutorialFlag.showTutorial = false;
        }

        SceneFader.Instance.LoadScene("GameScene");
    }

    public void OpenTutorial()
    {
    if (tutorialUI != null)
    {
        tutorialUI.SetActive(true); // Zeige Tutorial-Fenster im Menü
    }
    }

    public void OpenLeaderboard()
    {
        // Noch leer
        Debug.Log("Leaderboard wird später implementiert.");
    }

    public void OpenSettings()
    {
        // Noch leer
        Debug.Log("Einstellungen werden später implementiert.");
    }
}
