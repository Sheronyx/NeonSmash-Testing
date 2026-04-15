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
        // Features IMMER aktiv lassen – SetActive(false/true) löst URP-Pipeline-Rebuild aus = Flackern.
        // "Unsichtbar" = _Fade auf 0, nicht Feature deaktivieren.
        EnableAllFeatures();

        SetFade(gravityMaterial,  0f);
        SetFade(goldMaterial,     0f);
        SetFade(chaosMaterial,    0f);
        SetFade(fountainMaterial, 0f);
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
        // Kein SetActive(false) – nur Fade auf 0 zurücksetzen
        SetFade(gravityMaterial,  0f);
        SetFade(goldMaterial,     0f);
        SetFade(chaosMaterial,    0f);
        SetFade(fountainMaterial, 0f);

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

        activeRoutine = StartCoroutine(Fade(0f, 1f, fadeInTime));
    }

    private void HandleModeEnded(SpecialMode mode)
    {
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        activeRoutine = StartCoroutine(Co_End());
    }

    private IEnumerator Co_End()
    {
        yield return Fade(1f, 0f, fadeOutTime);
        // Kein SetActive(false) – Material bleibt bei _Fade=0 (unsichtbar)
        currentFeature  = null;
        currentMaterial = null;
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
}
