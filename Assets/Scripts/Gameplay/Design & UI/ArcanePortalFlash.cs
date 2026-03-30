using UnityEngine;
using UnityEngine.VFX;
using System.Collections;

public class ArcanePortalFlash : MonoBehaviour
{
    public VisualEffect vfx;

    public float fadeInTime = 0.15f;
    public float holdTime = 0.05f;
    public float fadeOutTime = 0.25f;

    [Header("Flash Strength")]
    [SerializeField] private float flashMultiplier = 2.5f;

    private Coroutine flashRoutine;

    public void FlashParticles()
    {
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(CoFlash());
    }

    IEnumerator CoFlash()
    {
        if (vfx == null)
            yield break;

        bool isGold = vfx.GetBool("IsGoldMode");

        // 👉 richtige Property je nach Mode
        string particlesName = isGold ? "Color Particles Gold" : "Color Particles Normal";

        // aktuelle Farbe holen
        Vector4 normalParticles = vfx.GetVector4(particlesName);

        // Ziel = heller machen (wie früher "cleared")
        Vector4 clearedParticles = normalParticles * flashMultiplier;

        // DEBUG
        Debug.Log($"[FLASH] Mode: {(isGold ? "GOLD" : "NORMAL")}");
        Debug.Log($"[FLASH] Normal: {normalParticles}");
        Debug.Log($"[FLASH] Target: {clearedParticles}");

        // --- Fade IN ---
        float t = 0f;
        while (t < fadeInTime)
        {
            float n = t / fadeInTime;

            vfx.SetVector4(particlesName, Vector4.Lerp(normalParticles, clearedParticles, n));

            t += Time.deltaTime;
            yield return null;
        }

        // exakt setzen
        vfx.SetVector4(particlesName, clearedParticles);

        yield return new WaitForSeconds(holdTime);

        // --- Fade OUT ---
        t = 0f;
        while (t < fadeOutTime)
        {
            float n = t / fadeOutTime;

            vfx.SetVector4(particlesName, Vector4.Lerp(clearedParticles, normalParticles, n));

            t += Time.deltaTime;
            yield return null;
        }

        // exakt zurücksetzen
        vfx.SetVector4(particlesName, normalParticles);

        Debug.Log("[FLASH] Done");

        flashRoutine = null;
    }

    public void SetGoldMode(bool active)
    {
        if (vfx == null) return;

        vfx.SetBool("IsGoldMode", active);
    }
}