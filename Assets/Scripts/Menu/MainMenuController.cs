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
        var switcher = MenuPortalSwitcher.Instance;
        if (switcher != null && !switcher.IsSelectedWorldPlayable())
        {
            // Gesperrte Welt: statt eines eigenen Popups direkt in den Shop (Bundle-Tab, siehe
            // ShopController.Open) leiten — dort steht dieselbe Weltbox samt Buy-Button.
            ShopController.Instance?.Open();
            return;
        }
        switcher?.ConsumeFreePlayIfNeeded();

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

    public void OpenShop()
    {
        ShopController.Instance?.Open();
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
