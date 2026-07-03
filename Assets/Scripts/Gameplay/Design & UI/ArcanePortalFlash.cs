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

    [Header("Elektrifizierung")]
    [Tooltip("Looping-VFX/Partikel das aktiv ist, während das Portal elektrifiziert.")]
    [SerializeField] private GameObject electricVFX;

    [Header("Intensitäts-Warnung")]
    [Tooltip("+X% Größe pro Intensitätsstufe (persistent).")]
    [SerializeField] private float scaleBoostPerLevel  = 0.12f;
    [Tooltip("+X% Animations-Geschwindigkeit pro Stufe (persistent).")]
    [SerializeField] private float speedBoostPerLevel  = 0.30f;
    [Tooltip("+X% Partikel-Helligkeit pro Stufe (persistent).")]
    [SerializeField] private float glowBoostPerLevel   = 0.35f;
    [Tooltip("Zeit (s) für eine Level-Stufe Übergang (nur wenn kein Pop läuft).")]
    [SerializeField] private float levelTransitionTime = 0.70f;

    [Header("Pop-Effekt beim Intensitätswechsel")]
    [Tooltip("Wie weit die Größe über das Ziel hinausschießt.")]
    [SerializeField] private float popScaleOvershoot  = 0.22f;
    [Tooltip("Wie weit die VFX-Geschwindigkeit über das Ziel hinausschießt.")]
    [SerializeField] private float popSpeedOvershoot  = 0.60f;
    [Tooltip("Sekunden bis zum Peak des Pops.")]
    [SerializeField] private float popRiseDuration    = 0.10f;
    [Tooltip("Sekunden vom Peak zurück zum Ziel.")]
    [SerializeField] private float popSettleDuration  = 0.35f;

    private Coroutine _flashRoutine;
    private Coroutine _comboColorRoutine;
    private Coroutine _popRoutine;
    private bool      _isPopping;

    // Farb-Grundwerte (skin-adjustiert, ohne Glow)
    private Vector4 _particlesNormalOrig;
    private Vector4 _portalNormalOrig;
    private Vector4 baseBlue;

    // Live-Grundwert (inkl. Glow-Faktor), von CoFlash/TransitionColor genutzt
    private Vector4 baseNormal;
    private Vector4 basePortalNormal;
    private Vector4 _currentParticlesBase;

    private Vector3 _origScale;
    private float   _origPlayRate = 1f;

    private bool  _wasComboActive;

    // Intensitäts-Zustand
    private int   _intensityLevel;
    private float _displayScaleMult = 1f;
    private float _displayPlayRate;
    private float _displayGlowMult  = 1f;
    private float _prevGlowMult     = 1f;

    // ──────────────────────────────────────────
    private void Start()
    {
        if (vfx == null) return;

        ApplySkinColors();

        _particlesNormalOrig  = vfx.GetVector4("Color Particles Normal");
        _portalNormalOrig     = vfx.GetVector4("Color Portal Normal");
        baseBlue              = vfx.GetVector4("Color Particles Blue");

        baseNormal            = _particlesNormalOrig;
        basePortalNormal      = _portalNormalOrig;
        _currentParticlesBase = baseNormal;

        _origScale       = vfx.transform.localScale;
        _origPlayRate    = vfx.playRate;
        _displayPlayRate = _origPlayRate;
    }

    private void ApplySkinColors()
    {
        var theme = SkinManager.Instance?.ActiveTheme;
        if (theme == null || !theme.overridePortalColor) return;

        vfx.SetVector4("Color Particles Normal", theme.portalParticleColor);
        vfx.SetVector4("Color Portal Normal",    theme.portalVoronoiColor);
    }

    // ──────────────────────────────────────────
    private void OnEnable()
    {
        SpecialModeManager.OnModeStarted           += HandleModeStart;
        SpecialModeManager.OnModeEnded             += HandleModeEnd;
        ComboManager.OnComboChanged                += HandleComboChanged;
        PortalElectrifier.OnElectrificationChanged += SetElectrifying;
        BackgroundIntensityWarning.OnSpeedupWarning += HandleSpeedupWarning;
        BackgroundIntensityWarning.OnPhaseReset     += ResetIntensity;
    }

    private void OnDisable()
    {
        SpecialModeManager.OnModeStarted           -= HandleModeStart;
        SpecialModeManager.OnModeEnded             -= HandleModeEnd;
        ComboManager.OnComboChanged                -= HandleComboChanged;
        PortalElectrifier.OnElectrificationChanged -= SetElectrifying;
        BackgroundIntensityWarning.OnSpeedupWarning -= HandleSpeedupWarning;
        BackgroundIntensityWarning.OnPhaseReset     -= ResetIntensity;
    }

    // ──────────────────────────────────────────
    // Update: smooth transition aller Intensitäts-Werte
    private void Update()
    {
        if (vfx == null) return;
        ApplyIntensityState();
    }

    private void ApplyIntensityState()
    {
        float dt       = Time.deltaTime;
        float glowSpeed = glowBoostPerLevel / levelTransitionTime * dt;
        float targetGlow = 1f + _intensityLevel * glowBoostPerLevel;
        _displayGlowMult = Mathf.MoveTowards(_displayGlowMult, targetGlow, glowSpeed);

        // Während des Pops steuert Co_Pop die Größe/Speed — Update greift nicht ein
        if (!_isPopping)
        {
            float targetScale    = 1f + _intensityLevel * scaleBoostPerLevel;
            float targetPlayRate = _origPlayRate * (1f + _intensityLevel * speedBoostPerLevel);
            float scaleSpeed     = scaleBoostPerLevel / levelTransitionTime * dt;
            float playRateSpeed  = _origPlayRate * speedBoostPerLevel / levelTransitionTime * dt;
            _displayScaleMult    = Mathf.MoveTowards(_displayScaleMult, targetScale,    scaleSpeed);
            _displayPlayRate     = Mathf.MoveTowards(_displayPlayRate,  targetPlayRate,  playRateSpeed);
        }

        vfx.transform.localScale = _origScale * _displayScaleMult;
        vfx.playRate             = _displayPlayRate;

        if (!Mathf.Approximately(_displayGlowMult, _prevGlowMult))
        {
            _prevGlowMult         = _displayGlowMult;
            baseNormal            = _particlesNormalOrig * _displayGlowMult;
            basePortalNormal      = _portalNormalOrig    * _displayGlowMult;
            _currentParticlesBase = baseNormal;
            vfx.SetVector4("Color Particles Normal", baseNormal);
            vfx.SetVector4("Color Portal Normal",    basePortalNormal);
        }
    }

    // ──────────────────────────────────────────
    private void HandleSpeedupWarning()
    {
        _intensityLevel++;
        FlashParticles();
        if (_popRoutine != null) StopCoroutine(_popRoutine);
        _popRoutine = StartCoroutine(Co_Pop());
    }

    private IEnumerator Co_Pop()
    {
        _isPopping = true;

        float fromScale    = _displayScaleMult;
        float fromPlayRate = _displayPlayRate;
        float targetScale    = 1f + _intensityLevel * scaleBoostPerLevel;
        float targetPlayRate = _origPlayRate * (1f + _intensityLevel * speedBoostPerLevel);
        float peakScale    = targetScale    + popScaleOvershoot;
        float peakPlayRate = targetPlayRate + popSpeedOvershoot;

        // Schnell zum Peak
        float t = 0f;
        while (t < popRiseDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / popRiseDuration));
            _displayScaleMult = Mathf.Lerp(fromScale,    peakScale,    k);
            _displayPlayRate  = Mathf.Lerp(fromPlayRate, peakPlayRate, k);
            yield return null;
        }
        _displayScaleMult = peakScale;
        _displayPlayRate  = peakPlayRate;

        // Smooth zurück zum Ziel
        t = 0f;
        while (t < popSettleDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / popSettleDuration));
            _displayScaleMult = Mathf.Lerp(peakScale,    targetScale,    k);
            _displayPlayRate  = Mathf.Lerp(peakPlayRate, targetPlayRate, k);
            yield return null;
        }
        _displayScaleMult = targetScale;
        _displayPlayRate  = targetPlayRate;

        _isPopping  = false;
        _popRoutine = null;
    }

    public void ResetIntensity()
    {
        _intensityLevel = 0;
        if (_popRoutine != null) { StopCoroutine(_popRoutine); _popRoutine = null; }
        _isPopping = false;
        // _displayScaleMult / _displayPlayRate / _displayGlowMult laufen
        // per MoveTowards smooth zu 1 / _origPlayRate / 1 zurück
    }

    // ──────────────────────────────────────────
    private void SetElectrifying(bool active)
    {
        if (electricVFX != null) electricVFX.SetActive(active);
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

    // ──────────────────────────────────────────
    public void FlashParticles()
    {
        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(CoFlash());
    }

    private IEnumerator CoFlash()
    {
        if (vfx == null) yield break;

        bool    isFountain      = vfx.GetBool("IsFountainMode");
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

        _flashRoutine = null;
    }

    // ──────────────────────────────────────────
    public void SetMode(SpecialMode mode)
    {
        if (vfx == null) return;
        vfx.SetBool("IsFountainMode", mode == SpecialMode.Fountain);
    }
}
