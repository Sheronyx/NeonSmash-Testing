using System;
using System.Collections;
using UnityEngine;

// Ersetzt den Zahlen-Countdown vorm Spielstart: die drei Feen fliegen vom (unsichtbaren) Portal-
// Anker zu ihrer Ruheposition in der Spielszene — im selben Bogen-/Timing-Stil wie beim Reinflug
// ins Portal im Hauptmenü (siehe PlayIntroSequence). Sobald alle angekommen sind, startet das
// eigentliche Spiel (siehe GameStartCoordinator.StartCountdown).
public class FairyArrivalSequence : MonoBehaviour
{
    public static FairyArrivalSequence Instance { get; private set; }

    [Header("Feen (fliegen vom Portal-Anker zu ihrer Ruheposition)")]
    [SerializeField] private Transform[] fairies;
    [SerializeField] private float fairyFlightDuration = 1f;
    [Tooltip("Versatz zwischen dem Start der einzelnen Feen-Flüge — 0 = alle gleichzeitig los.")]
    [SerializeField] private float fairyStagger = 0.12f;
    [Tooltip("Wie stark der Flugweg von der geraden Linie abweicht (Bogen statt starrer Linie).")]
    [SerializeField] private float fairyCurveStrength = 1.2f;
    [Tooltip("Wie viel schneller die Flügel während des Flugs schlagen (sanft hochgeeast über " +
             "FairyWingFlap.SetSpeedBoost, kein abrupter Sprung).")]
    [SerializeField] private float fairyFlapSpeedBoost = 1.6f;

    private Vector3[] _homePos;
    private Vector3[] _homeScale;

    private void Awake()
    {
        Instance = this;

        _homePos   = new Vector3[fairies.Length];
        _homeScale = new Vector3[fairies.Length];

        // Sofort unsichtbar machen (Ruheposition/-Größe vorher merken) — Skalierung 0 reicht dafür
        // aus, unabhängig von der Position. Die Position selbst wird erst in Co_Play auf den Portal-
        // Anker gesetzt: PortalAnchor.Instance ist zu diesem Zeitpunkt (Awake) noch nicht zuverlässig
        // gesetzt, falls dessen eigenes Awake noch nicht gelaufen ist — Play() wird dagegen erst von
        // GameStartCoordinator.Start() aufgerufen, also sicher NACH allen Awakes der Szene.
        for (int i = 0; i < fairies.Length; i++)
        {
            var fairy = fairies[i];
            if (fairy == null) continue;

            _homePos[i]   = fairy.position;
            _homeScale[i] = fairy.localScale;
            fairy.localScale = Vector3.zero;

            var col = fairy.GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
            var fairyFloat = fairy.GetComponent<FairyFloat>();
            if (fairyFloat != null) fairyFloat.enabled = false;
        }
    }

    /// <summary>Startet die Ankunfts-Sequenz; ruft onComplete auf, sobald alle Feen angekommen sind.</summary>
    public void Play(Action onComplete)
    {
        StartCoroutine(Co_Play(onComplete));
    }

    private IEnumerator Co_Play(Action onComplete)
    {
        Vector3 anchorPos = PortalAnchor.Instance != null ? PortalAnchor.Instance.transform.position : transform.position;

        // Erst jetzt (sicher nach allen Awakes) an den Anker teleportieren — unsichtbar (Scale 0)
        // seit Awake, macht also nichts sichtbar springen.
        foreach (var fairy in fairies)
            if (fairy != null) fairy.position = anchorPos;

        int remaining = 0;
        for (int i = 0; i < fairies.Length; i++)
        {
            var fairy = fairies[i];
            if (fairy == null) continue;
            remaining++;

            int   ci     = i;
            Vector3 toPos   = _homePos[ci];
            Vector3 toScale = _homeScale[ci];
            StartCoroutine(Co_SingleFairyArrive(fairy, anchorPos, toPos, toScale, () => remaining--));

            // Nach der letzten Fee nicht mehr unnötig warten (gleiches Prinzip wie PlayIntroSequence).
            bool isLast = i == fairies.Length - 1;
            if (isLast) continue;

            float staggerT = 0f;
            while (staggerT < fairyStagger)
            {
                staggerT += Time.deltaTime;
                yield return null;
            }
        }

        while (remaining > 0)
            yield return null;

        onComplete?.Invoke();
    }

    private IEnumerator Co_SingleFairyArrive(Transform fairy, Vector3 fromPos, Vector3 toPos, Vector3 toScale, Action onDone)
    {
        // ReleasePoseSmoothly blendet sanft aus einer evtl. hängenden gehaltenen Flügel-Pose in den
        // automatischen Takt über (kein Sprung, egal in welchem Zustand die Flügel gerade waren),
        // SetSpeedBoost fährt den Flügelschlag zusätzlich sanft hoch — gleiches Prinzip wie beim
        // Reinflug ins Portal.
        var wingFlap = fairy.GetComponent<FairyWingFlap>();
        if (wingFlap != null)
        {
            wingFlap.ReleasePoseSmoothly();
            wingFlap.SetSpeedBoost(fairyFlapSpeedBoost);
        }

        // Bogen statt gerader Linie (gleiches Prinzip wie beim Reinflug ins Portal) — zufälliger
        // seitlicher Kontrollpunkt, damit die Fee aktiv reinfliegt statt stur geradeaus zu gleiten.
        Vector3 toTarget = toPos - fromPos;
        Vector3 perp     = new Vector3(-toTarget.y, toTarget.x, 0f).normalized;
        perp            *= (UnityEngine.Random.value < 0.5f ? -1f : 1f);
        Vector3 control  = fromPos + toTarget * 0.5f + perp * fairyCurveStrength;

        float t = 0f;
        while (t < fairyFlightDuration)
        {
            t += Time.deltaTime;
            // SmoothStep: startet aus der Ruhe heraus, beschleunigt in den Flug und wird erst kurz
            // vorm Ankommen wieder langsamer — wirkt wie ein aktiver Anflug, nicht wie ein Rutschen.
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fairyFlightDuration));
            Vector3 a = Vector3.Lerp(fromPos, control, p);
            Vector3 b = Vector3.Lerp(control, toPos, p);
            fairy.position   = Vector3.Lerp(a, b, p);
            fairy.localScale = Vector3.Lerp(Vector3.zero, toScale, p);
            yield return null;
        }

        fairy.position   = toPos;
        fairy.localScale = toScale;

        var col = fairy.GetComponent<Collider2D>();
        if (col != null) col.enabled = true;
        var fairyFloat = fairy.GetComponent<FairyFloat>();
        if (fairyFloat != null) fairyFloat.enabled = true;
        if (wingFlap != null) wingFlap.SetSpeedBoost(1f);

        onDone?.Invoke();
    }
}
