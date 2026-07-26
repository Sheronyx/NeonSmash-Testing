using UnityEngine;

// Einzelnes buntes Steinstück für den neuen Colorless-Ankündigungseffekt (siehe PortalColorlessEffect):
// fliegt von einem Startpunkt (typischerweise rechts vom Portal) auf einer Sweep-Kurve zum Zentrum —
// ähnlich VortexPoint, aber mit ECHTEM Sog-Effekt: der Radius schrumpft nicht linear, sondern bleibt
// lange fast konstant und bricht erst gegen Ende (radiusEaseExponent) schnell zusammen, während der
// Winkel ebenfalls per Ease-In schwenkt — zusammen ergibt das ein spätes, deutliches "Reinziehen" statt
// eines gleichmäßig kleiner werdenden Kreisens. Gegen den Uhrzeigersinn, Teleport zum Startpunkt statt
// Spawn dort. Kurz vorm Ziel schrumpft und fadet das Stück zusätzlich aus, bis es verschwindet.
public class ColorlessFlyInPiece : MonoBehaviour
{
    [Header("Kurve")]
    [SerializeField] private float rotationSpeed = 220f;

    [Header("Einsaugen (Ende der Flugbahn)")]
    [Tooltip("Ab welchem Flugfortschritt (0-1) das Stück zu schrumpfen/fadn beginnt — niedriger = schrumpft " +
             "schon deutlich früher, sieht dadurch mehr nach echtem Sog aus statt nach spätem Verschwinden.")]
    [Range(0f, 0.95f)]
    [SerializeField] private float suckStartProgress = 0.4f;
    [SerializeField] private float minScaleBeforeGone = 0.1f;

    private Transform target;
    private float duration;
    private float startDelay;
    private float angleEaseExponent;
    private float radiusEaseExponent;

    private Vector3 initialScale;
    private SpriteRenderer[] renderers;
    private float[] rendererBaseAlpha;

    private float initialRadius;
    private float initialAngle;
    private float sweepRadians;

    private float t;
    private bool waiting;
    private bool arrived;

    /// <summary>Teleportiert das Stück sofort zu spawnWorldPos und fliegt danach (nach startDelaySecs
    /// Verzögerung) über durationSecs auf einer Sweep-Kurve (sweepDegreesForThisPiece, gegen den
    /// Uhrzeigersinn) zu flyTarget. Alle Kurven-Parameter kommen zentral von PortalColorlessEffect, damit
    /// Einflug UND Burst dieselbe Formel (nur umgekehrt) nutzen.</summary>
    public void FlyIn(Vector3 spawnWorldPos, Transform flyTarget, float durationSecs, float startDelaySecs,
                       float sweepDegreesForThisPiece, float angleEaseExp, float radiusEaseExp)
    {
        transform.position = spawnWorldPos;
        target     = flyTarget;
        duration   = Mathf.Max(0.05f, durationSecs);
        startDelay = startDelaySecs;
        angleEaseExponent  = angleEaseExp;
        radiusEaseExponent = radiusEaseExp;

        initialScale = transform.localScale;
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        rendererBaseAlpha = new float[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            rendererBaseAlpha[i] = renderers[i].color.a;

        Vector3 offset = spawnWorldPos - target.position;
        initialRadius = offset.magnitude;
        initialAngle  = Mathf.Atan2(offset.y, offset.x);
        sweepRadians  = sweepDegreesForThisPiece * Mathf.Deg2Rad; // positiv = gegen den Uhrzeigersinn

        t = 0f;
        waiting = startDelay > 0f;
        arrived = false;
        enabled = true;
    }

    private void Update()
    {
        if (arrived || target == null) return;

        if (waiting)
        {
            startDelay -= Time.deltaTime;
            if (startDelay > 0f) return;
            waiting = false;
        }

        t += Time.deltaTime / duration;
        transform.Rotate(Vector3.forward * rotationSpeed * Time.deltaTime);

        if (t >= 1f)
        {
            arrived = true;
            gameObject.SetActive(false);
            return;
        }

        float radius = initialRadius * Mathf.Pow(1f - t, radiusEaseExponent);
        float angle  = initialAngle + sweepRadians * Mathf.Pow(t, angleEaseExponent);

        Vector3 pos = target.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
        pos.z = 0f;
        transform.position = pos;

        if (t >= suckStartProgress)
        {
            float k = (t - suckStartProgress) / (1f - suckStartProgress);
            transform.localScale = initialScale * Mathf.Lerp(1f, minScaleBeforeGone, k);
            float alphaMul = Mathf.Lerp(1f, 0f, k);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color c = renderers[i].color;
                c.a = rendererBaseAlpha[i] * alphaMul;
                renderers[i].color = c;
            }
        }
    }
}
