using TMPro;
using UnityEngine;
using System.Collections;

public class ComboDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI multiplierText;

    [Header("Punch Animation")]
    [SerializeField] private float punchScale    = 1.25f;
    [SerializeField] private float punchDuration = 0.12f;

    private Coroutine _punch;

    private void OnEnable()
    {
        ComboManager.OnComboChanged    += HandleComboChanged;
        SpecialModeManager.OnModeStarted += HandleModeChanged;
        SpecialModeManager.OnModeEnded   += HandleModeChanged;
        RefreshCurrent();
    }

    private void OnDisable()
    {
        ComboManager.OnComboChanged    -= HandleComboChanged;
        SpecialModeManager.OnModeStarted -= HandleModeChanged;
        SpecialModeManager.OnModeEnded   -= HandleModeChanged;
    }

    private void HandleComboChanged(int combo)
    {
        Refresh(combo);
        if (combo > 1) Punch();
    }

    private void HandleModeChanged(SpecialMode _)
    {
        RefreshCurrent();
        Punch();
    }

    private void RefreshCurrent()
    {
        Refresh(ComboManager.Instance != null ? ComboManager.Instance.ComboCount : 0);
    }

    private void Refresh(int combo)
    {
        if (multiplierText == null) return;
        int comboMult = Mathf.Min(combo + 1, 10); // identisch mit ComboManager.Multiplier
        bool special  = SpecialModeManager.Instance != null && SpecialModeManager.Instance.IsModeActive;
        int totalMult = comboMult * (special ? 2 : 1);
        multiplierText.text = $"{totalMult}.0";
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
