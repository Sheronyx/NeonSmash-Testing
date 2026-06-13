using UnityEngine;

public class IdleFloat : MonoBehaviour
{
    [SerializeField] float amplitude      = 0.06f;
    [SerializeField] float period         = 2.0f;
    [SerializeField] float spawnDelay     = 0.25f;  // SpawnPulse abwarten
    [SerializeField] float easeInDuration = 0.4f;   // Amplitude sanft einblenden (kein Sprung)

    Vector3 _originWorld;  // World Position statt Local
    float   _t;
    float   _phase;

    void Start()
    {
        _originWorld = transform.position;  // Aktuelle World Position speichern
        _phase       = Random.Range(0f, Mathf.PI * 2f); // Elemente floaten nicht synchron
    }

    void Update()
    {
        _t += Time.deltaTime;
        if (_t < spawnDelay) return;

        float elapsed = _t - spawnDelay;

        // Amplitude von 0 hochrampen, sonst springt das Element im ersten Frame
        // um sin(_phase)*amplitude (Zufalls-Phase) → sichtbares Ruckeln.
        float ramp = easeInDuration > 0f
            ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / easeInDuration))
            : 1f;

        float y = Mathf.Sin(_phase + elapsed * (Mathf.PI * 2f / period)) * amplitude * ramp;
        transform.position = _originWorld + new Vector3(0f, y, 0f);  // World Position nutzen
    }
}
