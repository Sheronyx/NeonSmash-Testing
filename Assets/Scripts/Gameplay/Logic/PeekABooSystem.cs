using System.Collections;
using UnityEngine;

// Peek-a-boo-Modus: Eine Wolke fliegt (auf-/abschwebend) in die Mitte. Pro Runde
// kommen ZWEI zufällige echte Elemente gleichzeitig oben UND unten heraus. Sie
// verhalten sich voll normal (Punkt / Leben verlieren / Puff), egal ob getroffen
// oder nicht — nichts fliegt zurück in die Wolke.
public class PeekABooSystem : MonoBehaviour
{
    public static PeekABooSystem Instance { get; private set; }
    public static bool IsActive { get; private set; }

    [Header("Setup")]
    [SerializeField] private MixedPointSpawner spawner;
    [SerializeField] private GameObject cloudPrefab;

    [Header("Sequenz")]
    [Tooltip("Anzahl Runden (jede Runde = 2 Elemente oben+unten).")]
    [SerializeField] private int roundCount = 15;
    [SerializeField] private float slideDuration = 0.2f;
    [Tooltip("Pause zwischen den Runden, damit die Verschwinde-Animationen ausklingen (~1s).")]
    [SerializeField] private float gapBetween = 1f;
    [Tooltip("Vertikaler Abstand der Elemente von der Wolkenmitte.")]
    [SerializeField] private float peekOffset = 2.5f;
    [Tooltip("Horizontaler Abstand (für die diagonalen Positionen bei 3/4 Elementen).")]
    [SerializeField] private float peekOffsetX = 1.8f;

    [Header("Score-Schwellen (Anzahl Elemente)")]
    [Tooltip("Ab diesem Score kommen 3 Elemente.")]
    [SerializeField] private int scoreForThree = 5000;
    [Tooltip("Ab diesem Score kommen 4 Elemente.")]
    [SerializeField] private int scoreForFour = 10000;

    [Header("Wolke")]
    [SerializeField] private float flyInDuration = 0.8f;
    [SerializeField] private float flyOutDuration = 0.7f;
    [SerializeField] private float bobAmplitude = 0.15f;
    [SerializeField] private float bobPeriod = 2f;

    [Header("Wolke Squash & Stretch (beim Rausschießen)")]
    [Tooltip("Wie stark die Wolke vor dem Rausschießen zusammengepresst wird (z.B. 0.8).")]
    [SerializeField] private float cloudSquash = 0.8f;
    [Tooltip("Wie weit die Wolke beim Lösen über die Normalgröße schnellt (z.B. 1.15).")]
    [SerializeField] private float cloudOvershoot = 1.15f;
    [Tooltip("Dauer jeder Phase (Pressen / Lösen / Beruhigen).")]
    [SerializeField] private float cloudPunchTime = 0.12f;

    private Transform cloud;
    private Vector3 cloudBase;
    private Vector3 cloudBaseScale = Vector3.one;
    private bool bobbing;
    private float bobStartTime;

    private void Awake()
    {
        Instance = this;
        // Statische Flag sicher zurücksetzen — falls eine vorherige Runde mitten in
        // der Peek-Sequenz per Szenenwechsel abgebrochen wurde (sonst bleibt sie
        // true und blockt Special Modes in der nächsten Runde).
        IsActive = false;
        if (spawner == null) spawner = FindFirstObjectByType<MixedPointSpawner>();
    }

    private void OnDestroy()
    {
        // Bei Szenenwechsel/Abbruch nicht mit gesetzter Flag zurücklassen.
        if (Instance == this) IsActive = false;
    }

    public void StartPeekABoo()
    {
        if (IsActive) return;
        StartCoroutine(Co_Run());
    }

    private IEnumerator Co_Run()
    {
        IsActive = true;
        spawner.PauseSpawning(true);

        Camera cam = Camera.main;
        float camZ = Mathf.Abs(cam.transform.position.z);
        Vector3 center   = ToWorld(cam, 0.5f,  0.5f, camZ);
        Vector3 offRight = ToWorld(cam, 1.3f,  0.5f, camZ);
        Vector3 offLeft  = ToWorld(cam, -0.3f, 0.5f, camZ);

        // Geskinntes Wolken-Prefab (Skin-Override) oder Default
        GameObject cloudToUse = SkinManager.Instance?.ActiveTheme?.peekCloudPrefab ?? cloudPrefab;
        cloud = Instantiate(cloudToUse, offRight, Quaternion.identity).transform;
        cloudBaseScale = cloud.localScale;
        cloudBase = offRight;
        bobStartTime = Time.time;
        bobbing = true;

        yield return MoveBase(offRight, center, flyInDuration);
        cloudBase = center;

        for (int i = 0; i < roundCount; i++)
        {
            yield return Co_OneRound();

            if (LivesManager.Instance != null && !LivesManager.Instance.HasLivesLeft)
            {
                spawner.TriggerGameOverFromGravity();
                Cleanup();
                yield break;
            }

            if (gapBetween > 0f) yield return new WaitForSeconds(gapBetween);
        }

        yield return MoveBase(center, offLeft, flyOutDuration);

        Cleanup();
        spawner.PauseSpawning(false);
        spawner.SpawnNextPoint();
    }

    private void Cleanup()
    {
        bobbing = false;
        if (cloud != null) Destroy(cloud.gameObject);
        IsActive = false;
    }

