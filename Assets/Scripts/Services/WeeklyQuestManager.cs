using System;
using System.Collections.Generic;
using UnityEngine;

// Woechentliche Aufgaben ("Weeklies"). Ersetzt die frueheren Daily Missions samt Daily Streak.
//
// Ablauf: zu Beginn jeder Woche werden 3 Aufgaben zufaellig aus dem Pool gezogen. Der Spieler
// erfuellt sie durchs Spielen, holt die Belohnung im Quests-Fenster ab und bekommt dafuer
// Traumenergie UND Traumsplitter. Sonntag 23:59:59 LOKALER Zeit faellt alles zurueck auf null --
// nicht abgeholte Belohnungen verfallen dabei ersatzlos.
//
// Bewusst static wie MissionManager/DreamEnergyManager davor: der Fortschritt muss aus Gameplay-
// Code heraus erreichbar sein, ohne dass in jeder Szene ein Objekt stehen muss.

public enum WeeklyQuestType
{
    // Rundenbasiert
    PlayGames,          // Spiele X Spiele
    ScoreInOneGame,     // Erreiche X Punkte in einer Runde
    ScoreTotal,         // Sammle X Punkte insgesamt
    // Mechanik
    TriggerSpecialModes,// Loese X Special Modes aus
    ReachCombo,         // Erreiche eine Combo von X
    DestroyColor,       // Zerstoere X Elemente einer Farbe
    // Sammeln
    CollectDiamonds,    // Sammle X Diamanten
    GainLevels,         // Steige X Level auf
    EarnStickers,       // Erhalte X Sticker
    // Regelmaessigkeit
    PlayOnDays,         // Spiele an X verschiedenen Tagen dieser Woche
}

[Serializable]
public class WeeklyQuestData
{
    public WeeklyQuestType Type;
    public int    Target;
    public int    Splinters;        // Belohnung -- Quests zahlen ausschliesslich Traumsplitter
    public int    Progress;
    public bool   Claimed;
    /// <summary>Nur fuer DestroyColor: 0=Pink, 1=Gruen, 2=Blau. Sonst ungenutzt.</summary>
    public int    ColorIndex;

    public bool IsCompleted => Progress >= Target;
    public float Fraction    => Target <= 0 ? 1f : Mathf.Clamp01((float)Progress / Target);

    // Im Spiel ist alles auf Englisch -- die Titel deshalb auch.
    public string Title => Type switch
    {
        WeeklyQuestType.PlayGames           => Target == 1 ? "Play 1 game" : $"Play {Target} games",
        WeeklyQuestType.ScoreInOneGame      => $"Score {Target:N0} points in one run",
        WeeklyQuestType.ScoreTotal          => $"Score {Target:N0} points in total",
        WeeklyQuestType.TriggerSpecialModes => $"Trigger {Target} Special Modes",
        WeeklyQuestType.ReachCombo          => $"Reach a combo of {Target}",
        WeeklyQuestType.DestroyColor        => $"Destroy {Target} {ColorName} elements",
        WeeklyQuestType.CollectDiamonds     => $"Collect {Target} diamonds",
        WeeklyQuestType.GainLevels          => Target == 1 ? "Gain a level" : $"Gain {Target} levels",
        WeeklyQuestType.EarnStickers        => $"Earn {Target} stickers",
        WeeklyQuestType.PlayOnDays          => $"Play on {Target} different days",
        _                                   => ""
    };

    private string ColorName => ColorIndex switch { 0 => "pink", 1 => "green", 2 => "blue", _ => "" };
}

public static class WeeklyQuestManager
{
    const string PrefKeyWeek  = "weekly_quests_week";
    const string PrefKeyData  = "weekly_quests_data";
    const string PrefKeyDays  = "weekly_quests_days";   // Bitmaske der gespielten Wochentage
    const string PrefKeyLevel = "weekly_quests_startlevel";  // Spielerlevel zu Wochenbeginn
    const string PrefKeyTest  = "weekly_quests_testziele";

