using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSceneReloader : MonoBehaviour
{
    [Header("Scene Names")]
    public string bootstrapSceneName = "BootstrapScene";
    public string infinitySceneName  = "GameScene_InfinityMode";

    public void RestartInfinityViaBootstrap()
    {
        RestartTarget.NextSceneName = infinitySceneName;
        RestartTarget.NextMode = GameMode.Infinity;

        MusicManager.ForceRestartGameMusicNextLoad = true;

        if (SceneFader.Instance != null)
            SceneFader.Instance.LoadSceneDelayed(bootstrapSceneName, 0.05f);
        else
            SceneManager.LoadScene(bootstrapSceneName, LoadSceneMode.Single);
    }

    public void BackToMenu(string menuSceneName = "MainMenu")
    {
        if (SceneFader.Instance != null)
            SceneFader.Instance.LoadSceneDelayed(menuSceneName, 0.05f);
        else
            SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
    }
}
