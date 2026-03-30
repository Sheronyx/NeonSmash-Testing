using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSceneReloader : MonoBehaviour
{
    [Header("Scene Names")]
    public string bootstrapSceneName = "BootstrapScene";
    public string infinitySceneName  = "GameScene_InfinityMode";
    public string timeSceneName      = "GameScene_TimeMode";

    // Buttons im Infinity-Mode verknüpfen
    public void RestartInfinityViaBootstrap()
    {
        RestartTarget.NextSceneName = infinitySceneName;
        RestartTarget.NextMode = GameMode.Infinity;

        // WICHTIG: Beim nächsten Game-Load Musik hart neu starten
        MusicManager.ForceRestartGameMusicNextLoad = true;

        // Übergang über Bootstrap (mit Fade wenn vorhanden)
        if (SceneFader.Instance != null)
            SceneFader.Instance.LoadSceneDelayed(bootstrapSceneName, 0.05f);
        else
            SceneManager.LoadScene(bootstrapSceneName, LoadSceneMode.Single);
    }

    // Buttons im Time-Mode verknüpfen
    public void RestartTimeViaBootstrap()
    {
        RestartTarget.NextSceneName = timeSceneName;
        RestartTarget.NextMode = GameMode.Time;

        // WICHTIG: Beim nächsten Game-Load Musik hart neu starten
        MusicManager.ForceRestartGameMusicNextLoad = true;

        if (SceneFader.Instance != null)
            SceneFader.Instance.LoadSceneDelayed(bootstrapSceneName, 0.05f);
        else
            SceneManager.LoadScene(bootstrapSceneName, LoadSceneMode.Single);
    }

    // Zurück ins Hauptmenü (hier KEIN Musik-Flag nötig)
    public void BackToMenu(string menuSceneName = "MainMenu")
    {
        if (SceneFader.Instance != null)
            SceneFader.Instance.LoadSceneDelayed(menuSceneName, 0.05f);
        else
            SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
    }
}
