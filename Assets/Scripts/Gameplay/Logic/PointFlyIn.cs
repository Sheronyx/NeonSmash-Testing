using System;
using System.Collections;
using UnityEngine;

// Lässt ein frisch instanziiertes Element von außerhalb des Bildschirmrands einfliegen: startet
// winzig an einem zufälligen Punkt knapp außerhalb eines der 4 Ränder, fliegt über eine zufällige
// Bogenkurve zur Zielposition (quadratische Bezier, wie die Activation-Orb-Zeremonien) UND wächst
// dabei in EINER durchgehenden Bewegung auf Originalgröße (Skalierung per Ease-Out-Back — überschwingt von selbst leicht und
// landet exakt bei Zielgröße, keine separate Pop-In-Phase mehr nötig). Danach onArrived-Callback —
// der Spawner startet dort Timer/Fuse/Pulse. Collider bleibt während der gesamten Bewegung
// deaktiviert, damit ein noch fliegendes Element nicht antippbar ist.
public class PointFlyIn : MonoBehaviour
{
    [Header("Einflug (aus dem Hintergrund, eine durchgehende Bewegung)")]
    [SerializeField] private float duration          = 0.38f;
    [Tooltip("Startgröße relativ zur Zielgröße — je kleiner, desto stärker wirkt \"kommt aus der Ferne\".")]
    [SerializeField] private float startScalePercent = 0.05f;
    [Tooltip("Wie weit außerhalb des Bildschirmrands der Startpunkt liegt (Viewport-Einheiten, 0 = genau am Rand).")]
    [SerializeField] private float offScreenMargin = 0.2f;
    [Tooltip("Wie stark die Flugkurve seitlich ausbeult (zufällige Richtung pro Element).")]
    [SerializeField] private float curveStrengthMin  = 0.4f;
    [SerializeField] private float curveStrengthMax  = 1.1f;
    [Tooltip("Stärke des Scale-Überschwingens am Ende der Bewegung (Standard-Ease-Out-Back ≈ 1.7).")]
    [SerializeField] private float scaleOvershoot    = 1.70158f;
    [Tooltip("Ab welchem Anteil der Flugzeit das schnelle Wachstum mit Überschwingen einsetzt (0.8 = letzte 20%).")]
    [Range(0.1f, 0.95f)]
    [SerializeField] private float fastScalePhaseStart = 0.8f;
    [Tooltip("Wie viel des Weges zur Zielgröße bereits VOR dem schnellen Wachstum erreicht ist (langsame Phase).")]
    [Range(0f, 0.9f)]
    [SerializeField] private float slowScalePhaseFraction = 0.25f;

    private Collider2D col;

    private void Awake() => col = GetComponent<Collider2D>();

    // targetPosition/targetScale = die vom Spawner bereits berechnete finale Spawn-Pose.
    public void Play(Vector3 targetPosition, Vector3 targetScale, Action onArrived)
    {
        // SpawnPulse sitzt meist auf einem Kind-Objekt ("Visual") und startet sich selbst in
        // seinem eigenen OnEnable() — bereits VOR diesem Play()-Aufruf gelaufen, noch während
        // Instantiate(). Es animiert dieselbe localScale parallel zu uns und würde unsere Kurve
        // überschreiben. Cancel() (bereits vorhanden, z.B. fürs Peek-a-boo genutzt) stoppt es
        // sauber und setzt seinen eigenen Endzustand her, bevor wir unsere eigene Startgröße setzen.
        GetComponentInChildren<SpawnPulse>()?.Cancel();

        if (col != null) col.enabled = false;

        Vector3 startPos   = OffScreenPos();
        Vector3 startScale = targetScale * startScalePercent;
        Vector3 ctrl        = CurveControl(startPos, targetPosition);

        transform.position   = startPos;
        transform.localScale = startScale;

        StartCoroutine(Co_Fly(startPos, ctrl, startScale, targetPosition, targetScale, onArrived));
    }

