using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ModeSelectController : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string mainMenuScene = "MainMenuScene";
    [SerializeField] private string gameSceneInfinityMode = "GameScene_InfinityMode";
    [SerializeField] private string gameSceneTimeMode = "GameScene_TimeMode";

    [Header("Time Mode Lock")]
    [SerializeField] private Button timeModeButton;
    [SerializeField] private GameObject timeLockIcon;

    private void Start()
    {
        bool unlocked = PlayerPrefs.GetInt("TimeModeUnlocked", 0) == 1;
        if (timeLockIcon != null)  timeLockIcon.SetActive(!unlocked);
        if (timeModeButton != null) timeModeButton.interactable = unlocked;
    }

    // --- Buttons (per OnClick verdrahten) ---
    public void OnInfinity()
    {
        if (GlobalGameManager.Instance != null)
            GlobalGameManager.Instance.SetMode(GameMode.Infinity);

        LoadScene(gameSceneInfinityMode);
    }

    public void OnTime()
    {
        if (GlobalGameManager.Instance != null)
            GlobalGameManager.Instance.SetMode(GameMode.Time);

        LoadScene(gameSceneTimeMode);
    }

    public void OnBack()
    {
        LoadScene(mainMenuScene);
    }

    private void LoadScene(string sceneName)
    {
        SceneFader.Instance.LoadScene(sceneName);
    }
}
