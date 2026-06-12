using UnityEngine;

// Schaltet zwischen bereits in der Szene platzierten Objekten je nach aktivem
// Skin. Kein Instanziieren -> keine Scale-/Positions-Probleme.
//
// - defaultObject bleibt an, solange kein passender Skin aktiv ist.
// - Pro Skin ein eigenes Szenen-Objekt, das nur aktiviert wird, wenn dessen
//   Theme das aktive ist.
//
// Einsetzbar für Background, UI Top Bar usw. Auf ein beliebiges Szenen-Objekt
// legen und die Referenzen zuweisen.
public class SkinObjectSwap : MonoBehaviour
{
    [System.Serializable]
    public struct Entry
    {
        public SkinTheme  theme;
        public GameObject obj;
    }

    [Tooltip("Standard-Objekt (an, wenn kein Skin-Theme passt).")]
    [SerializeField] private GameObject defaultObject;

    [Tooltip("Pro Skin: Theme + zugehöriges Szenen-Objekt.")]
    [SerializeField] private Entry[] skinObjects;

    void Start()
    {
        Apply();
    }

    public void Apply()
    {
        var active = SkinManager.Instance != null ? SkinManager.Instance.ActiveTheme : null;
        GameObject chosen = null;

        foreach (var e in skinObjects)
        {
            bool match = active != null && e.theme == active;
            if (e.obj != null) e.obj.SetActive(match);
            if (match) chosen = e.obj;
        }

        if (defaultObject != null) defaultObject.SetActive(chosen == null);
    }
}
