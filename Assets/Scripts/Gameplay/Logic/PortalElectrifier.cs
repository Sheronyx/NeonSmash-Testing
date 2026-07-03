using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Verwaltet die Portal-Elektrifizierung.
/// Ablauf: Portal blitzt sofort (Vorwarnung), nach warningDuration werden alle Elemente elektrifiziert.
/// Spieler kann die Elektrifizierung durch Antippen des Portals aufheben (nur wenn IsActive).
/// </summary>
public class PortalElectrifier : MonoBehaviour
{
    public static PortalElectrifier Instance { get; private set; }

    /// <summary>Portal VFX läuft, Elemente noch NICHT elektrifiziert (Vorwarn-Phase).</summary>
    public static bool IsWarning { get; private set; }
    /// <summary>Elemente sind elektrifiziert, Antippen kostet ein Leben.</summary>
    public static bool IsActive  { get; private set; }

    public static event Action<bool> OnElectrificationChanged;

    [Header("Timing")]
    [Tooltip("Wie viele Sekunden das Portal vorwarnt bevor Elemente elektrifiziert werden.")]
    [SerializeField] private float warningDuration       = 1f;
    [SerializeField] private float postDeactivateCooldown = 7f;

    [Header("Element Overlay")]
    [Tooltip("Prefab das als Kind an jedes aktive Element gehängt wird (Elektro-Effekt).")]
    [SerializeField] private GameObject elementOverlayPrefab;

    [Header("Portal Tap-Erkennung")]
    [Tooltip("Radius in World Units innerhalb dem ein Tap das Portal trifft.")]
    [SerializeField] private float tapRadius = 1.2f;

    private bool      _onCooldown;
    private Coroutine _cooldownRoutine;
    private Coroutine _warningRoutine;
    private readonly List<GameObject> _overlays = new();

    public float TapRadius => tapRadius;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public bool CanActivate() => !IsActive && !IsWarning && !_onCooldown;

    public void Activate()
    {
        if (!CanActivate()) return;
        if (_warningRoutine != null) StopCoroutine(_warningRoutine);
        _warningRoutine = StartCoroutine(Co_Activate());
    }

    private IEnumerator Co_Activate()
    {
        // Phase 1: Portal blinkt — Spieler wird vorgewarnt, Elemente noch harmlos
        IsWarning = true;
        OnElectrificationChanged?.Invoke(true);   // Portal-VFX an

        yield return new WaitForSeconds(warningDuration);

        // Phase 2: Elemente elektrifizieren
        IsWarning = false;
        IsActive  = true;
        _warningRoutine = null;

        foreach (var tp in FindObjectsByType<TapPoint>(FindObjectsSortMode.None))
            AttachOverlay(tp.gameObject);
        foreach (var sp in FindObjectsByType<SwipePoint>(FindObjectsSortMode.None))
            AttachOverlay(sp.gameObject);
        foreach (var gp in FindObjectsByType<GravityPoint>(FindObjectsSortMode.None))
            AttachOverlay(gp.gameObject);
        foreach (var fp in FindObjectsByType<FountainPoint>(FindObjectsSortMode.None))
            AttachOverlay(fp.gameObject);
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;

        foreach (var o in _overlays)
            if (o != null) Destroy(o);
        _overlays.Clear();

        OnElectrificationChanged?.Invoke(false);

        if (_cooldownRoutine != null) StopCoroutine(_cooldownRoutine);
        _cooldownRoutine = StartCoroutine(Co_Cooldown());
    }

    public void ForceDeactivate()
    {
        bool wasShowing = IsActive || IsWarning;

        if (_warningRoutine != null) { StopCoroutine(_warningRoutine); _warningRoutine = null; }
        IsWarning = false;
        IsActive  = false;

        foreach (var o in _overlays)
            if (o != null) Destroy(o);
        _overlays.Clear();

        if (_cooldownRoutine != null) { StopCoroutine(_cooldownRoutine); _cooldownRoutine = null; }
        _onCooldown = false;

        if (wasShowing) OnElectrificationChanged?.Invoke(false);
    }

    public void ElectrifyElement(GameObject element)
    {
        if (!IsActive || element == null) return;
        AttachOverlay(element);
    }

    public void OnPortalTapped()
    {
        if (!IsActive) return;
        Deactivate();
    }

    private void AttachOverlay(GameObject element)
    {
        if (elementOverlayPrefab == null || element == null) return;
        var overlay = Instantiate(elementOverlayPrefab, element.transform);
        overlay.transform.localPosition = Vector3.zero;
        overlay.transform.localRotation = Quaternion.identity;
        foreach (var col in overlay.GetComponentsInChildren<Collider2D>(true))
            col.enabled = false;
        _overlays.Add(overlay);
    }

    private IEnumerator Co_Cooldown()
    {
        _onCooldown = true;
        yield return new WaitForSeconds(postDeactivateCooldown);
        _onCooldown = false;
        _cooldownRoutine = null;
    }
}
