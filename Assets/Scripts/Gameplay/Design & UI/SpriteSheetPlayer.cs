using UnityEngine;

// Spielt eine Frame-Sequenz auf einem SpriteRenderer ab — z.B. den Papagei-Flügelschlag
// (8 Frames). Leichtgewichtig (kein Animator nötig), data-driven, mit Ping-Pong für
// nahtlose Schleifen und optionalem kurzem "Flap-Boost" (schnellerer Schlag beim
// Element-Rauswerfen).
//
// Frames in der Reihenfolge der Flügelhöhe zuweisen (gefaltet → offen → gefaltet).
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteSheetPlayer : MonoBehaviour
{
    [Header("Frames (nach Flügelhöhe sortiert)")]
    [SerializeField] private Sprite[] frames;

    [Header("Tempo")]
    [Tooltip("Bilder pro Sekunde. ~10–14 wirkt natürlich.")]
    [SerializeField] private float fps = 12f;
    [Tooltip("Vor- UND rückwärts abspielen (1→N→1) → nahtlose, doppelt so flüssige Schleife.")]
    [SerializeField] private bool pingPong = true;
    [SerializeField] private bool useUnscaledTime = false;

    private SpriteRenderer sr;
    private float timer;
    private int index;
    private int dir = 1;
    private float fpsMultiplier = 1f;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (frames != null && frames.Length > 0) sr.sprite = frames[0];
    }

    private void Update()
    {
        if (frames == null || frames.Length < 2) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        timer += dt * fps * fpsMultiplier;

        if (timer < 1f) return;
        timer -= 1f;

        if (pingPong)
        {
            index += dir;
            if (index >= frames.Length - 1) { index = frames.Length - 1; dir = -1; }
            else if (index <= 0)            { index = 0;                  dir =  1; }
        }
        else
        {
            index = (index + 1) % frames.Length;
        }

        sr.sprite = frames[index];
    }

    // Vom PeekABooSystem aufrufbar: kurz schneller schlagen (z.B. beim Rauswerfen).
    public void SetFlapBoost(float multiplier) => fpsMultiplier = Mathf.Max(0.1f, multiplier);
    public void ResetFlapSpeed() => fpsMultiplier = 1f;
}
