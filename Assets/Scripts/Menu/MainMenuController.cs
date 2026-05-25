using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject tutorialUI;
    [SerializeField] private GameObject matchmakingScreen;
    [SerializeField] private GameObject friendsScreen;

    public void OnInfinity()
    {
        if (GlobalGameManager.Instance != null)
            GlobalGameManager.Instance.SetMode(GameMode.Infinity);

        SceneFader.Instance.LoadScene("GameScene_InfinityMode");
    }

    public void OnMultiplayer()
    {
        if (matchmakingScreen != null)
            matchmakingScreen.SetActive(true);
    }

    public void OnFriends()
    {
        if (friendsScreen != null)
            friendsScreen.SetActive(true);
    }

    public void OpenTutorial()
    {
        if (tutorialUI != null)
            tutorialUI.SetActive(true);
    }

    public void OpenLeaderboard()
    {
        Debug.Log("Leaderboard wird später implementiert.");
    }

    public void OpenSettings()
    {
        Debug.Log("Einstellungen werden später implementiert.");
    }

    public void OpenTasks()
    {
        TasksPopupController.Instance?.Open();
    }
}
