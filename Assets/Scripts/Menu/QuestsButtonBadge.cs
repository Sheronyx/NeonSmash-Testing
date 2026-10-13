using UnityEngine;

// Das Ausrufezeichen am Quests-Knopf im Startmenue. Zeigt sich, wenn es etwas abzuholen gibt
// ODER die Woche neu ist und der Spieler die Aufgaben noch nicht angesehen hat.
// Haengt an einem Objekt, das dauerhaft aktiv bleibt -- geschaltet wird nur badgeRoot.
public class QuestsButtonBadge : MonoBehaviour
{
    [Tooltip("Das Badge selbst. Leer lassen = dieses GameObject.")]
    [SerializeField] private GameObject badgeRoot;

    private void Awake()
    {
        if (badgeRoot == null) badgeRoot = gameObject;
    }

    private void OnEnable()
    {
        WeeklyQuestManager.OnQuestsChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        WeeklyQuestManager.OnQuestsChanged -= Refresh;
    }

    // Auch beim Zurueckkehren ins Menue pruefen: Fortschritt entsteht im Spiel, nicht hier.
    private void OnApplicationFocus(bool hatFokus)
    {
        if (hatFokus) Refresh();
    }

    public void Refresh()
    {
        if (badgeRoot != null) badgeRoot.SetActive(WeeklyQuestManager.ShouldShowBadge());
    }
}
