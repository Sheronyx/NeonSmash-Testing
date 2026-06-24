using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// An einen UI-Button hängen → startet ein Rewarded-Video und schreibt bei Erfolg Coins gut.
/// Der Button wird deaktiviert, wenn gerade kein Rewarded-Video bereit ist.
/// </summary>
[RequireComponent(typeof(Button))]
public class RewardedCoinsButton : MonoBehaviour
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

        AdManager.Instance.ShowRewardedForCoins(
            onGranted: amount =>
            {
                // Coins sind bereits auf dem Konto (CoinManager); hier nur die Anzeige
                // animieren — die fliegt vom Button zum Coin-Icon und zählt hoch.
                CoinDisplayUI.Instance?.FlyCoinsFrom(amount, transform.position);
                Debug.Log($"[Ads] +{amount} Coins gutgeschrieben.");
            },
            onUnavailable: () => { if (unavailableHint) unavailableHint.SetActive(true); });
    }
}
