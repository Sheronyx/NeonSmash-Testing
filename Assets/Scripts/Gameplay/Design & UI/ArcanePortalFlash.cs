using UnityEngine;
using UnityEngine.VFX;
using System.Collections;

public class ArcanePortalFlash : MonoBehaviour
{
    public VisualEffect vfx;

    public float fadeInTime  = 0.15f;
    public float holdTime    = 0.05f;
    public float fadeOutTime = 0.25f;

    [Header("Flash Strength")]
    [SerializeField] private float flashMultiplier = 2.5f;

    private Coroutine flashRoutine;

    private Vector4 baseNormal;
    private Vector4 baseBlue;

    private void Start()
    {
        if (vfx != null)
        {
            ApplySkinColors();

            baseNormal = vfx.GetVector4("Color Particles Normal");
            baseBlue   = vfx.GetVector4("Color Particles Blue");
        }
    }

    // Färbt Particles + Voronoi des Normal-Modus laut aktivem Skin um.
    // Muss VOR dem Cachen von baseNormal laufen, damit der Flash korrekt
    // zur Skin-Farbe zurücklerpt.
    private void ApplySkinColors()
    {
        var theme = SkinManager.Instance?.ActiveTheme;
        if (theme == null || !theme.overridePortalColor) return;

        vfx.SetVector4("Color Particles Normal", theme.portalParticleColor);
        vfx.SetVector4("Color Portal Normal",    theme.portalVoronoiColor);
    }

    private void OnEnable()
    {
        SpecialModeManager.OnModeStarted += HandleModeStart;
        SpecialModeManager.OnModeEnded   += HandleModeEnd;
    }

    private void OnDisable()
    {
        SpecialModeManager.OnModeStarted -= HandleModeStart;
        SpecialModeManager.OnModeEnded   -= HandleModeEnd;
    }

    private void HandleModeStart(SpecialMode mode) => SetMode(mode);
    private void HandleModeEnd(SpecialMode mode)   => SetMode(SpecialMode.None);

    public void FlashParticles()
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(CoFlash());
    }

    IEnumerator CoFlash()
    {
        if (vfx == null) yield break;

        bool isFountain = vfx.GetBool("IsFountainMode");

        string  particlesName   = isFountain ? "Color Particles Blue" : "Color Particles Normal";
        Vector4 normalParticles = isFountain ? baseBlue : baseNormal;
        Vector4 brightParticles = normalParticles * flashMultiplier;

        float t = 0f;
        while (t < fadeInTime)
        {
            vfx.SetVector4(particlesName, Vector4.Lerp(normalParticles, brightParticles, t / fadeInTime));
            t += Time.deltaTime;
            yield return null;
        }
        vfx.SetVector4(particlesName, brightParticles);

        yield return new WaitForSeconds(holdTime);

        t = 0f;
        while (t < fadeOutTime)
        {
            vfx.SetVector4(particlesName, Vector4.Lerp(brightParticles, normalParticles, t / fadeOutTime));
            t += Time.deltaTime;
            yield return null;
        }
        vfx.SetVector4(particlesName, normalParticles);

        flashRoutine = null;
    }

    public void SetMode(SpecialMode mode)
    {
        if (vfx == null) return;
        vfx.SetBool("IsFountainMode", mode == SpecialMode.Fountain);
    }
}
