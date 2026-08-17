using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TheVayuputra
{
    public class ShaderShowcaseController : MonoBehaviour
    {
        [Header("Material Names")]
        public List<string> materialNames = new List<string>();
        public Text materialNameText;
        [Header("Models To Apply Material")]
        public List<Renderer> modelRenderers = new List<Renderer>();

        [Header("Material Presets")]
        public List<Material> materials = new List<Material>();

        private int currentIndex = 0;

        void Start()
        {
            ApplyMaterial();
        }

        void UpdateText()
        {
            if (materialNames.Count == 0) return;
            string name = materialNames[currentIndex];
            materialNameText.text = name;
        }

        // Apply current material to all models
        void ApplyMaterial()
        {
            if (materials.Count == 0) return;

            Material mat = materials[currentIndex];

            foreach (Renderer rend in modelRenderers)
            {
                if (rend != null)
                    rend.material = mat;
            }
            UpdateText();
            Debug.Log("Applied Material: " + mat.name);
        }

        // Next Button
        public void NextMaterial()
        {
            currentIndex++;

            if (currentIndex >= materials.Count)
                currentIndex = 0;

            ApplyMaterial();
        }

        // Previous Button
        public void PreviousMaterial()
        {
            currentIndex--;

            if (currentIndex < 0)
                currentIndex = materials.Count - 1;

            ApplyMaterial();
        }
    }
}