    /// <summary>
    /// Nur zum Testen: deckelt beim Ziehen alle Ziele auf <see cref="TestZielObergrenze"/>,
    /// damit sich eine Woche in Minuten durchspielen laesst. Der Pool bleibt unangetastet --
    /// ausschalten und neu ziehen stellt die echten Werte wieder her.
    /// </summary>
    public static bool TestZiele
    {
        get => PlayerPrefs.GetInt(PrefKeyTest, 0) == 1;
        set { PlayerPrefs.SetInt(PrefKeyTest, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    const int TestZielObergrenze = 3;
    const int    QuestCount   = 3;

    /// <summary>Aufgabe erfuellt (noch nicht abgeholt) -- fuer Badge/Hinweis am Quests-Knopf.</summary>
    public static event Action OnQuestsChanged;
    /// <summary>Belohnung abgeholt: Aufgabe, Traumenergie, Traumsplitter.</summary>
    public static event Action<WeeklyQuestData, int> OnQuestClaimed;

    static WeeklyQuestData[] _quests;

    // ── Pool ──────────────────────────────────────────────────────────────────
    // Zielwerte sind auf eine ganze Woche ausgelegt, nicht auf einen Tag. Belohnung immer
    // beide Waehrungen, Hoehe grob nach Aufwand gestaffelt.
    static readonly WeeklyQuestData[] Pool =
    {
        // Rundenbasiert
        new() { Type = WeeklyQuestType.PlayGames,           Target = 15,    Splinters = 15 },
        new() { Type = WeeklyQuestType.PlayGames,           Target = 40,    Splinters = 40 },
        new() { Type = WeeklyQuestType.ScoreInOneGame,      Target = 400,   Splinters = 15 },
        new() { Type = WeeklyQuestType.ScoreInOneGame,      Target = 700,   Splinters = 25 },
        new() { Type = WeeklyQuestType.ScoreInOneGame,      Target = 1000,  Splinters = 40 },
        new() { Type = WeeklyQuestType.ScoreTotal,          Target = 5000,  Splinters = 25 },
        new() { Type = WeeklyQuestType.ScoreTotal,          Target = 12000, Splinters = 40 },
        // Mechanik
        new() { Type = WeeklyQuestType.TriggerSpecialModes, Target = 10,    Splinters = 15 },
        new() { Type = WeeklyQuestType.TriggerSpecialModes, Target = 25,    Splinters = 25 },
        new() { Type = WeeklyQuestType.ReachCombo,          Target = 15,    Splinters = 15 },
        new() { Type = WeeklyQuestType.ReachCombo,          Target = 25,    Splinters = 40 },
        new() { Type = WeeklyQuestType.DestroyColor,        Target = 150,   Splinters = 15, ColorIndex = 0 },
        new() { Type = WeeklyQuestType.DestroyColor,        Target = 150,   Splinters = 15, ColorIndex = 1 },
        new() { Type = WeeklyQuestType.DestroyColor,        Target = 150,   Splinters = 15, ColorIndex = 2 },
        new() { Type = WeeklyQuestType.DestroyColor,        Target = 400,   Splinters = 25, ColorIndex = 0 },
        // Sammeln
        new() { Type = WeeklyQuestType.CollectDiamonds,     Target = 10,    Splinters = 15 },
        new() { Type = WeeklyQuestType.CollectDiamonds,     Target = 120,   Splinters = 25 },
        new() { Type = WeeklyQuestType.GainLevels,          Target = 1,     Splinters = 15 },
        new() { Type = WeeklyQuestType.GainLevels,          Target = 3,     Splinters = 40 },
        new() { Type = WeeklyQuestType.EarnStickers,        Target = 5,     Splinters = 25 },
        // Regelmaessigkeit
        new() { Type = WeeklyQuestType.PlayOnDays,          Target = 3,     Splinters = 15 },
        new() { Type = WeeklyQuestType.PlayOnDays,          Target = 5,     Splinters = 40 },
    };

    // ── Oeffentlich ───────────────────────────────────────────────────────────

    public static WeeklyQuestData[] GetQuests()
    {
        EnsureLoaded();
        return _quests;
    }

    /// <summary>Wie viele Aufgaben sind erfuellt, aber noch nicht abgeholt -- fuer das Badge.</summary>
    public static int ClaimableCount()
    {
        EnsureLoaded();
        int n = 0;
        foreach (var q in _quests) if (q.IsCompleted && !q.Claimed) n++;
        return n;
    }

    /// <summary>Wann die aktuelle Woche endet: der naechste Sonntag, 23:59:59 LOKALER Zeit.</summary>
    public static DateTime CurrentWeekEnd()
    {
        DateTime jetzt = DateTime.Now;
        // DayOfWeek: Sonntag = 0. Wir wollen den kommenden Sonntag (heute, falls heute Sonntag ist).
        int bisSonntag = ((int)DayOfWeek.Sunday - (int)jetzt.DayOfWeek + 7) % 7;
        DateTime sonntag = jetzt.Date.AddDays(bisSonntag);
        return sonntag.AddHours(23).AddMinutes(59).AddSeconds(59);
    }

    public static TimeSpan TimeUntilReset()
    {
        var rest = CurrentWeekEnd() - DateTime.Now;
        return rest < TimeSpan.Zero ? TimeSpan.Zero : rest;
    }

    /// <summary>Holt die Belohnung einer erfuellten Aufgabe ab. Gibt false zurueck, wenn die Aufgabe
    /// nicht erfuellt oder schon abgeholt ist.</summary>
    public static bool TryClaim(WeeklyQuestData quest)
    {
        EnsureLoaded();
        if (quest == null || !quest.IsCompleted || quest.Claimed) return false;

        quest.Claimed = true;
        Save();

        if (quest.Splinters > 0) DiamondSplinterManager.AddSplinters(quest.Splinters);

        Debug.Log($"[Weeklies] claimed: {quest.Title} -> +{quest.Splinters} splinters");
        OnQuestClaimed?.Invoke(quest, quest.Splinters);
        OnQuestsChanged?.Invoke();
        return true;
    }

    // ── Fortschritt melden ────────────────────────────────────────────────────

    public static void OnGameFinished(int score)
    {
        EnsureLoaded();
        MarkeTagGespielt();

        foreach (var q in _quests)
        {
            if (q.Claimed) continue;
            switch (q.Type)
            {
                case WeeklyQuestType.PlayGames:      q.Progress++;             break;
                case WeeklyQuestType.ScoreTotal:     q.Progress += score;      break;
                case WeeklyQuestType.ScoreInOneGame: if (score > q.Progress) q.Progress = score; break;
                case WeeklyQuestType.PlayOnDays:     q.Progress = GespielteTage(); break;
                case WeeklyQuestType.GainLevels:
                    q.Progress = Mathf.Max(0, PlayerLevelManager.CurrentLevel
                                            - PlayerPrefs.GetInt(PrefKeyLevel, PlayerLevelManager.CurrentLevel));
                    break;
            }
        }
        Save();
        OnQuestsChanged?.Invoke();
    }

    public static void OnSpecialModeTriggered()  => Erhoehe(WeeklyQuestType.TriggerSpecialModes, 1);
    public static void OnDiamondCollected(int anzahl = 1) => Erhoehe(WeeklyQuestType.CollectDiamonds, anzahl);
    public static void OnStickerEarned(int anzahl = 1)    => Erhoehe(WeeklyQuestType.EarnStickers, anzahl);

    /// <summary>Hoechste erreichte Combo einer Runde -- nicht aufaddieren, sondern das Maximum halten.</summary>
    public static void OnComboReached(int combo)
    {
        EnsureLoaded();
        bool geaendert = false;
        foreach (var q in _quests)
            if (!q.Claimed && q.Type == WeeklyQuestType.ReachCombo && combo > q.Progress)
            { q.Progress = combo; geaendert = true; }
        if (geaendert) { Save(); OnQuestsChanged?.Invoke(); }
    }

    public static void OnColorDestroyed(int colorIndex)
    {
        EnsureLoaded();
        bool geaendert = false;
        foreach (var q in _quests)
            if (!q.Claimed && q.Type == WeeklyQuestType.DestroyColor && q.ColorIndex == colorIndex)
            { q.Progress++; geaendert = true; }
        if (geaendert) { Save(); OnQuestsChanged?.Invoke(); }
    }

    static void Erhoehe(WeeklyQuestType typ, int anzahl)
    {
        EnsureLoaded();
        bool geaendert = false;
        foreach (var q in _quests)
            if (!q.Claimed && q.Type == typ) { q.Progress += anzahl; geaendert = true; }
        if (geaendert) { Save(); OnQuestsChanged?.Invoke(); }
    }

    // ── Selbstverdrahtung ─────────────────────────────────────────────────────
    // Haengt sich beim Spielstart an die vorhandenen Ereignisse, statt in GameplayCode verstreute
    // Aufrufe einzubauen. Laeuft einmal pro Prozess; die Ereignisse sind alle static, es gibt also
    // kein Objekt, dessen Lebensdauer hier stoeren koennte.
    static bool _verdrahtet;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Verdrahten()
    {
        if (_verdrahtet) return;
        _verdrahtet = true;

        MixedPointSpawner.OnColorHitRegistered += farbe => OnColorDestroyed((int)farbe);
        // Das Ereignis feuert einmal je eingesammeltem Diamanten (der uebergebene Wert ist der
        // Phasen-Zwischenstand, nicht die Anzahl) -- deshalb immer um genau 1 erhoehen.
        MixedPointSpawner.OnDiamondCollected += (_, __) => OnDiamondCollected(1);
        ComboManager.OnComboChanged          += OnComboReached;
        StickerManager.OnStickerGranted      += _ => OnStickerEarned(1);
    }

    // ── Intern ────────────────────────────────────────────────────────────────

    /// <summary>Kennung der laufenden Woche: Datum des Sonntags, an dem sie endet. Dadurch wechselt
    /// die Kennung genau beim gewollten Zeitpunkt, ohne mit ISO-Wochennummern zu ringen.</summary>
    static string AktuelleWochenKennung() => CurrentWeekEnd().ToString("yyyy-MM-dd");

    static void EnsureLoaded()
    {
        string woche = AktuelleWochenKennung();
        if (_quests != null && _quests.Length == QuestCount &&
            PlayerPrefs.GetString(PrefKeyWeek, "") == woche) return;

        if (PlayerPrefs.GetString(PrefKeyWeek, "") == woche)
        {
            string json = PlayerPrefs.GetString(PrefKeyData, "");
            if (!string.IsNullOrEmpty(json))
            {
                var huelle = JsonUtility.FromJson<Huelle>(json);
                if (huelle?.quests != null && huelle.quests.Length == QuestCount)
                {
                    _quests = huelle.quests;
                    return;
                }
            }
        }

        NeueWoche(woche);
    }

    static void NeueWoche(string woche)
    {
        // Nicht abgeholte Belohnungen verfallen hier ersatzlos -- das ist so gewollt.
        var gezogen = new List<WeeklyQuestData>(QuestCount);
        var uebrig  = new List<WeeklyQuestData>(Pool);

        // Zufall an die Woche gekoppelt: jedes Geraet desselben Spielers zieht dieselben Aufgaben,
        // und ein Neustart der App wuerfelt nicht neu.
        var rng = new System.Random(woche.GetHashCode());
        while (gezogen.Count < QuestCount && uebrig.Count > 0)
        {
            int i = rng.Next(uebrig.Count);
            var vorlage = uebrig[i];
            uebrig.RemoveAt(i);
            // Keine zwei Aufgaben desselben Typs in einer Woche.
            if (gezogen.Exists(g => g.Type == vorlage.Type)) continue;
            gezogen.Add(new WeeklyQuestData
            {
                Type        = vorlage.Type,
                Target      = TestZiele ? Mathf.Min(vorlage.Target, TestZielObergrenze) : vorlage.Target,
                Splinters   = vorlage.Splinters,
                ColorIndex  = vorlage.ColorIndex,
                Progress    = 0,
                Claimed     = false
            });
        }

        _quests = gezogen.ToArray();
        PlayerPrefs.SetString(PrefKeyWeek, woche);
        PlayerPrefs.SetInt(PrefKeyDays, 0);
        // Level-Aufstiege zaehlen wir als Differenz zum Stand bei Wochenbeginn -- der
        // PlayerLevelManager hat kein Aufstiegs-Ereignis, an das man sich haengen koennte.
        PlayerPrefs.SetInt(PrefKeyLevel, PlayerLevelManager.CurrentLevel);
        Save();
        Debug.Log($"[Weeklies] new week until {woche}: " + string.Join(" | ", Array.ConvertAll(_quests, q => q.Title)));
    }

    static void Save()
    {
        if (_quests == null) return;
        PlayerPrefs.SetString(PrefKeyData, JsonUtility.ToJson(new Huelle { quests = _quests }));
        PlayerPrefs.Save();
    }

    // JsonUtility kann keine blanken Arrays -- deshalb diese Huelle.
    [Serializable] class Huelle { public WeeklyQuestData[] quests; }

    static void MarkeTagGespielt()
    {
        int maske = PlayerPrefs.GetInt(PrefKeyDays, 0);
        int bit   = 1 << (int)DateTime.Now.DayOfWeek;
        if ((maske & bit) != 0) return;
        PlayerPrefs.SetInt(PrefKeyDays, maske | bit);
    }

    static int GespielteTage()
    {
        int maske = PlayerPrefs.GetInt(PrefKeyDays, 0), n = 0;
        for (int i = 0; i < 7; i++) if ((maske & (1 << i)) != 0) n++;
        return n;
    }
}
