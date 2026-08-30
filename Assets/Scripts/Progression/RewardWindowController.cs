using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum RewardWindowTab
{
    Levels,
    Stickers,
    Titles,
}

// Fortschritt-/XP-/Belohnungssystemfenster: zeigt die aktuelle Stufe, die Traumenergie-
// Fortschrittsleiste bis zur nächsten Stufe, und drei Tabs (analog ShopController) —
// Stufen-Übersicht, Sticker-Album, Titelbuch — die alle im selben Fenster umschalten statt
// eigene Canvases zu öffnen. Struktur/Optik an ShopController angelehnt (gleiches Panel-Popup,
// gleiche Open/Close-Animation und Tab-Umschaltung).
public class RewardWindowController : MonoBehaviour
{
    public static RewardWindowController Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] CanvasGroup panel;
    [SerializeField] Button closeButton;

    [Header("Header")]
    [SerializeField] TextMeshProUGUI levelLabel;
    [SerializeField] Image fillImage;
    [SerializeField] Image nextTierIcon;

    [Header("Tabs")]
    [SerializeField] Button tabLevelsButton;
    [SerializeField] Button tabStickersButton;
    [SerializeField] Button tabTitlesButton;
    [SerializeField] Color tabActiveColor   = Color.white;
    [SerializeField] Color tabInactiveColor = new Color(1f, 1f, 1f, 0.4f);

    [Header("Stufen-Tab")]
    [SerializeField] ScrollRect itemScrollRect;
    [SerializeField] Transform levelsGridParent;
    [SerializeField] RewardTierCardUI tierCardPrefab;

    [Header("Sticker-Tab")]
    [SerializeField] Transform stickersGridParent;
    [SerializeField] StickerCoinUI stickerCoinPrefab;

    [Header("Titel-Tab")]
    [SerializeField] Transform titlesGridParent;
    [SerializeField] TitleEntryUI titleEntryPrefab;

    [Header("Daten")]
    [SerializeField] LevelRewardTrack rewardTrack;
    [SerializeField] RewardCatalogue catalogue;

    [Header("Animation")]
    [SerializeField] float popInDuration  = 0.28f;
    [SerializeField] float popOutDuration = 0.2f;

    RewardWindowTab _activeTab = RewardWindowTab.Levels;
    bool _open;

    void Awake()
    {
        Instance = this;
        if (panel != null) panel.gameObject.SetActive(false);
    }

    void OnEnable()
    {
        if (closeButton        != null) closeButton.onClick.AddListener(Close);
        if (tabLevelsButton    != null) tabLevelsButton.onClick.AddListener(() => SwitchTab(RewardWindowTab.Levels));
        if (tabStickersButton  != null) tabStickersButton.onClick.AddListener(() => SwitchTab(RewardWindowTab.Stickers));
        if (tabTitlesButton    != null) tabTitlesButton.onClick.AddListener(() => SwitchTab(RewardWindowTab.Titles));
        StickerManager.OnCountChanged           += HandleStickerCountChanged;
        StickerManager.OnSelectedStickerChanged += HandleStickerSelectedChanged;
    }

    // Hält das Album-Grid mit dem Detail-/Verkaufs-Popup synchron (z.B. Stückzahl nach Verkauf,
    // oder Checkmark nach SELECT), ohne dass das Popup selbst wissen muss, wie das Grid dahinter
    // aufgebaut ist.
    void HandleStickerCountChanged(string rewardId, int newCount)
    {
        if (_activeTab == RewardWindowTab.Stickers) PopulateStickers();
    }

    void HandleStickerSelectedChanged(string rewardId)
    {
        if (_activeTab == RewardWindowTab.Stickers) PopulateStickers();
    }

    void OnDisable()
    {
        if (closeButton        != null) closeButton.onClick.RemoveListener(Close);
        if (tabLevelsButton    != null) tabLevelsButton.onClick.RemoveAllListeners();
        if (tabStickersButton  != null) tabStickersButton.onClick.RemoveAllListeners();
        if (tabTitlesButton    != null) tabTitlesButton.onClick.RemoveAllListeners();
        StickerManager.OnCountChanged           -= HandleStickerCountChanged;
        StickerManager.OnSelectedStickerChanged -= HandleStickerSelectedChanged;
    }

    public void Open()
    {
        if (_open) return;
        _open = true;

        RefreshHeader();
        SwitchTab(RewardWindowTab.Levels);
        DimOverlay.Instance?.Show();
        StartCoroutine(Co_Open());
    }

    public void Close()
    {
        if (!_open) return;
        _open = false;
        DimOverlay.Instance?.Hide();
        StartCoroutine(Co_Close());
    }

    void RefreshHeader()
    {
        int lifetime = DreamEnergyManager.LifetimeEarned;
        var (fill, _, nextTierIndex) = PlayerLevelManager.Evaluate(lifetime);

        if (levelLabel != null) levelLabel.text = LeaderboardApi.GetLocalDisplayName();
        if (fillImage != null)  fillImage.fillAmount = fill;

        var tiers = rewardTrack != null ? rewardTrack.SortedTiers() : new LevelRewardTrack.LevelTier[0];
        bool hasNext = nextTierIndex >= 0 && nextTierIndex < tiers.Length;
        if (nextTierIcon != null)
        {
            nextTierIcon.gameObject.SetActive(hasNext);
            if (hasNext && tiers[nextTierIndex].rewards != null && tiers[nextTierIndex].rewards.Length > 0)
            {
                var firstReward = tiers[nextTierIndex].rewards[0];
                if (firstReward != null && firstReward.icon != null)
                    nextTierIcon.sprite = firstReward.icon;
            }
        }
    }

    // ── Tabs ─────────────────────────────────────────────────────────────────

    void SwitchTab(RewardWindowTab tab)
    {
        _activeTab = tab;
        UpdateTabHighlights();

        if (levelsGridParent   != null) levelsGridParent.gameObject.SetActive(tab == RewardWindowTab.Levels);
        if (stickersGridParent != null) stickersGridParent.gameObject.SetActive(tab == RewardWindowTab.Stickers);
        if (titlesGridParent   != null) titlesGridParent.gameObject.SetActive(tab == RewardWindowTab.Titles);

        if (itemScrollRect != null)
        {
            itemScrollRect.content = tab switch
            {
                RewardWindowTab.Stickers => (RectTransform)stickersGridParent,
                RewardWindowTab.Titles   => (RectTransform)titlesGridParent,
                _                        => (RectTransform)levelsGridParent,
            };
            itemScrollRect.verticalNormalizedPosition = 1f;
        }

        switch (tab)
        {
            case RewardWindowTab.Levels:   PopulateLevels();   break;
            case RewardWindowTab.Stickers: PopulateStickers(); break;
            case RewardWindowTab.Titles:   PopulateTitles();   break;
        }
    }

    void UpdateTabHighlights()
    {
        SetTabColor(tabLevelsButton,   _activeTab == RewardWindowTab.Levels);
        SetTabColor(tabStickersButton, _activeTab == RewardWindowTab.Stickers);
        SetTabColor(tabTitlesButton,   _activeTab == RewardWindowTab.Titles);
    }

    void SetTabColor(Button btn, bool active)
    {
        if (btn == null) return;
        var label = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.color = active ? tabActiveColor : tabInactiveColor;
    }

    // ── Stufen-Tab ───────────────────────────────────────────────────────────

    [Header("Pfad-Farben (Stufen-Tab)")]
    [SerializeField] Color pathReachedColor = new Color(0.72f, 0.55f, 0.95f, 1f); // helles Lila
    [SerializeField] Color pathLockedColor  = new Color(0.30f, 0.28f, 0.35f, 1f); // dunkel/gedämpft
    [SerializeField] Color pathGlowColor    = new Color(0.78f, 0.62f, 1f, 0.9f);

    [Tooltip("Feinjustierung in Pixeln: verschiebt nur den START-Punkt der Pfad-Füllung (untere Karte) zusätzlich zur automatisch aus der Karten-Grafik ermittelten sichtbaren Kante. Positiv = weiter von der Karte weg (tiefer in die Lücke). Der End-Punkt (obere Kante der nächsten Karte) bleibt unverändert.")]
    [SerializeField] float pathVisibleMarginAdjust = 4f;

    void PopulateLevels()
    {
        if (levelsGridParent == null || tierCardPrefab == null || rewardTrack == null) return;

        foreach (Transform child in levelsGridParent)
            Destroy(child.gameObject);

        var tiers = rewardTrack.SortedTiers();
        BuildPathSegments(tiers);

        foreach (var tier in tiers)
        {
            var card = Instantiate(tierCardPrefab, levelsGridParent);
            card.Bind(tier);
        }
    }

    // Ein Pfadsegment je Übergang zwischen zwei Stufen — als erste Kinder von levelsGridParent
    // eingefügt (rendern daher vor/unter allen später instanziierten Karten) und mit
    // LayoutElement.ignoreLayout markiert, damit das GridLayoutGroup sie nicht als eigene Zelle
    // einsortiert. Nur in den Lücken zwischen den (opaken) Karten sichtbar — läuft optisch hinter
    // den Karten durch. Farbe pro Segment: helles Lila, sobald die OBERE der beiden verbundenen
    // Stufen erreicht ist, sonst gedämpft.
    void BuildPathSegments(LevelRewardTrack.LevelTier[] tiers)
    {
        if (tiers.Length < 2) return;
        var glg = levelsGridParent.GetComponent<GridLayoutGroup>();
        if (glg == null) return;

        float cellH   = glg.cellSize.y;
        float gapY    = glg.spacing.y;
        float segmentH = cellH + gapY;
        float firstSegmentTop = glg.padding.top + cellH * 0.5f;

        // Die Karten-Grafik (Card Face) hat selbst einen getrimmten transparenten Rand (das Sprite
        // ist nicht randlos) — der wirklich SICHTBARE Kartenrahmen ist also kleiner als cellH. Ohne
        // das zu berücksichtigen, würde die Fortschrittsfüllung nur den winzigen spacing-Spalt statt
        // den tatsächlich sichtbaren freien Bereich zwischen den Karten abdecken.
        (float topMarginPx, float bottomMarginPx) = GetCardVisibleMargins(cellH);

        int lifetime = DreamEnergyManager.LifetimeEarned;
        var (currentFill, currentTierIndex, _) = PlayerLevelManager.Evaluate(lifetime);

        for (int i = 0; i < tiers.Length - 1; i++)
        {
            // Fortschritt dieses Segments (0..1): volle Segmente vor der aktuellen Stufe sind ganz
            // gefüllt, das Segment ZUR aktuell nächsten Stufe füllt sich anteilig mit dem exakt
            // gleichen Fortschritt wie die Header-Fortschrittsleiste, alles danach ist noch leer.
            float progress = i < currentTierIndex ? 1f : i == currentTierIndex ? currentFill : 0f;
            CreatePathSegment(i, firstSegmentTop + i * segmentH, segmentH, cellH, gapY, topMarginPx, bottomMarginPx, progress);
        }
    }

    // Liefert (oberer, unterer) wirklich sichtbarer/freier Rand der Karte in Pixeln, umgerechnet auf
    // die tatsächliche Kartenhöhe (cellH):
    // - oben: der transparente Rand der Card-Face-Grafik selbst (das Sprite ist getrimmt) — dort
    //   sitzt nur der "LEVEL X"-Text, der den Pfad nicht verdeckt, also wirklich frei sichtbar.
    // - unten: NICHT der transparente Sprite-Rand, sondern der Abstand zwischen dem undurchsichtigen
    //   Collect/Locked-Button und der Kartenunterkante — der Button selbst deckt seinen Teil des
    //   transparenten Rands bereits blickdicht ab, nur der schmale Rest darunter ist wirklich frei.
    (float top, float bottom) GetCardVisibleMargins(float cellH)
    {
        if (tierCardPrefab == null) return (0f, 0f);

        var face = tierCardPrefab.transform.Find("Card Face");
        var sprite = face != null ? face.GetComponent<Image>().sprite : null;
        float topMargin = 0f;
        if (sprite != null && sprite.rect.height > 0f)
        {
            float topFrac = 1f - (sprite.textureRect.y + sprite.textureRect.height) / sprite.rect.height;
            topMargin = topFrac * cellH;
        }

        var collectButtonRt = tierCardPrefab.transform.Find("Collect Button") as RectTransform;
        float bottomMargin = collectButtonRt != null ? collectButtonRt.anchoredPosition.y : 0f;

        // Feinjustierung: schiebt nur den START-Punkt (untere Karte) zusätzlich von der Karte weg
        // (siehe Tooltip auf pathVisibleMarginAdjust) — der obere Rand (Ende der Füllung, Übergang
        // zur nächsten Karte) bleibt unverändert, sonst entsteht dort beim Erreichen der nächsten
        // Stufe eine kleine Lücke, statt dass die Füllung nahtlos an der Karte endet.
        bottomMargin = Mathf.Max(0f, bottomMargin - pathVisibleMarginAdjust);

        return (topMargin, bottomMargin);
    }

    void CreatePathSegment(int index, float topOffset, float height, float cellH, float gapY, float topMarginPx, float bottomMarginPx, float progress)
    {
        bool reached = progress >= 1f;
        var segGO = new GameObject("Path Segment " + index, typeof(RectTransform), typeof(LayoutElement));
        segGO.transform.SetParent(levelsGridParent, false);
        segGO.transform.SetSiblingIndex(index); // vor allen Karten, in aufsteigender Reihenfolge
        segGO.GetComponent<LayoutElement>().ignoreLayout = true;

        var rt = segGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -topOffset);
        rt.sizeDelta = new Vector2(40, height);

        // "Rahmen" ist ein echter, etwas breiterer Hintergrund-Rect hinter Fill (nicht dupliziert
        // versetzt wie eine UI-Outline-Komponente) — bei erreichten Segmenten hell-lila eingefärbt,
        // sodass links/rechts von Fill ein schmaler leuchtender Rand sichtbar bleibt.
        var frameGO = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        frameGO.transform.SetParent(segGO.transform, false);
        var frameRt = frameGO.GetComponent<RectTransform>();
        frameRt.anchorMin = Vector2.zero; frameRt.anchorMax = Vector2.one; frameRt.offsetMin = Vector2.zero; frameRt.offsetMax = Vector2.zero;
        frameGO.GetComponent<Image>().color = reached ? pathGlowColor : Color.black;

        var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillGO.transform.SetParent(segGO.transform, false);
        var fillRt = fillGO.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(6, 0); fillRt.offsetMax = new Vector2(-6, 0);
        fillGO.GetComponent<Image>().color = pathLockedColor; // leerer "Rest" des Segments

        // Fortschrittsanzeige über dem leeren Fill: füllt sich von oben (sichtbares Ende der
        // Start-Karte) nach unten (sichtbarer Anfang der Ziel-Karte) proportional zum XP-Fortschritt
        // — exakt wie die Header-Fortschrittsleiste. WICHTIG: "sichtbar" heißt hier nicht einfach nur
        // der spacing-Spalt zwischen den Zellen, sondern inklusive des transparenten Rands der
        // Karten-Grafik selbst (siehe GetCardVisibleMargins) — sonst würde die Füllung nur einen
        // winzigen Bruchteil des tatsächlich sichtbaren freien Bereichs abdecken.
        float visibleGapStart  = cellH * 0.5f - bottomMarginPx;
        float visibleGapHeight = gapY + topMarginPx + bottomMarginPx;

        var progressGO = new GameObject("Fill Progress", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        progressGO.transform.SetParent(segGO.transform, false);
        var progressRt = progressGO.GetComponent<RectTransform>();
        progressRt.anchorMin = new Vector2(0f, 1f);
        progressRt.anchorMax = new Vector2(1f, 1f);
        progressRt.pivot = new Vector2(0.5f, 1f);
        progressRt.anchoredPosition = new Vector2(0, -visibleGapStart);
        progressRt.sizeDelta = new Vector2(-12, visibleGapHeight); // -12 = gleicher 6px-Inset links/rechts wie Fill
        var progressImg = progressGO.GetComponent<Image>();
        // Image.Type.Filled ignoriert fillAmount komplett und rendert stattdessen ein volles Rechteck,
        // wenn kein Sprite gesetzt ist (Unity fällt dann auf einen einfachen Vollflächen-Quad zurück) —
        // deshalb hier ein generisches weißes Pixel-Sprite zuweisen, sonst wirkt der Pfad immer "voll".
        progressImg.sprite = WhitePixelSprite;
        progressImg.color = pathReachedColor;
        progressImg.type = Image.Type.Filled;
        progressImg.fillMethod = Image.FillMethod.Vertical;
        progressImg.fillOrigin = (int)Image.OriginVertical.Top;
        progressImg.fillAmount = Mathf.Clamp01(progress);
    }

    static Sprite _whitePixelSprite;
    static Sprite WhitePixelSprite
    {
        get
        {
            if (_whitePixelSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                _whitePixelSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            return _whitePixelSprite;
        }
    }

    // ── Sticker-Tab ──────────────────────────────────────────────────────────

    void PopulateStickers()
    {
        if (stickersGridParent == null || stickerCoinPrefab == null || catalogue == null) return;
        foreach (Transform child in stickersGridParent)
            Destroy(child.gameObject);

        if (catalogue.allStickers == null) return;
        foreach (var sticker in catalogue.allStickers)
        {
            if (sticker == null) continue;
            var coin = Instantiate(stickerCoinPrefab, stickersGridParent);
            int count = StickerManager.GetCount(sticker.rewardId);
            bool selected = StickerManager.SelectedStickerId == sticker.rewardId;
            coin.Bind(sticker, count, selected, OnTapSticker);
        }
    }

    // Tippen auf eine besessene Münze öffnet jetzt das Detail-/Verkaufs-Popup (Auswahl als
    // Porträt passiert von dort aus über den eigenen SELECT-Button, siehe
    // StickerDetailPopupController) statt direkt zu wählen.
    void OnTapSticker(RewardDefinition sticker)
    {
        if (!StickerManager.IsOwned(sticker.rewardId)) return;
        StickerDetailPopupController.Instance?.Open(sticker);
    }

    // ── Titel-Tab ────────────────────────────────────────────────────────────

    void PopulateTitles()
    {
        if (titlesGridParent == null || titleEntryPrefab == null || catalogue == null) return;
        foreach (Transform child in titlesGridParent)
            Destroy(child.gameObject);

        if (catalogue.allTitles == null) return;
        foreach (var title in catalogue.allTitles)
        {
            if (title == null) continue;
            var entry = Instantiate(titleEntryPrefab, titlesGridParent);
            bool owned = TitleManager.IsOwned(title.rewardId);
            bool selected = TitleManager.SelectedTitleId == title.rewardId;
            entry.Bind(title, owned, selected, OnSelectTitle);
        }
    }

    void OnSelectTitle(RewardDefinition title)
    {
        if (!TitleManager.IsOwned(title.rewardId)) return;
        TitleManager.SelectTitle(title.rewardId);

        // Nur den Auswahl-Zustand der bereits vorhandenen Zeilen aktualisieren (Rebind), statt
        // die komplette Liste zu zerstören und neu aufzubauen — sonst flackert beim Klick jede
        // einzelne Zeile kurz auf (inkl. neu gestarteter Pop-Animation auf der eigenen Zeile).
        // Reihenfolge der Kinder entspricht immer catalogue.allTitles, da PopulateTitles() genau
        // in dieser Reihenfolge instanziiert und GridLayoutGroup nicht umsortiert.
        if (titlesGridParent == null || catalogue == null || catalogue.allTitles == null) return;
        int i = 0;
        foreach (Transform child in titlesGridParent)
        {
            if (i >= catalogue.allTitles.Length) break;
            var titleDef = catalogue.allTitles[i];
            i++;
            if (titleDef == null) continue;

            var entry = child.GetComponent<TitleEntryUI>();
            if (entry == null) continue;
            bool owned = TitleManager.IsOwned(titleDef.rewardId);
            bool selected = TitleManager.SelectedTitleId == titleDef.rewardId;
            entry.Bind(titleDef, owned, selected, OnSelectTitle);
        }
    }

    // ── Animation ────────────────────────────────────────────────────────────

    IEnumerator Co_Open()
    {
        if (panel != null) { panel.gameObject.SetActive(true); panel.alpha = 0f; }
        var rt = panel != null ? panel.GetComponent<RectTransform>() : null;
        if (rt != null) rt.localScale = Vector3.one * 0.85f;

        float t = 0f;
        while (t < popInDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / popInDuration));
            if (panel != null) panel.alpha   = p;
            if (rt    != null) rt.localScale = Vector3.Lerp(Vector3.one * 0.85f, Vector3.one, p);
            yield return null;
        }
        if (panel != null) panel.alpha   = 1f;
        if (rt    != null) rt.localScale = Vector3.one;
    }

    IEnumerator Co_Close()
    {
        var rt = panel != null ? panel.GetComponent<RectTransform>() : null;

        float t = 0f;
        while (t < popOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / popOutDuration));
            if (panel != null) panel.alpha   = 1f - p;
            if (rt    != null) rt.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.85f, p);
            yield return null;
        }
        if (panel != null) panel.gameObject.SetActive(false);
    }
}
