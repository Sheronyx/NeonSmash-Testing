# NeonSmash — Nightly Backlog

Offene Aufgaben für Night Shifts, nach Priorität. Nicht jeder Eintrag ist eine Entscheidung — Status ist vermerkt.

## Hoch (kritisch/blockierend für Release oder bekannter Bug)

- **[BUG, bestätigt 2026-07-27]** `NEONSMASH_PROD` Define ist zwischen `Assets/Settings/Build Profiles/Android Release.asset` und `iOS.asset` vertauscht (hängt jeweils am falschen Platform-Eintrag). Vor jedem Store-Build in Unity prüfen — sonst geht Android/iOS Prod mit falscher Firebase-Umgebung raus. Nicht automatisch fixbar ohne Unity-Editor-Zugriff auf die Asset-Referenzen; für Night Shift geeignet, sobald Unity offen ist.
- **[OFFEN]** iOS AdMob Interstitial — letzter bekannter Stand: Propagation/Account-seitig, Code als korrekt verifiziert. Bei nächster Gelegenheit mit echtem Gerät erneut testen (siehe `project_admob_plan`).

## Mittel (hoher Nutzen, vertretbarer Aufwand)

- **[BUG, gefunden 2026-08-19]** `DailyRewardManager`: Kommentar sagt "Day 1–7, then repeats from Day 1", aber `TodayRewardAmount`/`ClaimTodayReward` clampen den Streak-Index bei 7 statt zu wrappen (`Mathf.Clamp` statt Modulo) → Streak bleibt ab Tag 7 für immer bei Reward-Stufe 7 hängen, kein Zyklus zurück zu Tag 1. Bewusst NICHT in dieser Night Shift gefixt, weil unklar ist, ob `CurrentStreak` (PlayerPrefs `daily_streak`) auch für andere Zwecke (`AchievementManager.OnStreakReached`) als reiner Fortlaufzähler gebraucht wird — ein Wraparound nur im Reward-Index vs. auch im gespeicherten Streak-Wert hat unterschiedliche Nebenwirkungen auf Achievements. Braucht kurze Design-Entscheidung von dir, dann klein umsetzbar (`Assets/Scripts/Services/DailyRewardManager.cs:26-51`).
- **[Robustheit, gefunden 2026-08-19]** `DreamEnergyManager`: Cloud-Saves laufen fire-and-forget (`_ = SaveToCloudAsync()`), Fehler werden nur geloggt, nie retried. Bei Netzwerkausfall (heute Abend im Editor-Log beobachtet) driftet der lokale Dream-Energy-Stand dauerhaft von der Cloud-Kopie ab, ohne dass es auffällt. Braucht Dirty-Flag + Retry beim nächsten App-Resume — kein Quick-Fix, daher zurückgestellt (`Assets/Scripts/Services/DreamEnergyManager.cs:124,135,187-190`).

- `PhaseManager`-GameObject in `GameScene_InfinityMode` fehlt noch (Szene-Setup, keine Codeänderung) — 12-Phasen-System ist sonst komplett fertig aber inaktiv.
- `DiamondPoint`-Prefab bauen (Visual + Collider2D + Script) für Diamant-Collectibles ab Phase 9/11.
- Diagnose-Logs in `AdManager.cs` waren zum 2026-06-24 bereits ausgedünnt — bei Gelegenheit erneut prüfen, ob seither wieder Debug-Rauschen reingekommen ist.

## Niedrig / Recherche / Design (kein Blocker)

- Flow/Pacing-Redesign (Special Modes als Ruhephase, Ease-Ramp bei Phasenwechseln) — Diskussionsstand vom 2026-07-29, noch nicht umgesetzt. Braucht zuerst Klärung: gibt es reales Playtester-Signal für "zu stressig"?
- Magnet Mode (echte Mechanik statt Gravity-Platzhalter für Grün) — späteres Feature.
- Phase 13 Endless-Feindesign — bewusst zurückgestellt.
- 3D-Tap/Swipe-Elemente (ob Umstellung auf 3D für Gameplay-Elemente selbst sinnvoll ist) — als riskant geflaggt, braucht Prototyp/Test vor Entscheidung, nicht nur Fairy-Skins.

## Aus Night Shift 2026-08-19 hinzugekommen

Siehe [reports/nightly/2026-08-19.md](../reports/nightly/2026-08-19.md) für Details zu Findings aus Code-QA-Scan und Recherche dieser Nacht.
