using TMPro;
using UnityEngine;
using System.Collections;

public class ComboDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI multiplierText;

    [Header("Punch Animation")]
    [SerializeField] private float punchScale    = 1.25f;
    [SerializeField] private float punchDuration = 0.12f;

    [Header("Max Multiplier Pulse")]
    [Tooltip("Dauerhaftes Pulsieren, wenn der Multiplier am Limit ist (x10.0 normal / x20.0 Special).")]
    [SerializeField] private float maxPulseScale = 1.18f;
    [SerializeField] private float maxPulseSpeed = 4f;

    [Header("Max Multiplier Fire")]
    [Tooltip("Flamme (CFXR Fire), die am Limit spielt und beim Verlust der Max-Combo wieder erlischt.")]
    [SerializeField] private ParticleSystem maxFire;

    private Coroutine _punch;
    private Coroutine _pulse;
    private bool      _atMax;

    private void OnEnable()
    {
        ComboManager.OnComboChanged += HandleComboChanged;

        if (maxFire != null)
        {
            // CFXR zerstört/deaktiviert das GameObject von selbst, sobald keine Partikel mehr
            // "leben". Da wir die Flamme bewusst stoppen, würde sie sonst aus der Szene fliegen.
            var cfxr = maxFire.GetComponent<CartoonFX.CFXR_Effect>();
            if (cfxr != null) cfxr.clearBehavior = CartoonFX.CFXR_Effect.ClearBehavior.None;

            // Flamme startet aus (auch wenn am Prefab "Play On Awake" aktiv ist)
            maxFire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        Refresh(ComboManager.Instance != null ? ComboManager.Instance.ComboCount : 0);
    }

    private void OnDisable()
    {
        ComboManager.OnComboChanged -= HandleComboChanged;

        if (_pulse != null) { StopCoroutine(_pulse); _pulse = null; }
        if (_punch != null) { StopCoroutine(_punch); _punch = null; }
        _atMax = false;
        if (multiplierText != null) multiplierText.rectTransform.localScale = Vector3.one;
        if (maxFire != null) maxFire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void HandleComboChanged(int streak)
    {
        Refresh(streak);
        if (streak > 0 && !_atMax) Punch();
    }

    private void Refresh(int streak)
    {
        if (multiplierText == null) return;
        bool comboActive = streak >= 5;
        multiplierText.text = comboActive ? "x5.0" : "x1.0";
        SetMaxPulse(comboActive);
    }

    private void SetMaxPulse(bool on)
    {
        if (on == _atMax) return;
        _atMax = on;

        if (on)
        {
            if (_punch != null) { StopCoroutine(_punch); _punch = null; }
            if (_pulse == null) _pulse = StartCoroutine(Co_Pulse());
            if (maxFire != null) maxFire.Play(true);
        }
        else
        {
            if (_pulse != null) { StopCoroutine(_pulse); _pulse = null; }
            if (multiplierText != null) multiplierText.rectTransform.localScale = Vector3.one;
            // StopEmitting → bestehende Flammen brennen aus statt hart abzuschneiden
            if (maxFire != null) maxFire.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private IEnumerator Co_Pulse()
    {
        if (multiplierText == null) yield break;
        var rt = multiplierText.rectTransform;
        float t = 0f;
        while (true)
        {
            t += Time.deltaTime * maxPulseSpeed;
            float k = (Mathf.Sin(t) + 1f) * 0.5f;          // 0..1
            rt.localScale = Vector3.one * Mathf.Lerp(1f, maxPulseScale, k);
            yield return null;
        }
    }

    private void Punch()
    {
        if (_punch != null) StopCoroutine(_punch);
        _punch = StartCoroutine(Co_Punch());
    }

    private IEnumerator Co_Punch()
    {
        if (multiplierText == null) yield break;
        var rt   = multiplierText.rectTransform;
        float half = punchDuration * 0.5f;
        float t  = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            rt.localScale = Vector3.Lerp(Vector3.one, Vector3.one * punchScale, t / half);
            yield return null;
        }
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            rt.localScale = Vector3.Lerp(Vector3.one * punchScale, Vector3.one, t / half);
            yield return null;
        }
        rt.localScale = Vector3.one;
        _punch = null;
    }
}
