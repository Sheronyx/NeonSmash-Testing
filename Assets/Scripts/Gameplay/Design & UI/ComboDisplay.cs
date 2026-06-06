using TMPro;
using UnityEngine;
using System.Collections;

/// <summary>
/// Zeigt den aktuellen Combo-Multiplikator an.
/// An ein UI-GameObject hängen; comboRoot wird versteckt solange Combo ≤ 1.
/// </summary>
public class ComboDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI multiplierText;
    [SerializeField] private GameObject      comboRoot;

    [Header("Punch Animation")]
    [SerializeField] private float punchScale    = 1.25f;
    [SerializeField] private float punchDuration = 0.12f;

    private Coroutine _punch;

    private void OnEnable()
    {
        ComboManager.OnComboChanged += HandleComboChanged;
        Refresh(ComboManager.Instance != null ? ComboManager.Instance.ComboCount : 0);
    }

    private void OnDisable()
    {
        ComboManager.OnComboChanged -= HandleComboChanged;
    }

    private void HandleComboChanged(int combo)
    {
        Refresh(combo);
        if (combo > 1) Punch();
    }

    private void Refresh(int combo)
    {
        int mult = Mathf.Clamp(combo, 1, 10);
        if (comboRoot != null) comboRoot.SetActive(combo > 1);
        if (multiplierText != null) multiplierText.text = $"x{mult}";
    }

    private void Punch()
    {
        if (_punch != null) StopCoroutine(_punch);
        _punch = StartCoroutine(Co_Punch());
    }

    private IEnumerator Co_Punch()
    {
        if (multiplierText == null) yield break;
        var rt = multiplierText.rectTransform;
        float half = punchDuration * 0.5f;
        float t = 0f;
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
