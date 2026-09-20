using UnityEngine;

// Header und Tab Bar des Shops liegen in einem eigenen Canvas, das NÄHER an der Kamera steht
// (Plane Distance 3) als die 3D-Feen der Skin-Karten (z = -5, also Abstand 5). Nur dadurch liegen
// sie im Tiefentest vor den Figuren -- im normalen Shop-Canvas (Plane Distance 100) liegen sie weit
// dahinter und würden von den Figuren übermalt. So gleiten die Feen beim Scrollen sauber hinter den
// Header, statt darüber zu springen oder ausgeblendet werden zu müssen.
//
// Weil sie damit nicht mehr Kind des Shop-Panels sind, würden sie dessen Ein-/Ausblend-Animation
// (Alpha + Scale, siehe ShopController.Co_Open) nicht mehr mitmachen. Diese Komponente spiegelt den
// Zustand des Panels, damit die Animation unverändert bleibt.
[RequireComponent(typeof(CanvasGroup))]
public class ShopFrontLayer : MonoBehaviour
{
    [Tooltip("Die CanvasGroup des Shop-Panels -- dieselbe, die ShopController als 'panel' animiert. " +
             "Leer lassen: wird beim Start automatisch vom ShopController geholt.")]
    [SerializeField] CanvasGroup shopPanel;

    CanvasGroup _group;
    Canvas      _canvas;

    void Awake()
    {
        _group  = GetComponent<CanvasGroup>();
        _canvas = GetComponent<Canvas>();

        if (_canvas != null && _canvas.worldCamera == null) _canvas.worldCamera = Camera.main;

        if (shopPanel != null) return;

        // Der Shop ist beim Start deaktiviert, deshalb muss die Suche inaktive Objekte einschließen.
        var shop = FindAnyObjectByType<ShopController>(FindObjectsInactive.Include);
        if (shop != null) shopPanel = shop.PanelGroup;
    }

    void LateUpdate()
    {
        if (shopPanel == null) return;

        bool open = shopPanel.gameObject.activeInHierarchy;

        _group.alpha          = open ? shopPanel.alpha : 0f;
        _group.interactable   = open && shopPanel.interactable;
        _group.blocksRaycasts = open && shopPanel.blocksRaycasts;

        // Die Skalierung NICHT auf dem Canvas-Root setzen: Dort steuert der CanvasScaler die
        // Skalierung (scaleFactor), beide würden sich jeden Frame gegenseitig überschreiben und die
        // Öffnen-/Schließen-Animation würde zittern. Stattdessen die direkten Kinder skalieren.
        Vector3 scale = shopPanel.transform.localScale;
        for (int i = 0; i < transform.childCount; i++)
            transform.GetChild(i).localScale = scale;

        // Der Header-Hintergrund schreibt Tiefe (siehe Shader "UI/Depth Write"), damit die Outline
        // der Feen dahinter verschwindet. Bei alpha 0 wäre er zwar unsichtbar, würde aber weiter
        // Tiefe schreiben und andere Fenster an dieser Stelle beschneiden -- deshalb den Canvas bei
        // geschlossenem Shop ganz abschalten.
        if (_canvas != null) _canvas.enabled = open && shopPanel.alpha > 0f;
    }
}
