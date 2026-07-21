using UnityEngine;
using UnityEngine.SceneManagement;

public class ModeSelectController : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string mainMenuScene = "MainMenuScene";
    [SerializeField] private string gameSceneInfinityMode = "GameScene_InfinityMode";

    public void OnInfinity()
    {
        if (GlobalGameManager.Instance != null)
            GlobalGameManager.Instance.SetMode(GameMode.Infinity);

        // Feen fliegen ins Portal, Kamera zoomt hin — erst danach der eigentliche Szenenwechsel.
        // Ohne PlayIntroSequence in der Szene (z.B. andere Menüs) läuft der Wechsel wie bisher sofort.
        if (PlayIntroSequence.Instance != null)
            PlayIntroSequence.Instance.Play(() => LoadScene(gameSceneInfinityMode));
        else
            LoadScene(gameSceneInfinityMode);
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
