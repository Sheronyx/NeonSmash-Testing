using System.Collections;
using UnityEngine;

// Einzelnes farbloses Ersatzstück für den Colorless-Burst (siehe PortalColorlessEffect): exakt dieselbe
// Bewegungsformel wie ColorlessFlyInPiece, nur zeitlich/radial UMGEKEHRT — der Radius wächst von 0 auf
// maxRadius (statt zu schrumpfen), mit demselben Ease-Prinzip (bleibt am Anfang klein und schießt gegen
// Ende schnell nach außen — der Rückwärtslauf des Sog-Effekts), während der Winkel über die GESAMTE
// Flugdauer weiterschwenkt. Dadurch liest sich die Bewegung als echter Strudel-Rückwärtslauf des
// Einflugs, nicht als generischer Radial-Burst.
public class ColorlessBurstPiece : MonoBehaviour
{
    public void Play(float initialAngleDeg, float sweepDegrees, float angleEaseExponent, float radiusEaseExponent,
                      float duration, float maxRadius, float rotationSpeed, float popInDuration, Transform center)
    {
        StartCoroutine(Co_Play(initialAngleDeg, sweepDegrees, angleEaseExponent, radiusEaseExponent,
                                duration, maxRadius, rotationSpeed, popInDuration, center));
    }

    private IEnumerator Co_Play(float initialAngleDeg, float sweepDegrees, float angleEaseExponent, float radiusEaseExponent,
                                 float duration, float maxRadius, float rotationSpeed, float popInDuration, Transform center)
    {
        Vector3 targetScale = transform.localScale;
        transform.localScale = Vector3.zero;

        float initialAngleRad = initialAngleDeg * Mathf.Deg2Rad;
        float sweepRad = sweepDegrees * Mathf.Deg2Rad;
        duration = Mathf.Max(0.05f, duration);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float tc = Mathf.Clamp01(elapsed / duration);

            // Umgekehrt zu ColorlessFlyInPiece: Radius WÄCHST (statt zu schrumpfen), bleibt dabei dank
            // desselben Ease-Exponenten lange klein und schießt erst gegen Ende schnell nach außen.
            float radius = maxRadius * Mathf.Pow(tc, radiusEaseExponent);
            float angle  = initialAngleRad + sweepRad * Mathf.Pow(tc, angleEaseExponent);

            Vector3 pos = center.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            pos.z = 0f;
            transform.position = pos;

            float scaleK = popInDuration > 0f ? Mathf.Clamp01(elapsed / popInDuration) : 1f;
            transform.localScale = targetScale * scaleK;

            transform.Rotate(Vector3.forward * rotationSpeed * Time.deltaTime);
            yield return null;
        }

        Destroy(gameObject);
    }
}
