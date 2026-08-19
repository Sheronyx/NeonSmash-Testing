using UnityEngine;
using System.Collections.Generic;

public class CloudPool : MonoBehaviour
{
    [Header("Cloud Settings")]
    [SerializeField] private GameObject cloudPrefab;
    [SerializeField] private int poolSize = 10;
    [SerializeField] private float scrollSpeed = 0.3f;  // Units pro Sekunde nach links
    [SerializeField] private float spawnRateSeconds = 2f;  // Neue Wolke alle X Sekunden
    [SerializeField] private float screenBufferDistance = 2f;  // Wie weit außerhalb spawnen/verschwinden

    [Header("Random Spawn")]
    [SerializeField] private float randomYMin = -2f;
    [SerializeField] private float randomYMax = 2f;
    [SerializeField] private float randomScaleMin = 0.8f;
    [SerializeField] private float randomScaleMax = 1.2f;

    private Queue<Transform> cloudPool = new Queue<Transform>();
    private float nextSpawnTime = 0f;
    private float spawnRightX;
    private float despawnLeftX;

    private void Start()
    {
        // Spawn/Despawn Grenzen automatisch aus Kamera berechnen
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            float cameraWidth = mainCamera.orthographicSize * mainCamera.aspect;
            spawnRightX = cameraWidth * 0.5f + screenBufferDistance;      // Rechts außerhalb
            despawnLeftX = -cameraWidth * 0.5f - screenBufferDistance;    // Links außerhalb
            Debug.Log($"[CloudPool] Grenzen automatisch berechnet: Spawn={spawnRightX}, Despawn={despawnLeftX}");
        }
        else
        {
            Debug.LogError("[CloudPool] Keine Kamera gefunden!");
            return;
        }

        // Pool initialisieren
        for (int i = 0; i < poolSize; i++)
        {
            GameObject cloud = Instantiate(cloudPrefab, transform);
            cloud.SetActive(false);
            cloudPool.Enqueue(cloud.transform);
        }

        PrewarmScreen();

        nextSpawnTime = Time.time + spawnRateSeconds;
    }

    // Verteilt Wolken sofort über die ganze Bildschirmbreite, damit der Schirm
    // ab Spielstart bedeckt ist (statt erst nacheinander reinzuziehen).
    private void PrewarmScreen()
    {
        // Natürlicher Abstand zwischen zwei Spawns im laufenden Betrieb
        float spacing = scrollSpeed * spawnRateSeconds;
        if (spacing <= 0.01f) spacing = 1f;

        for (float x = spawnRightX; x > despawnLeftX && cloudPool.Count > 0; x -= spacing)
        {
            SpawnCloud(x);
        }
    }

    private void Update()
    {
        // Alle aktiven Wolken bewegen (direkte Child-Iteration statt
        // GetComponentsInChildren, das pro Frame ein neues Array alloziert)
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform cloud = transform.GetChild(i);
            if (!cloud.gameObject.activeSelf) continue;  // pooled/despawned

            cloud.position += Vector3.left * scrollSpeed * Time.deltaTime;

            // Wenn Wolke zu weit links → recyceln
            if (cloud.position.x < despawnLeftX)
            {
                cloud.gameObject.SetActive(false);
                cloudPool.Enqueue(cloud);
            }
        }

        // Neue Wolke spawnen
        if (Time.time >= nextSpawnTime && cloudPool.Count > 0)
        {
            SpawnCloud(spawnRightX);
            nextSpawnTime = Time.time + spawnRateSeconds;
        }
    }

    private void SpawnCloud(float spawnX)
    {
        Transform cloud = cloudPool.Dequeue();
        cloud.gameObject.SetActive(true);

        // Zufällige Position
        float randomY = Random.Range(randomYMin, randomYMax);
        cloud.position = new Vector3(spawnX, randomY, 0);

        // Zufällige Größe
        float randomScale = Random.Range(randomScaleMin, randomScaleMax);
        cloud.localScale = Vector3.one * randomScale;
    }
}
