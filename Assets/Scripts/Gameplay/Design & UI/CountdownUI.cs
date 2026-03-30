using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class CountdownUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private float stepDuration = 0.75f;
    [SerializeField] private float popScale = 1.25f;
    [SerializeField] private Vector3 baseScale = Vector3.one;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip tickClip, goClip;

    public event Action OnCountdownFinished;

    bool isRunning;

    void Awake()
    {
        if (countdownText != null)
        {
            countdownText.text = string.Empty;
            countdownText.transform.localScale = baseScale;
        }
        gameObject.SetActive(false);
    }

    public void StartCountdown()
    {
        if (isRunning) return;
        gameObject.SetActive(true);
        StartCoroutine(Co_Countdown());
    }

    IEnumerator Co_Countdown()
    {
        isRunning = true;
        string[] steps = { "3", "2", "1", "GO" };

        foreach (var step in steps)
        {
            countdownText.text = step;
            countdownText.alpha = 1f;
            countdownText.transform.localScale = baseScale * popScale;

            if (audioSource)
            {
                var clip = step == "GO" ? goClip : tickClip;
                if (clip) audioSource.PlayOneShot(clip);
            }

            float t = 0f, popBack = stepDuration * 0.35f;
            while (t < popBack)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / popBack);
                float eased = 1f - Mathf.Pow(1f - k, 3);
                countdownText.transform.localScale = Vector3.Lerp(baseScale * popScale, baseScale, eased);
                yield return null;
            }

            float hold = 0f, remain = stepDuration - popBack;
            while (hold < remain) { hold += Time.unscaledDeltaTime; yield return null; }
        }

        float f = 0f, fade = 0.35f;
        while (f < fade)
        {
            f += Time.unscaledDeltaTime;
            countdownText.alpha = Mathf.Lerp(1f, 0f, f / fade);
            yield return null;
        }

        countdownText.text = string.Empty;
        gameObject.SetActive(false);
        isRunning = false;
        OnCountdownFinished?.Invoke();
    }
}
