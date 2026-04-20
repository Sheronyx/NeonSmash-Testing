using UnityEngine;
using System.Collections;
using UnityEngine.Rendering.Universal;

public class FullScreenController : MonoBehaviour
{
    [Header("Renderer Features")]
    [SerializeField] private ScriptableRendererFeature gravityFeature;
    [SerializeField] private ScriptableRendererFeature goldFeature;
    [SerializeField] private ScriptableRendererFeature chaosFeature;
    [SerializeField] private ScriptableRendererFeature fountainFeature;

    private ScriptableRendererFeature currentFeature;
    private Material currentMaterial;

    [Header("Materials")]
    [SerializeField] private Material gravityMaterial;
    [SerializeField] private Material goldMaterial;
    [SerializeField] private Material chaosMaterial;
    [SerializeField] private Material fountainMaterial;

    [Header("Timing")]
    [SerializeField] private float fadeInTime = 0.3f;
    [SerializeField] private float fadeOutTime = 0.5f;

    [Header("Intensity Cap (1 = volle Shader-Stärke)")]
    [SerializeField] [Range(0f, 1f)] private float maxIntensity = 1f;


    private int FadeID = Shader.PropertyToID("_Fade");

    private Coroutine activeRoutine;

    private void Start()
    {
        SetFade(gravityMaterial,  0f);
        SetFade(goldMaterial,     0f);
        SetFade(chaosMaterial,    0f);
        SetFade(fountainMaterial, 0f);
        StartCoroutine(WarmUpPipeline());
    }

    // Aktiviert alle Features kurz beim Start damit URP die Pipeline einmal baut.
    // Danach sind spätere SetActive-Aufrufe flicker-frei weil die Shader bereits kompiliert sind.
    private IEnumerator WarmUpPipeline()
    {
        EnableAllFeatures();
        for (int i = 0; i < 5; i++)
            yield return null;
        DisableAllFeatures();
    }

    private void OnEnable()
    {
        SpecialModeManager.OnModeStarted += HandleModeStarted;
        SpecialModeManager.OnModeEnded   += HandleModeEnded;
    }

    private void OnDisable()
    {
        SpecialModeManager.OnModeStarted -= HandleModeStarted;
        SpecialModeManager.OnModeEnded   -= HandleModeEnded;
        Cleanup();
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    private void Cleanup()
    {
        SetFade(gravityMaterial,  0f);
        SetFade(goldMaterial,     0f);
        SetFade(chaosMaterial,    0f);
        SetFade(fountainMaterial, 0f);
        DisableAllFeatures();

        currentFeature  = null;
        currentMaterial = null;
    }

    private void HandleModeStarted(SpecialMode mode)
    {
        // Alle anderen Materials auf 0 (kein SetActive-Toggle)
        SetFade(gravityMaterial,  0f);
        SetFade(goldMaterial,     0f);
        SetFade(chaosMaterial,    0f);
        SetFade(fountainMaterial, 0f);

        switch (mode)
        {
            case SpecialMode.Gravity:
                currentFeature  = gravityFeature;
                currentMaterial = gravityMaterial;
                break;
            case SpecialMode.Gold:
                currentFeature  = goldFeature;
                currentMaterial = goldMaterial;
                break;
            case SpecialMode.Chaos:
                currentFeature  = chaosFeature;
                currentMaterial = chaosMaterial;
                break;
            case SpecialMode.Fountain:
                currentFeature  = fountainFeature;
                currentMaterial = fountainMaterial;
                break;
        }

        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(Co_Start());
    }

    private void HandleModeEnded(SpecialMode mode)
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(Co_End());
    }

    private IEnumerator Co_Start()
    {
        if (DevicePerformance.IsLowEnd)
            Application.targetFrameRate = 30;
        currentFeature?.SetActive(true);
        yield return null;
        yield return null;
        yield return Fade(0f, 1f, fadeInTime);
    }

    private IEnumerator Co_End()
    {
        yield return Fade(1f, 0f, fadeOutTime);
        yield return null;
        currentFeature?.SetActive(false);
        currentFeature  = null;
        currentMaterial = null;
        if (DevicePerformance.IsLowEnd)
            Application.targetFrameRate = 60;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t     = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(from, to, t);
            ApplyFade(eased);
            yield return null;
        }

        ApplyFade(to);
    }

    private void ApplyFade(float value)
    {
        if (currentMaterial == null) return;
        currentMaterial.SetFloat(FadeID, value * maxIntensity);
    }

    private void SetFade(Material mat, float value)
    {
        if (mat == null) return;
        mat.SetFloat(FadeID, value);
    }

    private void EnableAllFeatures()
    {
        gravityFeature?.SetActive(true);
        goldFeature?.SetActive(true);
        chaosFeature?.SetActive(true);
        fountainFeature?.SetActive(true);
    }

    private void DisableAllFeatures()
    {
        gravityFeature?.SetActive(false);
        goldFeature?.SetActive(false);
        chaosFeature?.SetActive(false);
        fountainFeature?.SetActive(false);
    }
}
