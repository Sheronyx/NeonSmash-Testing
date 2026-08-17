using UnityEngine;

// Dreht das gesamte Objekt (samt fester Kind-Formation) als starren Koerper auf einer einzigen,
// gemeinsamen Umlaufbahn - keine Neuverteilung der Kinder, keine Eigenrotation, keine individuellen Werte.
public class UniformOrbit : MonoBehaviour
{
    public Vector3 orbitAxis = Vector3.forward;
    public float orbitSpeed = 30f;

    void Update()
    {
        transform.Rotate(orbitAxis.normalized, orbitSpeed * Time.deltaTime, Space.Self);
    }
}
