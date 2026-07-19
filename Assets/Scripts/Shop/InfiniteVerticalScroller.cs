using UnityEngine;

// Lässt das Sternen-Muster endlos langsam von unten nach oben durchlaufen. Erzeugt beim Start
// eine Laufzeit-Kopie des kompletten Musters (inkl. aller Kind-Bilder), damit beim Umbruch immer
// eine zweite Kachel den sichtbaren Bereich abdeckt — dadurch entsteht kein sichtbarer Sprung,
// unabhängig vom genauen Sternenmuster. Setzt voraus, dass dieses GameObject bereits die komplette
// sichtbare Höhe des Hintergrunds abdeckt (wie aktuell im Shop der Fall).
[RequireComponent(typeof(RectTransform))]
public class InfiniteVerticalScroller : MonoBehaviour
{
    [Tooltip("Scrollgeschwindigkeit in Pixel/Sekunde (nach oben).")]
    [SerializeField] private float scrollSpeed = 15f;

    // Instantiate() kopiert das GameObject INKLUSIVE dieses Scripts und ruft Awake() der Kopie
    // synchron noch während des Instantiate()-Aufrufs auf — ein Destroy() danach kommt zu spät
    // (wirkt erst am Frame-Ende), die Kopie würde also selbst sofort wieder eine Kopie erzeugen
    // und das endlos rekursiv (Stack Overflow/Crash). Dieses statische Flag ist während des
    // Instantiate()-Aufrufs gesetzt und lässt die Kopie sich in ihrer eigenen Awake() sofort
    // selbst deaktivieren, statt weiterzuklonen.
    private static bool isCreatingClone = false;

    private RectTransform[] tiles;
    private Vector2 basePos;
    private float tileHeight;
    private float offset;

    private void Awake()
    {
        if (isCreatingClone)
        {
            enabled = false;
            return;
        }

        var original = (RectTransform)transform;
        tileHeight = original.rect.height;
        basePos = original.anchoredPosition;

        isCreatingClone = true;
        GameObject cloneGO;
        try
        {
            cloneGO = Instantiate(gameObject, transform.parent);
        }
        finally
        {
            isCreatingClone = false;
        }
        cloneGO.name = name + " (Scroll Clone)";
        // Instantiate() hängt die Kopie standardmäßig als LETZTES Kind an (rendert dadurch über
        // allem, z.B. über Grid Content). Direkt hinter das Original einsortieren, damit beide
        // Kacheln weiterhin hinter dem restlichen Inhalt liegen.
        cloneGO.transform.SetSiblingIndex(original.GetSiblingIndex() + 1);
        Destroy(cloneGO.GetComponent<InfiniteVerticalScroller>());

        tiles = new[] { original, (RectTransform)cloneGO.transform };
    }

    private void Update()
    {
        if (tiles == null) return;

        offset += scrollSpeed * Time.deltaTime;

        for (int i = 0; i < tiles.Length; i++)
        {
            float y = Mathf.Repeat(offset + i * tileHeight, tileHeight * 2f) - tileHeight;
            tiles[i].anchoredPosition = basePos + new Vector2(0f, y);
        }
    }
}
