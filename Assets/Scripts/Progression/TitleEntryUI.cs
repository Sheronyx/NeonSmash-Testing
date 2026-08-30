using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Eine Zeile/Karte im Titelbuch (siehe TitleBookController): Name, Besitz-/Auswahl-Zustand,
// klickbar zum Auswählen (nur wenn besessen).
public class TitleEntryUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI nameLabel;
    [SerializeField] GameObject selectedMarker;
    [SerializeField] GameObject lockOverlay;
    [SerializeField] Button selectButton;
    [SerializeField] CanvasGroup canvasGroup;

    [Header("Auswahl-Pop-Animation")]
    [SerializeField] float popDuration  = 0.3f;
    [SerializeField] float popOvershoot = 1.3f;

    Coroutine _popRoutine;

    public void Bind(RewardDefinition title, bool owned, bool selected, Action<RewardDefinition> onSelect)
    {
        bool hideName = title.secret && !owned;
        if (nameLabel != null) nameLabel.text = hideName ? "???" : title.displayName;

        if (selectedMarker != null)
        {
            bool wasSelected = selectedMarker.activeSelf;
            selectedMarker.SetActive(selected);
            if (selected && !wasSelected)
                PlayPop();
        }

        if (lockOverlay != null) lockOverlay.SetActive(!owned);
        if (canvasGroup != null) canvasGroup.alpha = owned ? 1f : 0.4f;

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelect?.Invoke(title));
            selectButton.interactable = owned;
        }
    }

    void PlayPop()
    {
        if (_popRoutine != null) StopCoroutine(_popRoutine);
        _popRoutine = StartCoroutine(Co_Pop());
    }

    IEnumerator Co_Pop()
    {
        Transform t = selectedMarker.transform;
        Vector3 baseScale = Vector3.one;
        t.localScale = Vector3.zero;

        // Zwei Phasen: erst über die Zielgröße hinausschießen, dann zurück auf 1 einpendeln.
        const float growPhase = 0.5f;
        float time = 0f;
        while (time < popDuration)
        {
            time += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(time / popDuration);
            float s = p < growPhase
                ? Mathf.Lerp(0f, popOvershoot, p / growPhase)
                : Mathf.Lerp(popOvershoot, 1f, (p - growPhase) / (1f - growPhase));
            t.localScale = baseScale * s;
            yield return null;
        }
        t.localScale = baseScale;
    }
}
