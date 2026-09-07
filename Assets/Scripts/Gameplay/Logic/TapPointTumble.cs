using UnityEngine;

// Lässt 3D-Tap-Elemente durchgehend sanft um eine Achse taumeln/rotieren — rein optisch, hat
// keinen Einfluss auf die Trefferkennung (die läuft über BoxCollider2D/Physics2D, unabhängig von
// der visuellen Rotation). Bewusst kontinuierlich statt sprunghaft: Update() dreht pro Frame nur um
// einen winzigen Schritt weiter, es gibt also nie einen Rotations-"Sprung".
public class TapPointTumble : MonoBehaviour
{
    [Tooltip("Grad pro Sekunde. Niedrig halten, damit es ruhig wirkt statt hektisch zu drehen.")]
    [SerializeField] private float rotationSpeed = 25f;

    [Tooltip("Feste Rotationsachse. Leer lassen (0,0,0) = pro Instanz eine zufällige Achse — wirkt " +
             "organischer, da nicht alle Elemente exakt synchron in dieselbe Richtung taumeln.")]
    [SerializeField] private Vector3 rotationAxis = Vector3.zero;

    private Vector3 axis;

    private void Awake()
    {
        axis = rotationAxis == Vector3.zero ? Random.onUnitSphere : rotationAxis.normalized;
    }

    private void Update()
    {
        transform.Rotate(axis, rotationSpeed * Time.deltaTime, Space.World);
    }
}
