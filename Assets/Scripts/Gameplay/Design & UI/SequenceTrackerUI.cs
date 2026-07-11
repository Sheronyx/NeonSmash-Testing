using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class SequenceTrackerUI : MonoBehaviour
{
    [Serializable]
    public class SlotUI
    {
        [Tooltip("Icon das die Fähigkeit/Belohnung der Sequenz zeigt")]
        public Image abilityIconImage;
        [Tooltip("Weltkoordinaten-Ankerpunkt für das nächste Element-Visual")]
        public Transform nextElementAnchor;
        [Tooltip("Alle Dots: [0-5] = Reihe 1, [6-11] = Reihe 2 (max. 12)")]
        public Image[] dots;
        [Tooltip("GameObject der zweiten Dot-Reihe — wird ausgeblendet wenn Sequenz ≤ 6 Elemente hat")]
        public GameObject dotsRow2;
    }

    [Header("Slots (einer pro aktiver Sequenz)")]
    [SerializeField] private SlotUI[] slots;

    [Header("Slide-Animation")]
    [SerializeField] private UISlideAnimator slideAnimator;

    [Header("Next-Element Prefabs")]
    [SerializeField] private GameObject prefabOrange;   // PointColor.Pink
    [SerializeField] private GameObject prefabGreen;    // PointColor.Green
    [FormerlySerializedAs("prefabPurple")]
    [SerializeField] private GameObject prefabBlue;     // PointColor.Blue

    [Header("Dot-Farben")]
    [SerializeField] private Color dotFilled = new Color(1f, 1f, 1f, 1.00f);
    [SerializeField] private Color dotEmpty  = new Color(1f, 1f, 1f, 0.20f);
    [SerializeField] private Material dotMaterial;

    private GameObject[] _nextPrefabs;

    private void Awake()
    {
        _nextPrefabs = new GameObject[slots != null ? slots.Length : 0];
    }

    private void OnEnable()
    {
        SequenceManager.OnProgressChanged   += HandleProgressChanged;
        SequenceManager.OnSequenceCompleted += HandleSequenceCompleted;
        SequenceManager.OnAllProgressReset  += HandleAllReset;
    }

    private void OnDisable()
    {
        SequenceManager.OnProgressChanged   -= HandleProgressChanged;
        SequenceManager.OnSequenceCompleted -= HandleSequenceCompleted;
        SequenceManager.OnAllProgressReset  -= HandleAllReset;
    }

    private void HandleRowHide()
    {
        slideAnimator?.SlideOutToLeft();
        SetNextPrefabsActive(false);
    }

    private void HandleRowShow()
    {
        slideAnimator?.SlideInFromLeft(() => SetNextPrefabsActive(true));
    }

    private void SetNextPrefabsActive(bool active)
    {
        if (_nextPrefabs == null) return;
        foreach (var p in _nextPrefabs)
            if (p != null) p.SetActive(active);
    }

    private void Start() => StartCoroutine(Co_RefreshAfterLayout());

    private IEnumerator Co_RefreshAfterLayout()
    {
        yield return null; // einen Frame warten bis Canvas-Layout fertig
        ApplyDotMaterial();
        RefreshAll();
    }

    private void ApplyDotMaterial()
    {
        if (dotMaterial == null || slots == null) return;
        foreach (var slot in slots)
        {
            if (slot?.dots == null) continue;
            foreach (var dot in slot.dots)
                if (dot != null) dot.material = dotMaterial;
        }
    }

    private void HandleProgressChanged(int index, int step) => RefreshSlot(index);
    private void HandleSequenceCompleted(int index)         => RefreshSlot(index);
    private void HandleAllReset()                           => RefreshAll();

    private void RefreshAll()
    {
        for (int i = 0; i < slots.Length; i++) RefreshSlot(i);
    }

    private void RefreshSlot(int index)
    {
        if (slots == null || index >= slots.Length) return;
        var slot = slots[index];
        if (slot == null) return;

        var sm       = SequenceManager.Instance;
        var seq      = sm != null ? sm.GetSequence(index) : null;
        int progress = sm != null ? sm.GetProgress(index) : 0;

        if (slot.abilityIconImage != null && seq != null)
            slot.abilityIconImage.sprite = seq.abilityIcon;

        // Next-Element Visual austauschen
        if (slot.nextElementAnchor != null)
        {
            if (_nextPrefabs[index] != null) Destroy(_nextPrefabs[index]);
            _nextPrefabs[index] = null;

            if (seq != null && seq.steps != null && seq.steps.Length > 0 && progress < seq.steps.Length)
            {
                var prefab = PrefabForColor(seq.steps[progress]);
                if (prefab != null)
                    _nextPrefabs[index] = Instantiate(prefab, slot.nextElementAnchor.position, Quaternion.identity);
            }
        }

        if (seq == null || seq.steps == null || seq.steps.Length == 0)
        {
            SetAllDots(slot, 0, 0);
            return;
        }

        SetAllDots(slot, progress, seq.steps.Length);
    }

    private void SetAllDots(SlotUI slot, int filledCount, int totalSteps)
    {
        if (slot.dots == null) return;
        for (int d = 0; d < slot.dots.Length; d++)
        {
            if (slot.dots[d] == null) continue;
            if (d >= totalSteps)
            {
                slot.dots[d].gameObject.SetActive(false);
                continue;
            }
            slot.dots[d].gameObject.SetActive(true);
            slot.dots[d].color = d < filledCount ? dotFilled : dotEmpty;
        }
        if (slot.dotsRow2 != null)
            slot.dotsRow2.SetActive(totalSteps > 6);
    }

    private GameObject PrefabForColor(PointColor pc) => pc switch
    {
        PointColor.Pink   => prefabOrange,
        PointColor.Green  => prefabGreen,
        PointColor.Blue   => prefabBlue,
        _                 => null
    };

    private void OnDestroy()
    {
        foreach (var go in _nextPrefabs)
            if (go != null) Destroy(go);
    }

    [ContextMenu("Auto-Configure Slots")]
    private void AutoConfigureSlots()
    {
        int slotCount = 0;
        while (transform.Find($"Slot {slotCount + 1}") != null) slotCount++;

        slots = new SlotUI[slotCount];
        for (int i = 0; i < slotCount; i++) slots[i] = new SlotUI();

        for (int i = 0; i < slots.Length; i++)
        {
            var slot     = slots[i];
            var slotRoot = transform.Find($"Slot {i + 1}");
            if (slotRoot == null) continue;

            var iconFrame = slotRoot.Find("Ability Icon Frame");
            slot.abilityIconImage = iconFrame?.GetComponentInChildren<Image>();
            slot.nextElementAnchor = slotRoot.Find("Next Element");

            var dotsBar = slotRoot.Find("Dots Row Progress Bar");
            if (dotsBar != null)
            {
                var row2Go    = dotsBar.Find("Dots Row 2");
                slot.dotsRow2 = row2Go?.gameObject;

                var allDots = new System.Collections.Generic.List<Image>();
                var row1    = dotsBar.Find("Dots Row 1");
                if (row1 != null)
                    foreach (Transform child in row1)
                    {
                        var img = child.GetComponent<Image>();
                        if (img != null) allDots.Add(img);
                    }
                if (row2Go != null)
                    foreach (Transform child in row2Go)
                    {
                        var img = child.GetComponent<Image>();
                        if (img != null) allDots.Add(img);
                    }
                slot.dots = allDots.ToArray();
            }
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log($"[SequenceTrackerUI] {slots.Length} Slots automatisch konfiguriert.");
    }
}
