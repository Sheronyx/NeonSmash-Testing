using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class LeaderboardPager : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    [Header("Targets")]
    [SerializeField] LeaderboardPanelController panel;   // dein PanelController
    [SerializeField] TMP_Text title;                     // optional: Header-Titel

    [Header("Tabs")]
    [SerializeField] string[] ids    = { LeaderboardApi.TimeModeId, LeaderboardApi.InfinityId };
    [SerializeField] string[] titles = { "Time Mode", "Infinity Mode" };

    [Header("UX")]
    [SerializeField] float swipeThreshold = 70f;         // Pixel, ab wann wir swipen
    [SerializeField] bool rememberSelection = true;      // Auswahl merken (PlayerPrefs)

    const string PrefKey = "lb_tab_index";
    int index;
    float startX;

    void Awake()
    {
        index = rememberSelection ? PlayerPrefs.GetInt(PrefKey, 0) : 0;
    }

    void OnEnable() => Apply();

    // --- Swipe-Handing nur im Header-Bereich ---
    public void OnBeginDrag(PointerEventData e) => startX = e.position.x;
    public void OnDrag(PointerEventData e) { /* keine Live-Animation nötig */ }
    public void OnEndDrag(PointerEventData e)
    {
        float dx = e.position.x - startX;
        if (Mathf.Abs(dx) < swipeThreshold) return;

        index = Mathf.Clamp(index + (dx < 0 ? 1 : -1), 0, ids.Length - 1);
        Apply();
    }

    // --- Für Buttons ‹ / › optional ---
    public void Next() => SetIndex(index + 1);
    public void Prev() => SetIndex(index - 1);
    public void SetIndex(int i)
    {
        index = Mathf.Clamp(i, 0, ids.Length - 1);
        Apply();
    }

void Apply()
{
    if (title && index < titles.Length)
        title.text = titles[index];

    // nur noch mit 1 Parameter aufrufen
    panel.SetLeaderboard(ids[index]);

    if (rememberSelection)
    {
        PlayerPrefs.SetInt(PrefKey, index);
        PlayerPrefs.Save();
    }
}
}
