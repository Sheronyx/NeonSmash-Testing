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

    private int FadeID = Shader.PropertyToID("_Fade");

    private Coroutine activeRoutine;

    private void Start()
    {
        DisableAllFeatures();

        SetFade(gravityMaterial, 0f);
        SetFade(goldMaterial, 0f);
        SetFade(chaosMaterial, 0f);
        SetFade(fountainMaterial, 0f);
    }

    private void OnEnable()
    {
        SpecialModeManager.OnModeStarted += HandleModeStarted;
        SpecialModeManager.OnModeEnded += HandleModeEnded;
    }

private void OnDestroy()
{
    Cleanup();
}

private void OnDisable()
{
    SpecialModeManager.OnModeStarted -= HandleModeStarted;
    SpecialModeManager.OnModeEnded -= HandleModeEnded;

    Cleanup();
}

private void Cleanup()
{
    DisableAllFeatures();

    SetFade(gravityMaterial, 0f);
    SetFade(goldMaterial, 0f);
    SetFade(chaosMaterial, 0f);

    currentFeature = null;
    currentMaterial = null;
}

    private void HandleModeStarted(SpecialMode mode)
{
    DisableAllFeatures();

    switch (mode)
    {
        case SpecialMode.Gravity:
            currentFeature = gravityFeature;
            currentMaterial = gravityMaterial;
            break;

        case SpecialMode.Gold:
            currentFeature = goldFeature;
            currentMaterial = goldMaterial;
            break;

        case SpecialMode.Chaos:
            currentFeature = chaosFeature;
            currentMaterial = chaosMaterial;
            break;

        case SpecialMode.Fountain: // 👈 DAS IST DEIN FIX
            currentFeature = fountainFeature;
            currentMaterial = fountainMaterial;
            break;
    }

    if (currentFeature != null)
        currentFeature.SetActive(true);

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

        if (currentFeature != null)
            currentFeature.SetActive(false);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float eased = Mathf.SmoothStep(from, to, t);

            ApplyFade(eased);

            yield return null;
        }

        ApplyFade(to);
    }

    private void ApplyFade(float value)
    {
        if (currentMaterial == null) return;

        currentMaterial.SetFloat(FadeID, value);
    }

    private void SetFade(Material mat, float value)
    {
        if (mat == null) return;
        mat.SetFloat(FadeID, value);
    }

    private void DisableAllFeatures()
    {
        gravityFeature?.SetActive(false);
        goldFeature?.SetActive(false);
        chaosFeature?.SetActive(false);
        fountainFeature?.SetActive(false);
    }
}