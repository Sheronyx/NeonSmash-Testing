using System.Collections;
using UnityEngine;

// Zeigt den fortschreitenden Demolierungszustand des Korbs (4 Sprites) über die Match-Dauer:
// alle (MatchDuration/4) Sekunden ein Sprite weiter, Stufe 4 (kaputt) ist bei Spielende erreicht.
// Jeder Sprite-Wechsel wird durch eine kurze Rauchwolke maskiert, damit man den harten Sprite-Pop
// nicht sieht. Bei Timeout/Bombe springt der Korb (maskiert) sofort auf Stufe 4 — Timeout erreicht
// sie ohnehin zeitlich, Bombe löst das vorzeitig aus. Bei vollem Korb (Erfolg, kein Fehlschlag)
// bleibt der Korb einfach im aktuell erreichten Zustand, es gibt nur den "FINISHED"-Banner
// (siehe WaveBasketController.EndGame).
public class BasketDemolitionView : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WaveBasketController controller;
    [SerializeField] private SpriteRenderer basketRenderer;

    [Header("Demolierungs-Sprites (1 = intakt ... 4 = kaputt)")]
    [SerializeField] private Sprite stage1Sprite;
    [SerializeField] private Sprite stage2Sprite;
    [SerializeField] private Sprite stage3Sprite;
    [SerializeField] private Sprite stage4Sprite;

    [Header("Übergang")]
    [Tooltip("Wie lange VOR dem Sprite-Wechsel die Rauchwolke startet.")]
    [SerializeField] private float smokeLeadTime = 0.5f;
    [SerializeField] private ParticleSystem smokePrefab;

    private int _stage; // 0..3
    private bool _finished;
    private float _stageDuration;

    void OnEnable()
    {
        if (!controller) controller = FindFirstObjectByType<WaveBasketController>(FindObjectsInactive.Include);
        if (controller != null)
        {
            controller.OnMatchStarted += HandleMatchStarted;
            controller.OnGameEnding += HandleGameEnding;
        }
    }

    void OnDisable()
    {
        if (controller != null)
        {
            controller.OnMatchStarted -= HandleMatchStarted;
            controller.OnGameEnding -= HandleGameEnding;
        }
    }

    private void HandleMatchStarted()
    {
        StopAllCoroutines();
        _stage = 0;
        _finished = false;
        _stageDuration = controller.MatchDuration / 4f;
        SetStage(0);
        StartCoroutine(Co_RunDemolition());
    }

    private void HandleGameEnding(string cause)
    {
        if (_finished) return;
        _finished = true;

        // Voller Korb = Erfolg, nicht Zerstörung — Zustand bleibt genau so, wie er gerade ist.
        if (cause == "basketFull") { StopAllCoroutines(); return; }

        StopAllCoroutines();
        StartCoroutine(Co_ForceBreak());
    }

    // Alle _stageDuration Sekunden eine Stufe weiter, jeweils smokeLeadTime vorher maskiert.
    private IEnumerator Co_RunDemolition()
    {
        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForSeconds(_stageDuration - smokeLeadTime);
            PlaySmoke();
            yield return new WaitForSeconds(smokeLeadTime);
            SetStage(_stage + 1);
        }
        _finished = true;
    }

    // Für Bombe/vollen Korb: sofort (maskiert) auf Stufe 4 springen, egal welche Zeit gerade lief.
    private IEnumerator Co_ForceBreak()
    {
        if (_stage < 3)
        {
            PlaySmoke();
            yield return new WaitForSeconds(smokeLeadTime);
            SetStage(3);
        }
        _finished = true;
    }

    private void SetStage(int stage)
    {
        _stage = Mathf.Clamp(stage, 0, 3);
        if (!basketRenderer) return;

        basketRenderer.sprite = _stage switch
        {
            0 => stage1Sprite,
            1 => stage2Sprite,
            2 => stage3Sprite,
            _ => stage4Sprite
        };
    }

    private void PlaySmoke()
    {
        if (smokePrefab == null || basketRenderer == null) return;
        var fx = Instantiate(smokePrefab, basketRenderer.transform.position, Quaternion.identity);
        fx.Play();
        float dur = fx.main.duration + fx.main.startLifetime.constantMax;
        Destroy(fx.gameObject, dur);
    }
}
