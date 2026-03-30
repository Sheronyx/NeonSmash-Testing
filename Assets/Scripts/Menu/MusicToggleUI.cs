using UnityEngine;
using UnityEngine.UI;

public class MusicToggleUI : MonoBehaviour
{
    [SerializeField] private Toggle toggle;
    [SerializeField] private Image iconImage;
    [SerializeField] private Sprite soundOnSprite;
    [SerializeField] private Sprite soundOffSprite;

    private void Reset()
    {
        toggle = GetComponent<Toggle>();
        iconImage = GetComponentInChildren<Image>();
    }

    private void OnEnable()
    {
        if (!toggle) toggle = GetComponent<Toggle>();
        if (AudioSwitch.Instance != null)
            toggle.isOn = AudioSwitch.Instance.AudioEnabled;

        UpdateIcon(toggle.isOn);
        toggle.onValueChanged.AddListener(OnChanged);
    }

    private void OnDisable()
    {
        toggle.onValueChanged.RemoveListener(OnChanged);
    }

    private void OnChanged(bool on)
    {
        if (AudioSwitch.Instance != null)
            AudioSwitch.Instance.SetEnabled(on);

        UpdateIcon(on);
    }

    private void UpdateIcon(bool enabled)
    {
        if (!iconImage) return;
        iconImage.sprite = enabled ? soundOnSprite : soundOffSprite;
    }
}