    private IEnumerator Co_OneRound()
    {
        // Reaktionsfenster = aktuelle Reaktionszeit im Spiel (dynamisch pro Level)
        float hold = spawner.CurrentReactionTime;

        // Anzahl + Positionen nach Punktestand
        int score = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;
        Vector3[] offsets = OffsetsForScore(score);
        int count = offsets.Length;

        // Genau EIN echtes Tap-Element, der Rest Fake (Position des Tap zufällig)
        int tapIndex = Random.Range(0, count);

        var elems = new PeekElement[count];
        for (int i = 0; i < count; i++)
            elems[i] = Spawn(i == tapIndex ? PeekType.Tap : PeekType.Fake, hold);

        // Koppeln: Klick auf eines löst ALLE aus
        for (int i = 0; i < count; i++)
            if (elems[i] != null) elems[i].SetGroup(elems);

        // Squash: Wolke vor dem Rausschießen zusammenpressen (Ausholen)
        yield return CloudScaleTo(cloudBaseScale * cloudSquash, cloudPunchTime);

        // Lösen (Overshoot → Normalgröße) parallel zum Rausschießen der Elemente
        StartCoroutine(Co_CloudRelease());
        yield return SlideGroup(elems, offsets, slideDuration);
        yield return HoldGroup(elems, offsets, hold);

        for (int i = 0; i < count; i++)
            if (elems[i] != null) elems[i].ResolveTimeout();
    }

    // Positionen (Offsets zur Wolkenmitte) je nach Punktestand:
    //  < scoreForThree → 2: oben, unten
    //  ≥ scoreForThree → 3: rechts oben, links oben, unten
    //  ≥ scoreForFour  → 4: rechts oben, links oben, links unten, rechts unten
    private Vector3[] OffsetsForScore(int score)
    {
        float x = peekOffsetX, y = peekOffset;

        if (score >= scoreForFour)
            return new[]
            {
                new Vector3( x,  y, 0f), new Vector3(-x,  y, 0f),
                new Vector3(-x, -y, 0f), new Vector3( x, -y, 0f),
            };

        if (score >= scoreForThree)
            return new[]
            {
                new Vector3( x,  y, 0f), new Vector3(-x,  y, 0f),
                new Vector3( 0f, -y, 0f),
            };

        return new[]
        {
            new Vector3(0f,  y, 0f), new Vector3(0f, -y, 0f),
        };
    }

    private PeekElement Spawn(PeekType type, float holdDuration)
    {
        GameObject prefab = PrefabFor(type);
        if (prefab == null) return null;

        var go = Instantiate(prefab, cloud.position, Quaternion.identity);

        var peek = go.AddComponent<PeekElement>();
        peek.Init(type, holdDuration);

        return peek;
    }

    private GameObject PrefabFor(PeekType type)
    {
        switch (type)
        {
            case PeekType.Tap:   return spawner.PeekTapPrefab;
            case PeekType.Shock: return spawner.PeekThunderPrefab;
            case PeekType.Fake:  return spawner.PeekFakePrefab;
        }
        return null;
    }

    private void Update()
    {
        if (!bobbing || cloud == null) return;
        float y = Mathf.Sin((Time.time - bobStartTime) * (Mathf.PI * 2f / bobPeriod)) * bobAmplitude;
        cloud.position = cloudBase + new Vector3(0f, y, 0f);
    }

    private Vector3 ToWorld(Camera cam, float vx, float vy, float z)
    {
        var p = cam.ViewportToWorldPoint(new Vector3(vx, vy, z));
        p.z = 0f;
        return p;
    }

    private IEnumerator MoveBase(Vector3 from, Vector3 to, float dur)
    {
        float e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            cloudBase = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, e / dur));
            yield return null;
        }
        cloudBase = to;
    }

    // Lösen: Wolke schnellt über die Normalgröße (Overshoot) und beruhigt sich.
    private IEnumerator Co_CloudRelease()
    {
        yield return CloudScaleTo(cloudBaseScale * cloudOvershoot, cloudPunchTime);
        yield return CloudScaleTo(cloudBaseScale, cloudPunchTime);
    }

    private IEnumerator CloudScaleTo(Vector3 target, float dur)
    {
        if (cloud == null) yield break;
        Vector3 from = cloud.localScale;
        float e = 0f;
        while (e < dur && cloud != null)
        {
            e += Time.deltaTime;
            cloud.localScale = Vector3.Lerp(from, target, Mathf.SmoothStep(0f, 1f, e / dur));
            yield return null;
        }
        if (cloud != null) cloud.localScale = target;
    }

    // Alle Elemente gleichzeitig von Offset 0 zu ihren Offsets (relativ zur Wolke).
    private IEnumerator SlideGroup(PeekElement[] elems, Vector3[] offs, float dur)
    {
        float e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, e / dur);
            for (int i = 0; i < elems.Length; i++)
                if (elems[i] != null) elems[i].transform.position = cloud.position + Vector3.Lerp(Vector3.zero, offs[i], k);
            yield return null;
        }
        for (int i = 0; i < elems.Length; i++)
            if (elems[i] != null) elems[i].transform.position = cloud.position + offs[i];
    }

    // Alle am Offset halten, folgen der bobbenden Wolke. Bricht früh ab, sobald
    // alle aufgelöst sind (Klick) → danach sorgt gapBetween für gleichmäßigen
    // Explosions-Puffer, unabhängig von der (bei hohem Score kurzen) Hold-Dauer.
    private IEnumerator HoldGroup(PeekElement[] elems, Vector3[] offs, float dur)
    {
        float e = 0f;
        while (e < dur && !AllResolved(elems))
        {
            e += Time.deltaTime;
            for (int i = 0; i < elems.Length; i++)
                if (elems[i] != null) elems[i].transform.position = cloud.position + offs[i];
            yield return null;
        }
    }

    private bool AllResolved(PeekElement[] elems)
    {
        foreach (var e in elems)
            if (e != null && !e.Resolved) return false;
        return true;
    }
}
