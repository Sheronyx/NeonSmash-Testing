using System;
using System.Collections;
using UnityEngine;

public class FloatingMine : MonoBehaviour
{
    [Header("Schwebende Bewegung")]
    [SerializeField] private float amplitudeX       = 0.30f;
    [SerializeField] private float amplitudeY       = 0.20f;
    [SerializeField] private float freqX            = 0.38f;
    [SerializeField] private float freqY            = 0.55f;
    [SerializeField] private float amplitudeBlendIn = 1.2f;  // Sekunden bis volle Amplitude

    [Header("Antippen")]
    [SerializeField] private float tapShakeStrength = 0.07f;
    [SerializeField] private float tapShakeDuration = 0.12f;

    [Header("Ein- / Ausschweben")]
    [SerializeField] private float enterDuration   = 1.2f;
    [SerializeField] private float exitDuration    = 0.85f;
    [Tooltip("Startversatz beim Reinschweben (relativ zur Zielposition). " +
             "Z.B. (0, -2, 0) = startet 2 Units unterhalb.")]
    [SerializeField] private Vector3 enterOffset   = new Vector3(0f, -2f, 0f);

    // Prefab-Werte (in Awake gespeichert, bevor wir irgendwas verändern)
    private Vector3    _targetScale;
    private Quaternion _targetRotation;

    // Laufzeit
    private Vector3   _basePos;
    private float     _phaseOffset;
    private bool      _floating;
    private float     _floatStartTime;
    private Coroutine _animRoutine;

    private void Awake()
    {
        _targetScale    = transform.localScale;
        _targetRotation = transform.localRotation;
    }

    // Wird vom FloatingMineSystem aufgerufen.
    public void Enter(Vector3 worldPos)
    {
        _basePos     = worldPos;
        _phaseOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

        // Startposition: Ziel + Versatz (wirkt wie "aus dem Hintergrund / von unten")
        transform.position      = worldPos + enterOffset;
        transform.localScale    = Vector3.zero;
        transform.localRotation = _targetRotation;

        if (_animRoutine != null) StopCoroutine(_animRoutine);
        _animRoutine = StartCoroutine(Co_Enter());
    }

    public void Exit(Action onDone = null)
    {
        _floating = false;
        if (_animRoutine != null) StopCoroutine(_animRoutine);
        _animRoutine = StartCoroutine(Co_Exit(onDone));
    }

    public void OnTapped()
    {
        ScreenShakeManager.Instance?.Shake(tapShakeStrength, tapShakeDuration);
    }

    private void Update()
    {
        if (!_floating) return;

        float elapsed = Time.time - _floatStartTime;

        // Amplitude blendet sanft ein → kein harter Ruck beim Übergang vom Enter
        float blend = amplitudeBlendIn > 0f
            ? Mathf.SmoothStep(0f, 1f, elapsed / amplitudeBlendIn)
            : 1f;

        float t = elapsed + _phaseOffset;
        transform.position = _basePos + new Vector3(
            Mathf.Sin(t * freqX * Mathf.PI * 2f) * amplitudeX * blend,
            Mathf.Sin(t * freqY * Mathf.PI * 2f) * amplitudeY * blend,
            0f
        );
    }

    private IEnumerator Co_Enter()
    {
        Vector3 startPos = _basePos + enterOffset;

        float t = 0f;
        while (t < enterDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / enterDuration);

            transform.position      = Vector3.Lerp(startPos, _basePos, k);
            transform.localScale    = _targetScale * k;
            transform.localRotation = _targetRotation;
            yield return null;
        }

        transform.position      = _basePos;
        transform.localScale    = _targetScale;
        transform.localRotation = _targetRotation;

        _floatStartTime = Time.time;
        _floating       = true;
        _animRoutine    = null;
    }

    private IEnumerator Co_Exit(Action onDone)
    {
        Vector3 startPos   = transform.position;
        Vector3 startScale = transform.localScale;

        float t = 0f;
        while (t < exitDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / exitDuration);

            transform.position   = Vector3.Lerp(startPos, startPos + enterOffset, k);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, k);
            yield return null;
        }

        transform.localScale = Vector3.zero;
        onDone?.Invoke();
        Destroy(gameObject);
    }
}
