using UnityEngine;
using UnityEngine.SceneManagement;

public class BootstrapLoader : MonoBehaviour
{
    public GameObject sceneFaderPrefab;

    [Header("Flow")]
    [SerializeField] private string firstScene = "IntroScene";
    [SerializeField] private AudioClip bootSfxOrMusic;

    [Header("Perf")]
    [SerializeField] int targetFpsIOS = 60;

    private void Awake()
    {
#if UNITY_IOS
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFpsIOS;
#else
        Application.targetFrameRate = 60;
#endif
        if (SceneFader.Instance == null && sceneFaderPrefab != null)
            Instantiate(sceneFaderPrefab);
    }

    private void Start()
    {
        // Restart-Shortcut?
        if (!string.IsNullOrEmpty(RestartTarget.NextSceneName))
        {
            if (bootSfxOrMusic != null)
                UIAudio.Instance?.PlayOneShot(bootSfxOrMusic);

            if (RestartTarget.NextMode.HasValue && GlobalGameManager.Instance != null)
                GlobalGameManager.Instance.OverrideSelectedMode(RestartTarget.NextMode.Value);

            string target = RestartTarget.NextSceneName;
            RestartTarget.NextSceneName = null;
            RestartTarget.NextMode = null;

            if (SceneFader.Instance != null)
                SceneFader.Instance.LoadSceneDelayed(target, 0.08f);
            else
                SceneManager.LoadScene(target, LoadSceneMode.Single);
            return;
        }

        // Normaler Boot-Flow (Intro/Main Menu)
        if (SceneFader.Instance != null)
            SceneFader.Instance.LoadSceneDelayed(firstScene, 0.05f);
        else
            SceneManager.LoadScene(firstScene, LoadSceneMode.Single);
    }
}
