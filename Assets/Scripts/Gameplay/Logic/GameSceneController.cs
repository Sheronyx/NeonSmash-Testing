using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameSceneController : MonoBehaviour
{
    public GameObject tutorialUI;
    public GameObject gameplayUI; // z. B. Score-Anzeige oder Spiel-Canvas
    public GameObject gameLogicRoot; // z. B. PointSpawner, GameManager etc.
    public AudioSource audioSource;
    public AudioClip clickSound;


    void Start()
    {
        if (TutorialFlag.showTutorial && tutorialUI != null)
        {
            tutorialUI.SetActive(true);
            gameplayUI.SetActive(false); // Spiel-UI deaktivieren
            gameLogicRoot.SetActive(false); // Gameplay pausieren
        }
        else
        {
            StartGameplay();
        }

    }

    public void OnTutorialClosed()
    {
        if (tutorialUI != null)
        tutorialUI.SetActive(false); // Tutorial ausblenden
        StartGameplay(); // Gameplay starten
    }

    private void StartGameplay()
    {
        gameplayUI.SetActive(true);
        gameLogicRoot.SetActive(true);
        Debug.Log("Gameplay gestartet.");
    }
}
