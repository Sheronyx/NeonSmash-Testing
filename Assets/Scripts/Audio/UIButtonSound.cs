using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Spielt beim Antippen eines Knopfes den passenden UI-Klang.
///
/// Normalfall ist <see cref="Art"/>: der Klang kommt dann zentral aus <see cref="UIAudio"/> und
/// muss nicht an jedem Knopf einzeln gepflegt werden. Ein eigener Clip in
/// <see cref="clickSound"/> hat trotzdem Vorrang -- fuer Knoepfe, die bewusst anders klingen.
/// </summary>
[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour
{
    public enum Klangart
    {
        /// <summary>Oeffnet ein Fenster oder wechselt den Tab.</summary>
        OeffnenWechseln,
        /// <summary>Schliesst ein Fenster.</summary>
        Schliessen,
        /// <summary>Gar kein Klang -- fuer Knoepfe, die ihren Ton woanders ausloesen.</summary>
        Stumm
    }

    [Tooltip("Welcher der beiden Standard-Klaenge gespielt wird.")]
    [SerializeField] private Klangart art = Klangart.OeffnenWechseln;

    [Tooltip("Optional: eigener Clip. Hat Vorrang vor der Klangart.")]
    [SerializeField] private AudioClip clickSound;

    private Button btn;

    void Awake()
    {
        btn = GetComponent<Button>();
        btn.onClick.AddListener(PlayClickSound);
    }

    public void PlayClickSound()
    {
        if (UIAudio.Instance == null) return;

        if (clickSound != null) { UIAudio.Instance.PlayOneShot(clickSound); return; }

        switch (art)
        {
            case Klangart.OeffnenWechseln: UIAudio.Instance.PlayOpenSwitch(); break;
            case Klangart.Schliessen:      UIAudio.Instance.PlayClose();      break;
        }
    }
}
