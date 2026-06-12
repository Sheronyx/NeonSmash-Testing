using UnityEngine;
using System.Collections.Generic;

public class RotatingSquarePool : MonoBehaviour
{
    [Header("Square Settings")]
    [SerializeField] private GameObject squarePrefab;
    [SerializeField] private int poolSize = 10;
    [SerializeField] private float scrollSpeed = 0.3f;  // Nach links
    [SerializeField] private float spawnRateSeconds = 3f;

    [Header("Spawn Area")]
    [SerializeField] private float spawnRightX = 6f;
    [SerializeField] private float despawnLeftX = -6f;
    [SerializeField] private float randomYMin = -2f;
    [SerializeField] private float randomYMax = 2f;
    [SerializeField] private float randomScaleMin = 0.5f;
    [SerializeField] private float randomScaleMax = 1.5f;

    [Header("Vertical Movement")]
    [SerializeField] private float maxVerticalSpeed = 0.5f;   // Diagonale Geschwindigkeit
    [SerializeField] private float waveAmplitude = 0.3f;      // Wellenhöhe
    [SerializeField] private float waveFrequency = 2f;        // Wellen-Frequenz

    private Queue<Transform> squarePool = new Queue<Transform>();
    private List<Transform> activeSquares = new List<Transform>();
    private Dictionary<Transform, SquareMovement> squareMovements = new Dictionary<Transform, SquareMovement>();
    private float nextSpawnTime = 0f;

    private class SquareMovement
    {
        public int type;  // 0=gerade, 1=wellig, 2=diagonal
        public float verticalSpeed;
        public float waveTime;
    }

    private void Start()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject square = Instantiate(squarePrefab, transform);
            square.SetActive(false);
            squarePool.Enqueue(square.transform);
        }

        PrewarmScreen();

        nextSpawnTime = Time.time + spawnRateSeconds;
    }

    // Verteilt Quadrate sofort über die ganze Bildschirmbreite, damit der
    // Schirm ab Spielstart bedeckt ist (statt erst nacheinander reinzulaufen).
    private void PrewarmScreen()
    {
        // Natürlicher Abstand zwischen zwei Spawns im laufenden Betrieb
        float spacing = scrollSpeed * spawnRateSeconds;
        if (spacing <= 0.01f) spacing = 1f;

        for (float x = spawnRightX; x > despawnLeftX && squarePool.Count > 0; x -= spacing)
        {
            SpawnSquare(x);
        }
    }

    private void Update()
    {
        // Nur aktive Quadrate bewegen
        for (int i = activeSquares.Count - 1; i >= 0; i--)
        {
            Transform child = activeSquares[i];
            if (!child.gameObject.activeSelf) continue;

            // Nach links
            child.position += Vector3.left * scrollSpeed * Time.deltaTime;

            // Vertikale Bewegung
            if (squareMovements.TryGetValue(child, out SquareMovement movement))
            {
                float yMovement = 0f;

                if (movement.type == 0)
                {
                    // Gerade: konstante Y-Geschwindigkeit
                    yMovement = movement.verticalSpeed * Time.deltaTime;
                }
                else if (movement.type == 1)
                {
                    // Wellig: Sinuswelle
                    movement.waveTime += waveFrequency * Time.deltaTime;
                    yMovement = Mathf.Sin(movement.waveTime) * waveAmplitude * Time.deltaTime;
                }
                else if (movement.type == 2)
                {
                    // Diagonal: langsame diagonale Bewegung
                    yMovement = movement.verticalSpeed * Time.deltaTime;
                }

                child.position += Vector3.up * yMovement;
            }

            // Rotation
            child.Rotate(0, 0, 30 * Time.deltaTime);

            // Recyclen
            if (child.position.x < despawnLeftX)
            {
                child.gameObject.SetActive(false);
                if (squareMovements.ContainsKey(child))
                {
                    squareMovements.Remove(child);
                }
                squarePool.Enqueue(child);
                activeSquares.RemoveAt(i);
            }
        }

        // Spawn
        if (Time.time >= nextSpawnTime && squarePool.Count > 0)
        {
            SpawnSquare(spawnRightX);
            nextSpawnTime = Time.time + spawnRateSeconds;
        }
    }

    private void SpawnSquare(float spawnX)
    {
        Transform square = squarePool.Dequeue();
        square.gameObject.SetActive(true);
        activeSquares.Add(square);

        float randomY = Random.Range(randomYMin, randomYMax);
        square.position = new Vector3(spawnX, randomY, 0);

        float randomScale = Random.Range(randomScaleMin, randomScaleMax);
        square.localScale = Vector3.one * randomScale;

        // Zufälliges Bewegungsmuster
        int movementType = Random.Range(0, 3);
        float verticalSpeed = Random.Range(-maxVerticalSpeed, maxVerticalSpeed);

        SquareMovement movement = new SquareMovement
        {
            type = movementType,
            verticalSpeed = verticalSpeed,
            waveTime = Random.Range(0f, Mathf.PI * 2f)
        };

        if (squareMovements.ContainsKey(square))
        {
            squareMovements[square] = movement;
        }
        else
        {
            squareMovements.Add(square, movement);
        }

        square.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
    }
}
