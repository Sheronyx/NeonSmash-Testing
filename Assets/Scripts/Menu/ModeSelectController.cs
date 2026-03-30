using UnityEngine;
using UnityEngine.SceneManagement;

public class ModeSelectController : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string mainMenuScene = "MainMenuScene";
    [SerializeField] private string gameSceneInfinityMode = "GameScene_InfinityMode";

    [SerializeField] private string gameSceneTimeMode = "GameScene_TimeMode";

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
