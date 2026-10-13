using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Eine Kachel im Quests-Fenster: Aufgabentext, Fortschrittsbalken und der Belohnungs-Knopf.
// Die Kachel selbst bleibt immer dunkel -- nur der Knopf zeigt den Zustand an.
// Haelt selbst keinen Zustand, Bind() wird vom WeeklyQuestsController gerufen.
public class WeeklyQuestRowUI : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TextMeshProUGUI titleLabel;
    [Tooltip("Fortschritt als Text, z. B. \"35/150\".")]
    [SerializeField] private TextMeshProUGUI progressLabel;

    [Header("Fortschritt")]
    [Tooltip("Die Breite wird ueber anchorMax.x gesetzt, nicht ueber fillAmount.")]
    [SerializeField] private Image progressFill;

    [Header("Belohnung")]
    [Tooltip("Quests zahlen ausschliesslich Traumsplitter.")]
    [SerializeField] private TextMeshProUGUI splinterLabel;

    [Header("Abholen")]
    [Tooltip("Der Belohnungs-Knopf unten rechts -- er allein ist anklickbar.")]
    [SerializeField] private Button claimButton;
    [Tooltip("Alles, was mit dem Knopf ausgegraut wird: sein Bild, das Splitter-Symbol und die "
           + "Zahl. Weiss zeigt jeweils die Eigenfarbe, also das Lila des Knopf-Sprites.")]
    [SerializeField] private Graphic[] rewardGraphics;
    [Tooltip("Gruener Haken -- nur sichtbar, nachdem die Belohnung abgeholt wurde.")]
    [SerializeField] private GameObject claimedCheck;

    [Header("Abgeholt abdunkeln")]
    [Tooltip("Werden nach dem Abholen abgedunkelt: Balken, Fuellung und Fortschrittszahl. Der " +
             "Aufgabentext wird separat behandelt, der Haken bewusst gar nicht.")]
    [SerializeField] private Graphic[] dimWhenClaimed;
    [Tooltip("Helligkeit im abgeholten Zustand. 1 = unveraendert, 0 = schwarz.")]
    [Range(0f, 1f)] [SerializeField] private float claimedDim = 0.4f;

    [Header("Farben")]
    [Tooltip("Titelfarbe, solange die Aufgabe laeuft.")]
    [SerializeField] private Color unfinishedColor = new Color(1f, 1f, 1f, 0.6f);
    [Tooltip("Titelfarbe, sobald die Aufgabe erfuellt ist.")]
    [SerializeField] private Color finishedColor   = Color.white;
    [Tooltip("Knopffarbe, solange nichts abzuholen ist -- und wieder, nachdem abgeholt wurde.")]
    [SerializeField] private Color rewardLockedColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    [Tooltip("Knopffarbe, sobald abholbar: weiss, damit das Sprite seine eigene Farbe zeigt.")]
    [SerializeField] private Color rewardReadyColor = Color.white;

    [Header("Haken-Animation")]
    [Tooltip("Dauer des Aufpoppens beim Abholen.")]
    [SerializeField] private float checkPopDuration = 0.38f;
    [Tooltip("Wie weit der Haken ueber seine Endgroesse hinausschiesst, bevor er einrastet.")]
    [SerializeField] private float checkPopOvershoot = 1.9f;

    private WeeklyQuestData _quest;
    private System.Action<WeeklyQuestData> _onClaim;
    private Color[]   _dimRuhefarben;
    private bool      _warAbgeholt;
    private bool      _ersterBind = true;
    private Coroutine _popLauf;

    private void Awake()
    {
        if (claimButton != null) claimButton.onClick.AddListener(Claim);

        RuhefarbenSichern();
    }

    // Ruhefarben einmal merken: Bind() rechnet daraus ab, sonst wuerde sich das Abdunkeln bei
    // jedem Aufruf erneut auf den schon dunklen Wert anwenden.
    // Wird auch aus Bind() gerufen: das Panel ist beim Szenenstart ausgeschaltet, Awake() laeuft
    // also erst beim ersten Oeffnen -- und der Controller bindet die Zeilen VORHER.
    private void RuhefarbenSichern()
    {
        if (_dimRuhefarben != null || dimWhenClaimed == null) return;
        _dimRuhefarben = new Color[dimWhenClaimed.Length];
        for (int i = 0; i < dimWhenClaimed.Length; i++)
            _dimRuhefarben[i] = dimWhenClaimed[i] != null ? dimWhenClaimed[i].color : Color.white;
    }

    private static Color Abdunkeln(Color farbe, float faktor)
        => new Color(farbe.r * faktor, farbe.g * faktor, farbe.b * faktor, farbe.a);

    public void Bind(WeeklyQuestData quest, System.Action<WeeklyQuestData> onClaim)
    {
        _quest   = quest;
        _onClaim = onClaim;

        if (quest == null) { gameObject.SetActive(false); return; }
        gameObject.SetActive(true);
        RuhefarbenSichern();

        if (titleLabel    != null) titleLabel.text    = quest.Title;
        if (progressLabel != null) progressLabel.text = Mathf.Min(quest.Progress, quest.Target) + "/" + quest.Target;
        if (splinterLabel != null) splinterLabel.text = quest.Splinters.ToString();

        // Balkenbreite ueber die Anker statt ueber fillAmount: das haengt an keinem Sprite
        // und laeuft garantiert von links (0) nach rechts (1).
        if (progressFill != null)
        {
            var rt = progressFill.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(Mathf.Clamp01(quest.Fraction), 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // Nach dem Abholen ist die Kachel erledigt -- Text, Balken und Zahl treten zurueck,
        // damit nur noch der gruene Haken ins Auge faellt.
        float dim = quest.Claimed ? claimedDim : 1f;

        if (titleLabel != null)
            titleLabel.color = Abdunkeln(quest.IsCompleted ? finishedColor : unfinishedColor, dim);

        if (dimWhenClaimed != null && _dimRuhefarben != null
            && _dimRuhefarben.Length == dimWhenClaimed.Length)
            for (int i = 0; i < dimWhenClaimed.Length; i++)
                if (dimWhenClaimed[i] != null)
                    dimWhenClaimed[i].color = Abdunkeln(_dimRuhefarben[i], dim);

        // Nur der Knopf wechselt die Farbe: hell, solange es etwas zu holen gibt, sonst dunkel.
        // Nach dem Abholen faellt er damit wieder in den dunklen Zustand zurueck.
        bool abholbar = quest.IsCompleted && !quest.Claimed;
        if (claimButton != null) claimButton.interactable = abholbar;
        var tont = abholbar ? rewardReadyColor : rewardLockedColor;
        if (rewardGraphics != null)
            foreach (var g in rewardGraphics)
                if (g != null) g.color = tont;
        // Der Haken poppt nur auf, wenn er gerade erst dazugekommen ist -- nicht jedes Mal,
        // wenn das Fenster mit einer laengst abgeholten Aufgabe geoeffnet wird.
        if (claimedCheck != null)
        {
            bool frisch = quest.Claimed && !_warAbgeholt && !_ersterBind;
            claimedCheck.SetActive(quest.Claimed);

            if (_popLauf != null) { StopCoroutine(_popLauf); _popLauf = null; }
            if (frisch) _popLauf = StartCoroutine(HakenPoppt());
            else        claimedCheck.transform.localScale = Vector3.one;
        }
        _warAbgeholt = quest.Claimed;
        _ersterBind  = false;
    }

    // Waechst von null auf die Endgroesse und schiesst dabei kurz darueber hinaus.
    private IEnumerator HakenPoppt()
    {
        var t = claimedCheck.transform;
        t.localScale = Vector3.zero;

        float zeit = 0f;
        while (zeit < checkPopDuration)
        {
            zeit += Time.unscaledDeltaTime;          // laeuft auch im pausierten Menue
            float p = Mathf.Clamp01(zeit / checkPopDuration);
            // Ease-Out-Back: ueberschwingt einmal und rastet dann auf 1 ein.
            float q = p - 1f;
            float wert = 1f + (checkPopOvershoot + 1f) * q * q * q + checkPopOvershoot * q * q;
            t.localScale = Vector3.one * wert;
            yield return null;
        }

        t.localScale = Vector3.one;
        _popLauf = null;
    }

    private void Claim()
    {
        if (_quest == null) return;
        _onClaim?.Invoke(_quest);
    }
}
