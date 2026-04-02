using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class GoldUIMaterialSwitcher : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private List<Image> targetImages;

    [Header("Materials")]
    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material goldMaterial;

    [Header("Timing")]
    [SerializeField] private float delayBeforeSwitch = 0.1f;

    private Coroutine switchRoutine;

    private void OnEnable()
    {
        GoldModeSystem.OnGoldModeStarted += EnableGold;
        GoldModeSystem.OnGoldModeEnded += DisableGold;
    }

    private void OnDisable()
    {
        GoldModeSystem.OnGoldModeStarted -= EnableGold;
        GoldModeSystem.OnGoldModeEnded -= DisableGold;
    }

    private void EnableGold()
    {
        if (switchRoutine != null)
            StopCoroutine(switchRoutine);

        switchRoutine = StartCoroutine(Co_EnableGoldDelayed());
    }

    private IEnumerator Co_EnableGoldDelayed()
    {
        yield return new WaitForSeconds(delayBeforeSwitch);

        foreach (var img in targetImages)
        {
            if (img != null && goldMaterial != null)
            {
                img.material = goldMaterial;
            }
        }
    }

    private void DisableGold()
    {
        if (switchRoutine != null)
            StopCoroutine(switchRoutine);

        foreach (var img in targetImages)
        {
            if (img != null && normalMaterial != null)
            {
                img.material = normalMaterial;
            }
        }
    }
}