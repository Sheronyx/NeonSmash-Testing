using UnityEngine;

public enum BoostType
{
    None,
    AllSwipe,
    AllTap,
    StayPositive,
    SwipeHowYouLike
}

// Zentraler Dienst (Singleton, EIN Objekt in der Szene, z.B. neben MixedPointSpawner): hält den vom
// Spieler zu Session-Beginn gewählten Boost (siehe BoostSelectionUI) für die Dauer des aktuellen Runs.
// Wird von MixedPointSpawner (Stay Positive) und PlayerInputHandler (All Swipe/All Tap/Swipe How You
// Like) gelesen.
public class BoostManager : MonoBehaviour
{
    public static BoostManager Instance { get; private set; }

    public BoostType Selected { get; private set; } = BoostType.None;

    // Stay Positive: nur das ERSTE Shocker-Vorkommen im Run wird umgewandelt — danach verhalten sich
    // weitere Shocker wieder normal. Wird bei jeder neuen Boost-Auswahl zurückgesetzt.
    public bool StayPositiveConsumed { get; set; }

    private void Awake() => Instance = this;

    public void Select(BoostType type)
    {
        Selected = type;
        StayPositiveConsumed = false;
    }
}
