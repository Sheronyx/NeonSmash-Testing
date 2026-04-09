using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class SpecialModeUIMaterialSwitcher : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private List<Image> targetImages;

    [Header("Materials")]
    [SerializeField] private Material normalMaterial;
    [SerializeField] private Material goldMaterial;
    [SerializeField] private Material fountainMaterial;

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
        switch (mode)
        {
            case SpecialMode.Gold:
                ApplyMaterial(goldMaterial);
                break;

            case SpecialMode.Fountain:
                ApplyMaterial(fountainMaterial);
                break;
        }
    }

    private void HandleModeEnd(SpecialMode mode)
    {
        ApplyMaterial(normalMaterial);
    }

    private void ApplyMaterial(Material mat)
    {
        foreach (var img in targetImages)
        {
            if (img != null && mat != null)
                img.material = mat;
        }
    }
}