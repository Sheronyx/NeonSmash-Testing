using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// An einen UI-Button hängen → startet ein Rewarded-Video und schreibt bei Erfolg Dream Energy gut.
/// Der Button wird deaktiviert, wenn gerade kein Rewarded-Video bereit ist.
/// </summary>
[RequireComponent(typeof(Button))]
public class RewardedDreamEnergyButton : MonoBehaviour
{
    [Tooltip("Optional: wird kurz eingeblendet, wenn gerade keine Anzeige verfügbar ist.")]
    [SerializeField] private GameObject unavailableHint;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnClick);
    }

    private void Update()
    {
        // Button nur klickbar, wenn ein Rewarded geladen ist
        bool ready = AdManager.Instance != null && AdManager.Instance.IsRewardedReady;
        if (_button.interactable != ready) _button.interactable = ready;
    }

    private void OnClick()
    {
        if (AdManager.Instance == null) return;

        AdManager.Instance.ShowRewardedForDreamEnergy(
            onGranted: amount =>
            {
                // Dream Energy ist bereits auf dem Konto (DreamEnergyManager); hier nur die
                // Anzeige animieren — die fliegt vom Button zum Dream-Energy-Icon und zählt hoch.
                DreamEnergyDisplayUI.Instance?.FlyDreamEnergyFrom(amount, transform.position);
                Debug.Log($"[Ads] +{amount} Dream Energy gutgeschrieben.");
            },
            onUnavailable: () => { if (unavailableHint) unavailableHint.SetActive(true); });
    }
}
