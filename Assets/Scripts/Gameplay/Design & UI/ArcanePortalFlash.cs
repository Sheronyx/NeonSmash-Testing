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

    // 🔥 NEU: hört auf ALLE Modes
    private void OnEnable()
    {
        SpecialModeManager.OnModeStarted += HandleModeStart;
        SpecialModeManager.OnModeEnded += HandleModeEnd;
    }

    private void OnDisable()
    {
        SpecialModeManager.OnModeStarted -= HandleModeStart;
        SpecialModeManager.OnModeEnded -= HandleModeEnd;
    }

    private void HandleModeStart(SpecialMode mode)
    {
        SetMode(mode);
    }

    private void HandleModeEnd(SpecialMode mode)
    {
        SetMode(SpecialMode.None);
    }

    // =========================
    // 💥 FLASH
    // =========================

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

        // 🔥 MODE CHECK (NEU)
        bool isFountain = vfx.GetBool("IsFountainMode");
        bool isGold = vfx.GetBool("IsGoldMode");

        string particlesName;

        if (isFountain)
            particlesName = "Color Particles Blue";
        else if (isGold)
            particlesName = "Color Particles Gold";
        else
            particlesName = "Color Particles Normal";

        Vector4 normalParticles = vfx.GetVector4(particlesName);
        Vector4 clearedParticles = normalParticles * flashMultiplier;

        // --- Fade IN ---
        float t = 0f;
        while (t < fadeInTime)
        {
            float n = t / fadeInTime;

            vfx.SetVector4(particlesName, Vector4.Lerp(normalParticles, clearedParticles, n));

            t += Time.deltaTime;
            yield return null;
        }

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

        vfx.SetVector4(particlesName, normalParticles);

        flashRoutine = null;
    }

    // =========================
    // 🎨 MODE SWITCH
    // =========================

    public void SetMode(SpecialMode mode)
    {
        if (vfx == null) return;

        // Reset
        vfx.SetBool("IsGoldMode", false);
        vfx.SetBool("IsFountainMode", false);

        switch (mode)
        {
            case SpecialMode.Gold:
                vfx.SetBool("IsGoldMode", true);
                break;

            case SpecialMode.Fountain:
                vfx.SetBool("IsFountainMode", true);
                break;
        }
    }
}