    private IEnumerator Co_Fly(Vector3 startPos, Vector3 ctrl, Vector3 startScale,
                                Vector3 targetPos, Vector3 targetScale, Action onArrived)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);

            // Position: quadratische Bezier-Kurve (Bogen statt Gerade) mit sanftem Ease-Out-Timing.
            float posK = Mathf.SmoothStep(0f, 1f, k);
            float u    = 1f - posK;
            transform.position = u * u * startPos + 2f * u * posK * ctrl + posK * posK * targetPos;

            // Scale: erste fastScalePhaseStart (Standard 80%) der Flugzeit nur langsames Wachstum
            // bis slowScalePhaseFraction (Standard 25%) des Weges, danach schnelles Wachstum mit
            // Überschwingen bis Zielgröße in den letzten 20%.
            float scaleK = ScaleCurve(k);
            transform.localScale = Vector3.LerpUnclamped(startScale, targetScale, scaleK);

            yield return null;
        }

        transform.position   = targetPos;
        transform.localScale = targetScale;

        // SwipePoint cached seinen Hit-Radius in Start() basierend auf lossyScale — die kleine
        // Einflug-Startgröße darf sich NICHT dauerhaft als Trefferzone einbrennen, deshalb hier
        // (nach Erreichen der Zielgröße) neu berechnen.
        GetComponent<SwipePoint>()?.RefreshEffectiveRadius();

        // Collider erst JETZT aktivieren: onArrived setzt gleich erst spawner/CurrentSwipePoint
        // in FinishSlotSpawn — vorher wäre ein Tap technisch schon physisch möglich, würde aber
        // wegen spawner == null lautlos verschluckt.
        if (col != null) col.enabled = true;

        onArrived?.Invoke();
    }

    // Zufälliger Punkt knapp außerhalb eines der 4 Bildschirmränder — analog zu
    // GravityModeActivationPoint.OffScreenPos.
    private Vector3 OffScreenPos()
    {
        Camera cam = Camera.main;
        if (cam == null) return transform.position;

        float camZ = Mathf.Abs(cam.transform.position.z);
        int   edge = UnityEngine.Random.Range(0, 4);
        float u    = UnityEngine.Random.Range(0.1f, 0.9f);
        Vector2 vp = edge switch
        {
            0 => new Vector2(u, 1f + offScreenMargin),
            1 => new Vector2(1f + offScreenMargin, u),
            2 => new Vector2(u, -offScreenMargin),
            _ => new Vector2(-offScreenMargin, u),
        };
        Vector3 p = cam.ViewportToWorldPoint(new Vector3(vp.x, vp.y, camZ));
        p.z = 0f;
        return p;
    }

    // Kontrollpunkt für die Bezier-Kurve: Mittelpunkt der Strecke, seitlich versetzt in zufälliger
    // Richtung/Stärke — analog zu GravityModeActivationPoint.CurveControl.
    private Vector3 CurveControl(Vector3 from, Vector3 to)
    {
        Vector3 mid  = (from + to) * 0.5f;
        Vector3 dir  = (to - from).normalized;
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
        float strength = UnityEngine.Random.Range(curveStrengthMin, curveStrengthMax);
        return mid + perp * strength * (UnityEngine.Random.value > 0.5f ? 1f : -1f);
    }

    // Zweiphasige Scale-Kurve: langsames Wachstum bis fastScalePhaseStart (nur bis
    // slowScalePhaseFraction des Weges), danach schnelles Wachstum mit Überschwingen bis 1.
    private float ScaleCurve(float k)
    {
        if (k < fastScalePhaseStart)
        {
            float local = k / fastScalePhaseStart;
            return Mathf.SmoothStep(0f, slowScalePhaseFraction, local);
        }
        else
        {
            float local = (k - fastScalePhaseStart) / (1f - fastScalePhaseStart);
            float eased = EaseOutBack(local, scaleOvershoot);
            return Mathf.LerpUnclamped(slowScalePhaseFraction, 1f, eased);
        }
    }

    // Robert-Penner-Style Ease-Out-Back: 0→1, überschwingt kurz über 1 hinaus, landet exakt bei 1.
    private static float EaseOutBack(float t, float overshoot)
    {
        t -= 1f;
        return t * t * ((overshoot + 1f) * t + overshoot) + 1f;
    }
}
