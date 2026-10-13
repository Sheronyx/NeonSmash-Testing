using UnityEditor;
using UnityEngine;

// Kleine Werkzeuge fuer die Weeklies. Nur im Editor -- im Build existiert nichts davon.
public static class WeeklyQuestDebugMenu
{
    const string MenuTest  = "NeonSmash/Weeklies/Testziele (Deckel bei 3)";
    const string MenuDraw  = "NeonSmash/Weeklies/Woche neu ziehen";
    const string MenuState = "NeonSmash/Weeklies/Stand ausgeben";

    [MenuItem(MenuTest)]
    static void TestZieleUmschalten()
    {
        WeeklyQuestManager.TestZiele = !WeeklyQuestManager.TestZiele;
        NeuZiehen();   // die Aenderung greift erst bei der naechsten Ziehung
        Debug.Log($"[Weeklies] Testziele: {(WeeklyQuestManager.TestZiele ? "AN" : "AUS")} -- Woche neu gezogen.");
    }

    [MenuItem(MenuTest, true)]
    static bool TestZieleHaken()
    {
        Menu.SetChecked(MenuTest, WeeklyQuestManager.TestZiele);
        return true;
    }

    [MenuItem(MenuDraw)]
    static void WocheNeuZiehen()
    {
        NeuZiehen();
        Debug.Log("[Weeklies] Wochendaten geloescht -- beim naechsten Start werden drei neue Aufgaben gezogen.");
    }

    [MenuItem(MenuState)]
    static void StandAusgeben()
    {
        var quests = WeeklyQuestManager.GetQuests();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[Weeklies] Testziele: {(WeeklyQuestManager.TestZiele ? "AN" : "AUS")}");
        if (quests == null || quests.Length == 0) sb.AppendLine("  (noch keine Aufgaben gezogen)");
        else foreach (var q in quests)
            sb.AppendLine($"  {q.Title}  {q.Progress}/{q.Target}  +{q.Splinters} Splitter"
                          + (q.Claimed ? "  [abgeholt]" : q.IsCompleted ? "  [abholbar]" : ""));
        Debug.Log(sb.ToString());
    }

    static void NeuZiehen()
    {
        foreach (var key in new[]{ "weekly_quests_week", "weekly_quests_data",
                                   "weekly_quests_days", "weekly_quests_startlevel" })
            PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }
}
