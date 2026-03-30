using UnityEngine;

public class TutorialWindowAnimator : MonoBehaviour
{
    // Direkt beim Aktivieren sicherstellen, dass alles sichtbar ist
    private void OnEnable()
    {
        gameObject.SetActive(true);
    }

    // Wird vom CloseButton aufgerufen
    public void CloseWindow()
    {
        transform.parent.gameObject.SetActive(false);
    }
}
