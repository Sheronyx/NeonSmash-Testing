using UnityEngine;

/// Sanfter Schwebe-Effekt für World-Space Objekte (Sprites, GameObjects).
/// Funktioniert mit Transform.localPosition — kein Canvas nötig.
public class WorldFloatBob : MonoBehaviour
{
    [SerializeField] private float amplitude  = 0.15f;  // World Units auf/ab
    [SerializeField] private float frequency  = 0.4f;   // Zyklen/Sekunde
    [SerializeField] private bool  randomPhase = true;

    private Vector3 basePos;
    private float   phase;

    void Awake()
    {
        basePos = transform.localPosition;
        phase   = randomPhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;
    }

    void OnEnable()
    {
        transform.localPosition = basePos;
    }

    void Update()
    {
        float offY = Mathf.Sin(Time.time * frequency * Mathf.PI * 2f + phase) * amplitude;
        transform.localPosition = basePos + new Vector3(0f, offY, 0f);
    }

    /// BoostSystem ruft das jeden Frame auf um basePos nach oben zu verschieben,
    /// ohne die Schwebephase zu stören.
    public void AddToBase(Vector3 delta) { basePos += delta; }

    public void RecalibrateBase()
    {
        basePos = transform.localPosition;
        // Reset phase so sin = 0 at this moment → no jump on first Update
        phase = -(Time.time * frequency * Mathf.PI * 2f);
    }
}
