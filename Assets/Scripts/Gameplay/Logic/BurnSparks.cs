using UnityEngine;
using System.Collections;

public class BurnSparks : MonoBehaviour
{
    [SerializeField] private float lineLength = 1.6f;
    [SerializeField] private bool isHorizontal = true;
    [SerializeField] private bool isQuadMode = false;  // Für Quad-Outline (Normal Tap Element)
    [SerializeField] private Transform topBottomEmitter;    // Linear: Oben/Unten oder Links/Rechts
    [SerializeField] private Transform bottomTopEmitter;    // Linear: Unten/Oben oder Rechts/Links

    private Coroutine burnRoutine;

    private void Awake()
    {
        if (isQuadMode)
        {
            if (topBottomEmitter == null)
            {
                Debug.LogWarning("[BurnSparks] Quad Mode, aber kein Emitter zugewiesen!");
            }
        }
        else
        {
            if (topBottomEmitter != null && bottomTopEmitter != null)
            {
                SetEmitterRotations();
            }
            else
            {
                Debug.LogWarning("[BurnSparks] Linear Mode, aber Emitter nicht im Inspector zugewiesen!");
            }
        }
    }

    private void SetEmitterRotations()
    {
        if (topBottomEmitter == null || bottomTopEmitter == null) return;

        if (isHorizontal)
        {
            // Horizontal: Partikel spreaden links/rechts
            topBottomEmitter.localRotation = Quaternion.Euler(0, 0, 0);
            bottomTopEmitter.localRotation = Quaternion.Euler(0, 0, 180);
        }
        else
        {
            // Vertikal: Partikel spreaden oben/unten
            topBottomEmitter.localRotation = Quaternion.Euler(0, 0, 0);
            bottomTopEmitter.localRotation = Quaternion.Euler(180, 0, 0);
        }
    }

    public void SetQuadMode(bool quadMode)
    {
        isQuadMode = quadMode;
    }

    public void StartBurn(float duration)
    {
        if (burnRoutine != null) StopCoroutine(burnRoutine);
        burnRoutine = StartCoroutine(Co_Burn(duration));
    }

    private IEnumerator Co_Burn(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float remaining = 1f - (elapsed / duration);

            if (isQuadMode)
            {
                UpdateQuadEmitter(remaining);
            }
            else
            {
                UpdateEmitterPositions(remaining);
            }

            yield return null;
        }

        burnRoutine = null;
    }

    private void UpdateQuadEmitter(float remaining)
    {
        if (topBottomEmitter == null) return;

        // Der Emitter folgt der schrumpfenden Quad-Outline
        // t geht von 0 (Anfang oben) bis 1 (wieder oben) während die Linie abbrennt
        float t = 1f - remaining;
        Vector3 quadPoint = GetQuadPoint(t, 0.75f);  // 0.75 = gleicher Radius wie FuseCountdown
        topBottomEmitter.localPosition = quadPoint;
    }

    private Vector3 GetQuadPoint(float t, float radius)
    {
        t = t % 1f;
        float side = t * 4f;

        if (side < 1f)
        {
            // Links: von oben nach unten (oben links → unten links)
            float y = Mathf.Lerp(radius, -radius, side);
            return new Vector3(-radius, y, 0f);
        }
        else if (side < 2f)
        {
            // Unten: von links nach rechts (unten links → unten rechts)
            float x = Mathf.Lerp(-radius, radius, side - 1f);
            return new Vector3(x, -radius, 0f);
        }
        else if (side < 3f)
        {
            // Rechts: von unten nach oben (unten rechts → oben rechts)
            float y = Mathf.Lerp(-radius, radius, side - 2f);
            return new Vector3(radius, y, 0f);
        }
        else
        {
            // Oben: von rechts nach links (oben rechts → oben links)
            float x = Mathf.Lerp(radius, -radius, side - 3f);
            return new Vector3(x, radius, 0f);
        }
    }

    private void UpdateEmitterPositions(float remaining)
    {
        if (topBottomEmitter == null || bottomTopEmitter == null) return;

        float distance = (lineLength * 0.5f) * remaining;

        if (isHorizontal)
        {
            // Horizontal: Links und Rechts
            topBottomEmitter.localPosition = new Vector3(-distance, 0, 0);
            bottomTopEmitter.localPosition = new Vector3(distance, 0, 0);
        }
        else
        {
            // Vertikal: Oben und Unten
            topBottomEmitter.localPosition = new Vector3(0, distance, 0);
            bottomTopEmitter.localPosition = new Vector3(0, -distance, 0);
        }
    }
}
