using System.Collections;
using UnityEngine;

public class FireworkController : MonoBehaviour
{
    [Header("Particle Systems")]
    public ParticleSystem rocket;
    public ParticleSystem explosion;

    [Header("Timing")]
    public float rocketDuration = 0.5f;

    [Header("Optional Position Offset")]
    public Vector3 startOffset = new Vector3(0, -200f, 0); // unten starten

    public void PlayFirework()
    {
        Debug.Log("FIREWORK TRIGGERED");
        StartCoroutine(FireworkRoutine());
    }

    private IEnumerator FireworkRoutine()
    {
        // 🚀 Startposition setzen
        Vector3 startPos = transform.position + startOffset;
        rocket.transform.position = startPos;

        // Rakete starten
        rocket.Play();

        // Warten bis oben
        yield return new WaitForSeconds(rocketDuration);

        // 💥 Explosion an Raketenposition
        Vector3 explosionPos = rocket.transform.position;

        explosion.transform.position = explosionPos;
        explosion.Play();
    }
}