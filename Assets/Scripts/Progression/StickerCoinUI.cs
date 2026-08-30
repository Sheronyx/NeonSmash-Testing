using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Eine klickbare Sticker-Münze im Sticker-Album (siehe RewardWindowController): zeigt das
// Stickerbild (Farbe/Optik kommt komplett aus dem Artwork selbst, wird hier nicht angefasst),
// eine Auswahl-Markierung falls dies der aktuell gewählte Sticker ist, die besessene Stückzahl
// (Sticker sind immer zufällig, Duplikate sind normal), und ist bei unbesessenen Stickern gesperrt
// (abgedunkelt, nicht klickbar). Klick auf eine besessene Münze öffnet das Detail-/Verkaufs-Popup
// (siehe StickerDetailPopupController), nicht mehr direkt die Auswahl als Porträt.
public class StickerCoinUI : MonoBehaviour
{
    [SerializeField] Image iconImage;
    [SerializeField] GameObject selectedRing;
    [SerializeField] GameObject lockOverlay;
    [SerializeField] Button button;
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] TextMeshProUGUI countLabel;

    [Header("Auswahl-Pop-Animation")]
    [SerializeField] float popDuration  = 0.3f;
    [SerializeField] float popOvershoot = 1.3f;

    Coroutine _popRoutine;

    public void Bind(RewardDefinition sticker, int count, bool selected, Action<RewardDefinition> onTap)
    {
        bool owned = count > 0;

        if (iconImage != null && sticker.icon != null)
            iconImage.sprite = sticker.icon;

        if (selectedRing != null)
        {
            bool wasSelected = selectedRing.activeSelf;
            selectedRing.SetActive(selected);
            if (selected && !wasSelected)
                PlayPop();
        }

        if (lockOverlay != null)  lockOverlay.SetActive(!owned);
        if (canvasGroup != null)  canvasGroup.alpha = owned ? 1f : 0.35f;

        if (countLabel != null)
        {
            countLabel.gameObject.SetActive(owned);
            if (owned) countLabel.text = "x" + count;
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onTap?.Invoke(sticker));
            button.interactable = owned;
        }
    }

    void PlayPop()
    {
        if (_popRoutine != null) StopCoroutine(_popRoutine);
        _popRoutine = StartCoroutine(Co_Pop());
    }

    IEnumerator Co_Pop()
    {
        Transform t = selectedRing.transform;
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
