using UnityEngine;

// Haelt ein Objekt an einem Knochen fest, auch wenn dieser nur von der Animation bewegt wird.
//
// Hintergrund: Die Feen-Animationen bewegen das Skelett, nicht das Wurzelobjekt. Ein Kind des
// Wurzelobjekts bleibt deshalb stehen, waehrend der sichtbare Koerper schwebt oder wegfliegt.
// Diese Komponente setzt die Position jeden Frame neu -- nach dem Animator, deshalb LateUpdate.
//
// Der Versatz liegt im lokalen Raum des Bezugsobjekts (der Fee). Dadurch wandert das Objekt
// korrekt mit, wenn Unity die Fee verschiebt, dreht ODER skaliert, folgt aber nicht der
// Eigendrehung des Knochens -- eine Aura soll mitwandern und nicht mitkippen.
public class FollowBonePosition : MonoBehaviour
{
    [Tooltip("Der Knochen, dem gefolgt wird (z. B. mixamorig:Hips).")]
    [SerializeField] private Transform bone;
    [Tooltip("Bezugsobjekt fuer den Versatz -- normalerweise das Wurzelobjekt der Fee.")]
    [SerializeField] private Transform reference;
    [Tooltip("Versatz zum Knochen, im lokalen Raum des Bezugsobjekts. Wird beim Einrichten " +
             "aus der Ruhelage berechnet und hier abgelegt -- nicht zur Laufzeit gemessen, " +
             "weil das Objekt beim Start deaktiviert ist und dann schon animiert waere.")]
    [SerializeField] private Vector3 offsetInReferenceSpace;

    private void LateUpdate()
    {
        if (bone == null) return;
        transform.position = reference != null
            ? bone.position + reference.TransformVector(offsetInReferenceSpace)
            : bone.position + offsetInReferenceSpace;
    }
}
