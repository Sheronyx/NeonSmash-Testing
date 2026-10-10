using UnityEngine;

// Zuendet einen Partikeleffekt an der Faust der Fee, genau in dem Frame, in dem sie nach ihrem
// Aufstieg im Special-Mode wieder aufkommt. Ausgeloest per Animation Event aus dem Clip
// SpecialMode_Fly (siehe dessen Import-Einstellungen, Reiter "Animation" -> Events).
//
// WICHTIG: Animation Events rufen Methoden auf Komponenten DES OBJEKTS auf, das den Animator
// traegt. Dieses Skript gehoert deshalb neben den Animator und nicht an den Effekt oder den Anker.
public class FairyImpactEffect : MonoBehaviour
{
    [Tooltip("Der Partikeleffekt, der beim Aufprall gezuendet wird. Spielt von selbst los " +
             "(Play On Awake) und wird nach lifetime wieder entfernt.")]
    [SerializeField] private GameObject effectPrefab;

    [Tooltip("Leer lassen: wird ueber den Namen unter dieser Fee gesucht.")]
    [SerializeField] private Transform anchor;

    [Tooltip("Name des Ankers an der Faust.")]
    [SerializeField] private string anchorName = "ImpactAnchor_RightHand";

    [Tooltip("Groesse des Effekts. Das Hovl-Prefab ist mit Startgroesse 8 fuer grosse Szenen " +
             "gebaut und wuerde die 1,7 m hohe Fee sonst voellig zudecken.")]
    [SerializeField] private float effectScale = 0.2f;

    [Tooltip("Sekunden, bis der erzeugte Effekt wieder zerstoert wird. Das Prefab laeuft 3,5 s.")]
    [SerializeField] private float lifetime = 4f;

    [Tooltip("Effekt an die Faust heften statt ihn am Aufprallort stehen zu lassen. Normalerweise " +
             "aus: eine Einschlagwolke soll liegen bleiben und nicht mit der Hand weiterwandern.")]
    [SerializeField] private bool parentToAnchor = false;

    private Transform _anchor;
    private bool _anchorGesucht;

    // Wird vom Animation Event aufgerufen. Name darf sich nicht aendern, ohne das Event mitzuziehen.
    public void PlayFistImpact()
    {
        var ziel = Anchor();
        if (effectPrefab == null)
        {
            Debug.LogWarning($"[FairyImpactEffect] Kein effectPrefab gesetzt auf '{name}'.", this);
            return;
        }
        if (ziel == null)
        {
            Debug.LogWarning($"[FairyImpactEffect] Kein '{anchorName}' unter '{name}' gefunden.", this);
            return;
        }

        var go = Instantiate(effectPrefab, ziel.position, ziel.rotation,
                             parentToAnchor ? ziel : null);

        // Der Anker haengt tief in der Knochenhierarchie, deren Weltskalierung beim Mixamo-Rig
        // das 130-fache betraegt. Beim Anhaengen muss sie herausgerechnet werden, sonst waere der
        // Effekt um genau diesen Faktor zu gross. Ohne Elternobjekt entfaellt das.
        float kette = parentToAnchor ? Mathf.Abs(ziel.lossyScale.x) : 1f;
        go.transform.localScale = Vector3.one * (effectScale / Mathf.Max(0.0001f, kette));

        if (lifetime > 0f) Destroy(go, lifetime);
    }

    private Transform Anchor()
    {
        if (anchor != null) return anchor;
        if (_anchorGesucht && _anchor != null) return _anchor;
        _anchorGesucht = true;

        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == anchorName) { _anchor = t; return _anchor; }
        return null;
    }
}
