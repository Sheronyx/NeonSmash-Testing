using System.Collections;
using UnityEngine;

// Lauscht auf ComboManager und spawnt Combo-Meldungen in der Bildschirmmitte.
public class FloatingComboPopup : MonoBehaviour
{
    public static FloatingComboPopup Instance { get; private set; }

    [Header("Prefabs")]
    [SerializeField] private GameObject comboActivatedPrefab;

    [Header("Timing")]
    [Tooltip("Delay nach dem Treffer bevor der Kombotext erscheint (nach der Score-Zahl)")]
    [SerializeField] private float comboActivatedDelay = 0.15f;
    [Tooltip("Gesamtdauer der Kombotext-Animation (punchIn + hold + punchOut)")]
    [SerializeField] private float comboTotalDuration  = 2.00f;

    [Header("Spawn-Position")]
    [SerializeField] private Vector3 spawnOffset = Vector3.zero;

    private Camera _cam;
    private bool   _wasComboActive;
    private bool   _hadComboEvent;
    private float  _comboEventTime = -1f;

    private void Awake()
    {
        Instance = this;
        _cam = Camera.main;
    }

    private void OnEnable()
    {
        ComboManager.OnComboChanged += HandleComboChanged;
        _wasComboActive = false;
        _hadComboEvent  = false;
    }

    private void OnDisable() => ComboManager.OnComboChanged -= HandleComboChanged;

    private void HandleComboChanged(int streak)
    {
        bool isNowActive = streak >= 5;

        if (isNowActive && !_wasComboActive)
        {
            _hadComboEvent  = true;
            _comboEventTime = Time.realtimeSinceStartup;
            PointColor? color = ComboManager.Instance?.CurrentColor;
            if (color.HasValue) StartCoroutine(Co_ShowActivated(color.Value));
        }
        else if (!isNowActive && _wasComboActive)
        {
            // Combo-Lost absichtlich nicht angezeigt
        }

        _wasComboActive = isNowActive;
    }

    /// <summary>
    /// Gibt die noch verbleibende Kombotext-Anzeigedauer zurück (ab jetzt gemessen).
    /// Wenn der Text schon längst fertig ist, wird defaultDelay zurückgegeben.
    /// Einmalig: setzt den Flag danach zurück.
    /// </summary>
    public float ConsumeSpawnDelay(float defaultDelay)
    {
        if (!_hadComboEvent) return defaultDelay;
        _hadComboEvent = false;
        float elapsed   = Time.realtimeSinceStartup - _comboEventTime;
        float remaining = (comboActivatedDelay + comboTotalDuration) - elapsed;
        return Mathf.Max(defaultDelay, remaining);
    }

    private IEnumerator Co_ShowActivated(PointColor color)
    {
        yield return new WaitForSeconds(comboActivatedDelay);

        if (comboActivatedPrefab == null) yield break;

        string message = color switch
        {
            PointColor.Pink   => "PINK\nCOMBO",
            PointColor.Green  => "GREEN\nCOMBO",
            PointColor.Blue   => "BLUE\nCOMBO",
            _                 => "COMBO"
        };

        Spawn(comboActivatedPrefab, message, Color.white, color);
    }

    private void Spawn(GameObject prefab, string message, Color color, PointColor? pointColor)
    {
        Vector3 center = _cam != null
            ? _cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, Mathf.Abs(_cam.transform.position.z)))
            : Vector3.zero;
        center.z = 0f;

        var go  = Instantiate(prefab, center + spawnOffset, Quaternion.identity);
        var fct = go.GetComponentInChildren<FloatingComboText>();
        if (fct != null) fct.Play(message, color, pointColor);
        else Destroy(go, 2f);
    }
}
