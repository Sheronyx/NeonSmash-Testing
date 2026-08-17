using UnityEngine;

public class OrbitPivot : MonoBehaviour
{
    [Header("Formation Orbit (dreht die gesamte Kugel-Formation als Ganzes, Abstaende bleiben dadurch immer erhalten)")]
    public Vector3 orbitAxis = new Vector3(0.4f, 1f, 0.2f);
    public float orbitSpeed = 30f;

    [Header("Kugel-Formation")]
    public float radius = 1.5f;

    [Header("Self-Spin (jeder Stein dreht sich zusaetzlich individuell um sich selbst)")]
    public float minSpinSpeed = 60f;
    public float maxSpinSpeed = 180f;

    private Transform[] stones;
    private Vector3[] spinAxes;
    private float[] spinSpeeds;

    void Start()
    {
        int count = transform.childCount;
        stones = new Transform[count];
        spinAxes = new Vector3[count];
        spinSpeeds = new float[count];

        for (int i = 0; i < count; i++)
        {
            stones[i] = transform.GetChild(i);

            // Fibonacci-Sphere: gleichmaessige Verteilung auf einer Kugel
            float t = count <= 1 ? 0f : (float)i / (count - 1);
            float phi = Mathf.Acos(1f - 2f * t);
            float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
            float theta = goldenAngle * i;

            float x = Mathf.Sin(phi) * Mathf.Cos(theta);
            float y = Mathf.Sin(phi) * Mathf.Sin(theta);
            float z = Mathf.Cos(phi);

            stones[i].localPosition = new Vector3(x, y, z) * radius;

            spinAxes[i] = Random.onUnitSphere;
            spinSpeeds[i] = Random.Range(minSpinSpeed, maxSpinSpeed);
        }
    }

    void Update()
    {
        // Ganze Formation als starren Koerper drehen - Abstaende zwischen den Steinen bleiben dadurch garantiert erhalten
        transform.Rotate(orbitAxis.normalized, orbitSpeed * Time.deltaTime, Space.Self);

        // Jeder Stein dreht sich zusaetzlich individuell um sich selbst (beeinflusst nicht die Position/Formation)
        for (int i = 0; i < stones.Length; i++)
        {
            stones[i].Rotate(spinAxes[i], spinSpeeds[i] * Time.deltaTime, Space.Self);
        }
    }
}