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

    [Header("Combo-Übergang")]
    [SerializeField] private float comboTransitionTime = 0.25f;

    private Coroutine flashRoutine;
    private Coroutine _comboColorRoutine;

    private Vector4 baseNormal;
    private Vector4 baseBlue;
    private Vector4 basePortalNormal;

    // Aktueller Soll-Grundwert für Particles (Base oder aktive Combo-Farbe).
    // CoFlash liest immer hieraus — nie live vom VFX — damit Unterbrechungen nicht aufkumulieren.
    private Vector4 _currentParticlesBase;

    private bool _wasComboActive;

    private void Start()
    {
        if (vfx != null)
        {
            ApplySkinColors();

            baseNormal           = vfx.GetVector4("Color Particles Normal");
            baseBlue             = vfx.GetVector4("Color Particles Blue");
            basePortalNormal     = vfx.GetVector4("Color Portal Normal");
            _currentParticlesBase = baseNormal;
        }
    }

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
        ComboManager.OnComboChanged      += HandleComboChanged;
    }

    private void OnDisable()
    {
        SpecialModeManager.OnModeStarted -= HandleModeStart;
        SpecialModeManager.OnModeEnded   -= HandleModeEnd;
        ComboManager.OnComboChanged      -= HandleComboChanged;
    }

    private void HandleModeStart(SpecialMode mode) => SetMode(mode);
    private void HandleModeEnd(SpecialMode mode)   => SetMode(SpecialMode.None);

    private void HandleComboChanged(int streak)
    {
        bool isNowActive = streak >= 5;

        if (isNowActive && !_wasComboActive)
        {
            var cm = ComboManager.Instance;
            if (cm != null && cm.CurrentColor.HasValue)
                TransitionToComboColor(cm.CurrentColor.Value);
        }
        else if (!isNowActive && _wasComboActive)
        {
            TransitionToBaseColor();
        }

        _wasComboActive = isNowActive;
    }

    private void TransitionToComboColor(PointColor color)
    {
        if (vfx == null) return;

        string particlesKey = color switch
        {
            PointColor.Red    => "Color Particles Combo Red",
            PointColor.Green  => "Color Particles Combo Green",
            PointColor.Purple => "Color Particles Combo Purple",
            _                 => "Color Particles Normal"
        };
        string portalKey = color switch
        {
            PointColor.Red    => "Color Portal Combo Red",
            PointColor.Green  => "Color Portal Combo Green",
            PointColor.Purple => "Color Portal Combo Purple",
            _                 => "Color Portal Normal"
        };

        Color targetParticles = vfx.HasVector4(particlesKey) ? (Color)vfx.GetVector4(particlesKey) : (Color)baseNormal;
        Color targetPortal    = vfx.HasVector4(portalKey)    ? (Color)vfx.GetVector4(portalKey)    : (Color)basePortalNormal;

        if (_comboColorRoutine != null) StopCoroutine(_comboColorRoutine);
        _comboColorRoutine = StartCoroutine(Co_TransitionColor(targetParticles, targetPortal));
    }

    private void TransitionToBaseColor()
    {
        if (_comboColorRoutine != null) StopCoroutine(_comboColorRoutine);
        _comboColorRoutine = StartCoroutine(Co_TransitionColor((Color)baseNormal, (Color)basePortalNormal));
    }

    private IEnumerator Co_TransitionColor(Color targetParticles, Color targetPortal)
    {
        if (vfx == null) yield break;

        Vector4 startParticles = vfx.GetVector4("Color Particles Normal");
        Vector4 startPortal    = vfx.GetVector4("Color Portal Normal");

        float t = 0f;
        while (t < comboTransitionTime)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / comboTransitionTime));
            vfx.SetVector4("Color Particles Normal", Vector4.Lerp(startParticles, targetParticles, k));
            vfx.SetVector4("Color Portal Normal",    Vector4.Lerp(startPortal,    targetPortal,    k));
            yield return null;
        }

        vfx.SetVector4("Color Particles Normal", targetParticles);
        vfx.SetVector4("Color Portal Normal",    targetPortal);
        _currentParticlesBase = targetParticles;
        _comboColorRoutine = null;
    }

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
        Vector4 normalParticles = isFountain ? baseBlue : _currentParticlesBase;
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
