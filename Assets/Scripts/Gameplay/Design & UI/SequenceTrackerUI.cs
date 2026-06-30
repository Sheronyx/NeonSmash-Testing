using System;
using System.Collections;
using UnityEngine;
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
        [Tooltip("Fortschrittspunkte von links nach rechts")]
        public Image[] dots;
    }

    [Header("Slots (einer pro aktiver Sequenz)")]
    [SerializeField] private SlotUI[] slots;

    [Header("Next-Element Prefabs")]
    [SerializeField] private GameObject prefabOrange;   // PointColor.Red
    [SerializeField] private GameObject prefabGreen;    // PointColor.Green
    [SerializeField] private GameObject prefabPurple;   // PointColor.Purple

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
    }

    private GameObject PrefabForColor(PointColor pc) => pc switch
    {
        PointColor.Red    => prefabOrange,
        PointColor.Green  => prefabGreen,
        PointColor.Purple => prefabPurple,
        _                 => null
    };

    private void OnDestroy()
    {
        foreach (var go in _nextPrefabs)
            if (go != null) Destroy(go);
    }
}
