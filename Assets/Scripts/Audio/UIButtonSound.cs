using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour
{
    [SerializeField] private AudioClip clickSound;
    private Button btn;

    void Awake()
    {
        btn = GetComponent<Button>();
        btn.onClick.AddListener(PlayClickSound);
    }

    public void PlayClickSound()
    {
        if (clickSound != null && UIAudio.Instance != null)
            UIAudio.Instance.PlayOneShot(clickSound);
    }
}
