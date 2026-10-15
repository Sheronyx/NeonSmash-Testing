using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System.Collections;

public class BootstrapLoader : MonoBehaviour
{
    public GameObject sceneFaderPrefab;

    [Header("Flow")]
    [SerializeField] private string firstScene = "IntroScene";
    [SerializeField] private AudioClip bootSfxOrMusic;
    [SerializeField] private float logoDuration = 1.8f;

    [Header("Perf")]
    [SerializeField] int targetFpsIOS = 60;

    [SerializeField] private VFXWarmup vfxWarmup;

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
        // SceneFader bleibt schwarz bis Warmup fertig — versteckt die VFX-Effekte
    }

    private IEnumerator Start()
    {
        // Restart-Shortcut (z.B. nach Game Over zurück ins Spiel)
        if (!string.IsNullOrEmpty(RestartTarget.NextSceneName))
        {
            if (bootSfxOrMusic != null)
                UIAudio.Instance?.PlayOneShot(bootSfxOrMusic);

            if (RestartTarget.NextMode.HasValue && GlobalGameManager.Instance != null)
                GlobalGameManager.Instance.OverrideSelectedMode(RestartTarget.NextMode.Value);

            string target = RestartTarget.NextSceneName;
            RestartTarget.NextSceneName = null;
            RestartTarget.NextMode = null;

            SceneManager.LoadScene(target, LoadSceneMode.Single);
            yield break;
        }

        // Auth starten (läuft im Hintergrund während Warmup + Logo)
        UgsBootstrap.Begin();
        NeonAnalytics.LogSessionStart();

        // VFX Warmup abwarten — SceneFader ist noch schwarz und versteckt die Effekte
        if (vfxWarmup != null)
        {
            // Mit Zeitlimit: haengt der Warmup (z.B. weil Camera.main in einer neu geladenen
            // Szene noch fehlt), blieb die App frueher stumm in der BootstrapScene stehen.
            float wartezeit = 0f;
            while (!vfxWarmup.IsComplete && wartezeit < 5f)
            {
                wartezeit += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!vfxWarmup.IsComplete)
                Debug.LogWarning("[Bootstrap] VFX-Warmup nach 5 s nicht fertig — es geht trotzdem weiter.");
        }
        Debug.Log("[Bootstrap] Warmup durch, Logo wird gezeigt.");

        // Logo einblenden
        SceneFader.Instance?.Clear();

        // Logo-Dauer abwarten
        yield return new WaitForSecondsRealtime(logoDuration);

        // Logo zu Schwarz faden
        if (SceneFader.Instance != null)
            yield return SceneFader.Instance.FadeToBlack();

        // NetworkManager sauber herunterfahren bevor BootstrapScene entladen wird
        NetworkManager.Singleton?.Shutdown();

        Debug.Log("[Bootstrap] Lade " + firstScene + ".");

        // IntroScene laden — bleibt schwarz, IntroScene ruft Clear() auf und zeigt Ladebildschirm
        if (SceneFader.Instance != null)
            SceneFader.Instance.LoadSceneKeepBlack(firstScene);
        else
            SceneManager.LoadScene(firstScene, LoadSceneMode.Single);
    }
}
