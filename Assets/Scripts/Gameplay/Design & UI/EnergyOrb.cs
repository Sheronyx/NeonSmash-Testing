using System;
using System.Collections;
using UnityEngine;

// Energiekugel, die vom zerstörten Farbelement zur passenden Fairy fliegt, dabei schrumpft und dort
// verschwindet. Folgt der Fairy-Position live (falls sie sich per FairyFloat bewegt), statt zu einer
// fixen Position zu fliegen.
public class EnergyOrb : MonoBehaviour
{
    [SerializeField] private float duration = 0.45f;
    [Tooltip("Bewegungs-Timing (0→1). Standard: sanftes Ease-In/Out.")]
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public void Play(Transform target, Action onArrived)
    {
        StartCoroutine(Co_Fly(target, onArrived));
    }

    private IEnumerator Co_Fly(Transform target, Action onArrived)
    {
        Vector3 startPos   = transform.position;
        Vector3 startScale = transform.localScale;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = moveCurve.Evaluate(Mathf.Clamp01(t / duration));
            Vector3 targetPos = target != null ? target.position : startPos;
            transform.position   = Vector3.Lerp(startPos, targetPos, k);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, k);
            yield return null;
        }

        onArrived?.Invoke();
        Destroy(gameObject);
    }
}
