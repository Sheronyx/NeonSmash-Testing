using UnityEngine;

public class IdleFloat : MonoBehaviour
{
    [SerializeField] float amplitude   = 0.06f;
    [SerializeField] float period      = 2.0f;
    [SerializeField] float spawnDelay  = 0.25f;  // SpawnPulse abwarten

    Vector3 _originLocal;
    float   _t;
    float   _phase;

    void Start()
    {
        _originLocal = transform.localPosition;
        _phase       = Random.Range(0f, Mathf.PI * 2f); // Elemente floaten nicht synchron
    }

    void Update()
    {
        _t += Time.deltaTime;
        if (_t < spawnDelay) return;

        float y = Mathf.Sin(_phase + (_t - spawnDelay) * (Mathf.PI * 2f / period)) * amplitude;
        transform.localPosition = _originLocal + new Vector3(0f, y, 0f);
    }
}
