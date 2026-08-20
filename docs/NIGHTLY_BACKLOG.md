# NeonSmash — Nightly Backlog

Offene Aufgaben für Night Shifts, nach Priorität. Nicht jeder Eintrag ist eine Entscheidung — Status ist vermerkt.

## Hoch (kritisch/blockierend für Release oder bekannter Bug)

- **[BUG-Beschreibung korrigiert, geprüft 2026-08-20 Zyklus 3]** `NEONSMASH_PROD` Define zwischen den Build-Profile-Assets: Bei Dateiinspektion (Unity lief, `Unity_GetConsoleLogs` bestätigte keine Fehler) zeigte sich, dass der ursprünglich gemeldete "Vertausch" in `Assets/Settings/Build Profiles/Android Release.asset` / `iOS.asset` tatsächlich in `m_PlayerSettingsYaml` liegt — das ist nur eine inerte Snapshot-Kopie alter globaler PlayerSettings, **nicht** die aktive Konfiguration (beide Profile haben `m_HasScriptingDefines: 0` und `m_ScriptingDefines: []`, überschreiben also nichts). Die tatsächlich wirksame Quelle ist `ProjectSettings/ProjectSettings.asset` → `scriptingDefineSymbols` (global, ein Eintrag pro Plattform). Dort ist **aktuell weder für Android noch iPhone `NEONSMASH_PROD` oder `NEON_ANALYTICS_TEST` gesetzt** — laut Code-Konvention (`AdConfig.cs`, `UgsBootstrap.cs`: "ohne Define → development") ist das der sichere Default, aber es bedeutet: vor jedem echten Store-Release muss `NEONSMASH_PROD` manuell in Player Settings → Scripting Define Symbols für die Zielplattform gesetzt werden — es gibt keinen Automatismus und keinen Bug, der das verhindert. Alte Formulierung ("vertauscht") war irreführend und ist hiermit ersetzt. Bleibt Release-Checkliste-Punkt (siehe `project_release_checklist`), kein Code-Bug mehr.
- **[OFFEN]** iOS AdMob Interstitial — letzter bekannter Stand: Propagation/Account-seitig, Code als korrekt verifiziert. Bei nächster Gelegenheit mit echtem Gerät erneut testen (siehe `project_admob_plan`).

## Mittel (hoher Nutzen, vertretbarer Aufwand)

- **[BUG, gefunden 2026-08-19, GEFIXT 2026-08-20]** `DailyRewardManager`: Reward-Index clampte bei 7 statt zu wrappen → Streak blieb ab Tag 7 für immer bei Reward-Stufe 7 hängen. Fix in Commit `2c169a7a`: neue `RewardTierIndex()`-Funktion wrapped den Reward-Index per Modulo, der rohe Streak-Zähler (PlayerPrefs/Cloud, für `AchievementManager.OnStreakReached`) bleibt unclamped/monoton wachsend — einzige bestehenden Streak-Achievements sind Streak3/Streak7, unverändertes Verhalten dort. Design-Entscheidung: Zyklus und Rohzähler trennen statt beide zu wrappen.
- **[Robustheit, gefunden 2026-08-19]** `DreamEnergyManager`: Cloud-Saves laufen fire-and-forget (`_ = SaveToCloudAsync()`), Fehler werden nur geloggt, nie retried. Bei Netzwerkausfall (heute Abend im Editor-Log beobachtet) driftet der lokale Dream-Energy-Stand dauerhaft von der Cloud-Kopie ab, ohne dass es auffällt. Braucht Dirty-Flag + Retry beim nächsten App-Resume — kein Quick-Fix, daher zurückgestellt (`Assets/Scripts/Services/DreamEnergyManager.cs:124,135,187-190`).

- **[Idee, Recherche 2026-08-20]** Streak-Freeze für `DailyRewardManager`: aktuell resettet ein verpasster Tag den Streak hart auf 1, Research zu Retention-Mechaniken empfiehlt explizit einen Freeze-Mechanismus, um Frust/Investitionsverlust abzufedern (siehe `docs/RESEARCH.md` Abschnitt "Daily-Reward/Streak-Loop Best Practices"). Braucht neue Mechanik (Freeze-Charge verdienen/per Rewarded-Ad) + UI, kein Quick-Fix.
- **[Beschreibung korrigiert, geprüft 2026-08-20 Zyklus 4]** `PhaseManager`-GameObject in `GameScene_InfinityMode`: entgegen dem alten Backlog-Text ("fehlt noch") ist das GameObject "Phase Manager" tatsächlich bereits in der Szene vorhanden und aktiv (`m_IsActive: 1`, `m_Enabled: 1`, `spawner`-Referenz gesetzt, Zeile ~2130 in `GameScene_InfinityMode.unity`). Per Live-Play-Mode-Test via Unity-MCP bestätigt: `PhaseManager.Instance` ist zur Laufzeit nicht null, ein `PhaseManager`-Component existiert genau einmal in der Szene. Vermutlich zwischenzeitlich durch manuelle User-Arbeit ergänzt (kein Night-Shift-Commit dafür auffindbar). Das 12-Phasen-System ist damit **nicht mehr inaktiv** — falls das unbeabsichtigt ist, sollte der User das prüfen, da es Balance/Flow direkt beeinflusst. Kein Handlungsbedarf durch Night Shift, nur Beobachtung/Korrektur.
- `DiamondPoint`-Prefab bauen (Visual + Collider2D + Script) für Diamant-Collectibles ab Phase 9/11.
- **[Erledigt/geprüft 2026-08-20 Zyklus 3]** Diagnose-Logs in `AdManager.cs` erneut geprüft: aktuell 8 `Debug.Log`/`Debug.LogWarning`-Aufrufe, alle mit `[Ads]`-Präfix, ausschließlich Fehler-/Statuswechsel-relevant (Consent-Fehler, Ad-Laden fehlgeschlagen, Init-Status) — kein Rauschen, kein Handlungsbedarf. Punkt kann aus dem Backlog.

## Niedrig / Recherche / Design (kein Blocker)

- Flow/Pacing-Redesign (Special Modes als Ruhephase, Ease-Ramp bei Phasenwechseln) — Diskussionsstand vom 2026-07-29, noch nicht umgesetzt. Braucht zuerst Klärung: gibt es reales Playtester-Signal für "zu stressig"?
- Magnet Mode (echte Mechanik statt Gravity-Platzhalter für Grün) — späteres Feature.
- Phase 13 Endless-Feindesign — bewusst zurückgestellt.
- 3D-Tap/Swipe-Elemente (ob Umstellung auf 3D für Gameplay-Elemente selbst sinnvoll ist) — als riskant geflaggt, braucht Prototyp/Test vor Entscheidung, nicht nur Fairy-Skins.

## Aus Night Shift 2026-08-19 hinzugekommen

Siehe [reports/nightly/2026-08-19.md](../reports/nightly/2026-08-19.md) für Details zu Findings aus Code-QA-Scan und Recherche dieser Nacht.
