using UnityEngine;

public class ZapplingLine : MonoBehaviour
{
    private LineRenderer lr;
    [SerializeField] private float zappleStrength = 0.1f;
    [SerializeField] private float zappleSpeed = 5f;

    private Vector3[] basePositions;
    private Vector3 lastTransformPosition;

    private void Start()
    {
        lr = GetComponent<LineRenderer>();

        // Basis-Positionen speichern
        basePositions = new Vector3[lr.positionCount];
        for (int i = 0; i < lr.positionCount; i++)
        {
            basePositions[i] = lr.GetPosition(i);
        }

        lastTransformPosition = transform.position;
    }

    private void Update()
    {
        // Wenn Transform Position geändert, update basePositions
        Vector3 positionDelta = transform.position - lastTransformPosition;
        if (positionDelta.magnitude > 0.001f)
        {
            for (int i = 0; i < basePositions.Length; i++)
            {
                basePositions[i] += positionDelta;
            }
            lastTransformPosition = transform.position;
        }

        // Jeder Punkt zappelt basierend auf Zeit
        for (int i = 0; i < lr.positionCount; i++)
        {
            Vector3 pos = basePositions[i];

            // Perlin Noise für natürliches Zappeln (X und Y)
            float noiseX = Mathf.PerlinNoise(Time.time * zappleSpeed, i * 10f) - 0.5f;
            float noiseY = Mathf.PerlinNoise(Time.time * zappleSpeed + 100f, i * 10f) - 0.5f;

            pos.x += noiseX * zappleStrength;
            pos.y += noiseY * zappleStrength;

            lr.SetPosition(i, pos);
        }
    }
}
