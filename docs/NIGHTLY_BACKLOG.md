# NeonSmash — Nightly Backlog

Offene Aufgaben für Night Shifts, nach Priorität. Nicht jeder Eintrag ist eine Entscheidung — Status ist vermerkt.

## Hoch (kritisch/blockierend für Release oder bekannter Bug)

- **[BUG, gefunden & gefixt 2026-08-22 Zyklus 17]** `LobbyHandler.TickHeartbeat()` (Multiplayer, `async void`, läuft jeden Frame) hatte kein try/catch um den Heartbeat-Call — bei abgelaufener Lobby/Netzwerkfehler während eines Matches hätte das alle 15s eine unbehandelte Exception geworfen. Fix analog zum bereits vorhandenen `DeleteAsync`-Muster im selben File, Commit `f7ab50d1`. Kompiliert fehlerfrei; NICHT live in einem echten Multiplayer-Match getestet (bräuchte zweiten Client + gezielte Netzwerkunterbrechung).
- **[KRITISCHER BUG, gefunden & gefixt 2026-08-22 Zyklus 14, live verifiziert Zyklus 29]** `DiamondManager`/`DiamondSplinterManager` wurden nie beim App-Start aus der Cloud geladen (`UgsBootstrap.cs` rief nur Tutorial/DreamEnergy/DailyReward/Achievement). Ein Geräte-/Reinstall-Wechsel hat den kompletten Diamanten-/Splitter-Stand verloren — beide sind aktive Ingame-Währung (`ShopInventory.cs`, `IAPManager.cs`). Fix: beide `LoadFromCloudAsync()`-Aufrufe ergänzt, Commit `a0b8ecd5`. Zyklus 29: `BootstrapScene` mit echter UGS-Verbindung (Anonymous Sign-In) in Play Mode durchlaufen — beide Load-Aufrufe liefen fehlerfrei durch den echten Ablauf, 0 Errors. Damit über reine try/catch-Resilienz hinaus bestätigt.

- **[BUG, gefunden & gefixt 2026-08-21 Zyklus 6]** Pause während Life-Lost-Ramp-Up hob die Pause faktisch wieder auf — `LivesManager.RampUpTimeScale()` überschrieb `Time.timeScale` weiter, während `PauseMenuController.ShowPauseMenu()` es auf 0 gesetzt hatte. Fix: `LivesManager.CancelRampUp()`, aufgerufen aus `ShowPauseMenu()`. Commit `b4fe313b`. Live im Play Mode verifiziert (Unity-MCP, frame-genaues Polling über 60 Frames, kein Drift mehr). Kein automatisierter PlayMode-Test ergänzt — EditMode kann Coroutine-Timing nicht abbilden.
- **[BUG, gefunden & gefixt 2026-08-21 Zyklus 7, live verifiziert Zyklus 8]** Identisches Muster in `SequenceManager.Co_PlayEffect()` gefunden (gezielte Suche nach allen `Time.timeScale`-Zuweisungen im Projekt) — Combo-Freeze-Frame setzte `Time.timeScale=1f` nach Ablauf unconditional zurück, hätte eine während des Freeze aktive Pause aufgehoben. Fix: Guard `if (!PauseMenuController.IsPaused)`, Commit `7b2dadef`. Live im Play Mode nachgestellt (Unity-MCP, temporäres `SequenceDefinition` mit 1-Schritt-Sequenz + Dummy-Combo-Prefab, `RegisterHit` löst Freeze aus, Pause währenddessen simuliert): über 181 Frames nach `ShowPauseMenu()` blieb `timeScale=0`, obwohl `effectDuration` (1s) längst abgelaufen war — Fix bestätigt.

- **[BUG-Beschreibung korrigiert, geprüft 2026-08-20 Zyklus 3]** `NEONSMASH_PROD` Define zwischen den Build-Profile-Assets: Bei Dateiinspektion (Unity lief, `Unity_GetConsoleLogs` bestätigte keine Fehler) zeigte sich, dass der ursprünglich gemeldete "Vertausch" in `Assets/Settings/Build Profiles/Android Release.asset` / `iOS.asset` tatsächlich in `m_PlayerSettingsYaml` liegt — das ist nur eine inerte Snapshot-Kopie alter globaler PlayerSettings, **nicht** die aktive Konfiguration (beide Profile haben `m_HasScriptingDefines: 0` und `m_ScriptingDefines: []`, überschreiben also nichts). Die tatsächlich wirksame Quelle ist `ProjectSettings/ProjectSettings.asset` → `scriptingDefineSymbols` (global, ein Eintrag pro Plattform). Dort ist **aktuell weder für Android noch iPhone `NEONSMASH_PROD` oder `NEON_ANALYTICS_TEST` gesetzt** — laut Code-Konvention (`AdConfig.cs`, `UgsBootstrap.cs`: "ohne Define → development") ist das der sichere Default, aber es bedeutet: vor jedem echten Store-Release muss `NEONSMASH_PROD` manuell in Player Settings → Scripting Define Symbols für die Zielplattform gesetzt werden — es gibt keinen Automatismus und keinen Bug, der das verhindert. Alte Formulierung ("vertauscht") war irreführend und ist hiermit ersetzt. Bleibt Release-Checkliste-Punkt (siehe `project_release_checklist`), kein Code-Bug mehr.
- **[OFFEN]** iOS AdMob Interstitial — letzter bekannter Stand: Propagation/Account-seitig, Code als korrekt verifiziert. Bei nächster Gelegenheit mit echtem Gerät erneut testen (siehe `project_admob_plan`).

## Vom User priorisiert (2026-08-21, für nächste Night Shift)

- **[TEILWEISE ERLEDIGT, Night Shift 2026-08-22 Zyklus 15]** Icon-Artwork selbst erstellt (`Assets/001 Fairy World/Elements/Fairy Progress Icons/`, Commit `77df1e68`) — Blatt/Tropfen/Kristall als 128x128 Sprites, importiert und konfiguriert. **Weiterhin blockiert:** die eigentliche Verdrahtung in der Szene (Icon-GameObjects neben den Fill-Bars in `ColorProgressUI`/`GameScene_InfinityMode.unity`) — Szene hat weiterhin einen großen uncommitteten WIP-Diff (manuelle 3D-Charakter-Arbeit), wird laut Standing-Regel nicht angefasst. Sobald WIP geklärt: Icons sind fertig zum Drag&Drop in die Image-Felder.
- **[TEILWEISE ERLEDIGT, Night Shift 2026-08-22 Zyklus 18]** Reward-Preview: Code-Unterstützung in `ColorProgressUI.cs` (optionale, nullable Felder + Schwellenwert-Logik) und Sparkle-Icon erstellt, Commit `7356ea98`. **Weiterhin blockiert:** Zuweisung der Icons in der Szene selbst (`GameScene_InfinityMode.unity`, weiterhin uncommitteter WIP).
- **[ERLEDIGT, Night Shift 2026-08-21]** Streak-Freeze für `DailyRewardManager` umgesetzt — reiner Code-Task, keine Szene betroffen. Siehe Commit `4180f37d`. UI-Anbindung (Anzeige der Charges, Freeze-Button) bleibt offen, ebenfalls Scene-WIP-blockiert.

## Mittel (hoher Nutzen, vertretbarer Aufwand)

- **[BUG, gefunden 2026-08-19, GEFIXT 2026-08-20]** `DailyRewardManager`: Reward-Index clampte bei 7 statt zu wrappen → Streak blieb ab Tag 7 für immer bei Reward-Stufe 7 hängen. Fix in Commit `2c169a7a`: neue `RewardTierIndex()`-Funktion wrapped den Reward-Index per Modulo, der rohe Streak-Zähler (PlayerPrefs/Cloud, für `AchievementManager.OnStreakReached`) bleibt unclamped/monoton wachsend — einzige bestehenden Streak-Achievements sind Streak3/Streak7, unverändertes Verhalten dort. Design-Entscheidung: Zyklus und Rohzähler trennen statt beide zu wrappen.
- **[ERLEDIGT, Night Shift 2026-08-21 Zyklus 4]** `DreamEnergyManager`-Cloud-Save-Drift behoben — Dirty-Flag + Retry bei `LoadFromCloudAsync` (App-Resume), Commit `53e0ba17`. War zuvor als "kein Quick-Fix" zurückgestellt, ließ sich bei genauerer Betrachtung aber klein und isoliert umsetzen (kein UI/Scene nötig). 11/11 EditMode-Tests grün.
- **[ERLEDIGT, Night Shift 2026-08-21 Zyklus 10]** Gleicher Cloud-Save-Drift-Bug auch in `DiamondManager` und `DiamondSplinterManager` gefunden (identische Speicher-/Sync-Struktur) und mit demselben Dirty-Flag+Retry-Muster gefixt, Commit `0c5886f3`. Funktional im Editor verifiziert (Fehlschlag→Dirty-Flag→Retry→No-Op bei sauberem Zustand), echter Spielerstand danach unverändert wiederhergestellt.
- **[ERLEDIGT, Night Shift 2026-08-21 Zyklus 11]** Sweep abgeschlossen: `AchievementManager` (letzter verbliebener Manager mit diesem Muster, zwei unabhängige Save-Pfade) ebenfalls gefixt, Commit `44b5d8a4`. `TutorialProgress.cs` bewusst ausgenommen — funktional tot (`IsTutorialCompleted` hart auf `true`). Alle vier betroffenen Manager (DreamEnergy/Diamond/DiamondSplinter/Achievement) haben jetzt konsistent Dirty-Flag+Retry.
- **[ERLEDIGT, Night Shift 2026-08-21 Zyklus 12]** Automatisierte EditMode-Tests für die drei zuvor nur manuell verifizierten Cloud-Dirty-Fixes nachgezogen (Diamond/DiamondSplinter/Achievement), Commit `dbf9b251`. 9/9 grün, Test-Isolation bestätigt (Realstand vor/nach unverändert). Gesamte Test-Suite jetzt: 8 (DailyReward) + 3 (DreamEnergy) + 9 (Diamond/Splinter/Achievement) = 20 EditMode-Tests für die Services-Schicht.
- **[Test-Isolations-Bug, gefunden & gefixt 2026-08-21 Zyklus 4]** `DailyRewardManagerTests.cs` (aus Zyklus 2) hat `DreamEnergyManager`-PlayerPrefs nicht gesichert/wiederhergestellt — jeder Testlauf hat den echten Editor-`dream_energy_balance` dauerhaft erhöht (auf 50100 aufgelaufen, keine Baseline vor Zyklus 2 aufgezeichnet, daher nicht mehr exakt rückrechenbar). Fix in Commit `53e0ba17`. **Für den User:** falls der Editor-PlayerPrefs-Dream-Energy-Stand (50100) nicht dem erwarteten Testdaten-Stand entspricht, manuell über `PlayerPrefs.DeleteKey("dream_energy_balance")` im Editor zurücksetzen — betrifft nur lokale Editor-Testdaten, keine echten Spielerdaten.

- ~~Streak-Freeze für `DailyRewardManager`~~ — siehe "Vom User priorisiert" oben, Backend erledigt in Night Shift 2026-08-21 (Commit `4180f37d`), UI-Teil bleibt dort als offener Punkt.
- **[ERLEDIGT, Night Shift 2026-08-21 Zyklus 2]** EditMode-Tests für `DailyRewardManager` ergänzt (`Assets/Editor/Tests/DailyRewardManagerTests.cs`, Commit `c2090fb5`) — 8/8 Tests grün über `TestRunnerApi` im laufenden Editor verifiziert, deckt Streak-Freeze, Reward-Tier-Wrap und Already-Claimed-Fall ab.
- **[Beschreibung korrigiert, geprüft 2026-08-20 Zyklus 4]** `PhaseManager`-GameObject in `GameScene_InfinityMode`: entgegen dem alten Backlog-Text ("fehlt noch") ist das GameObject "Phase Manager" tatsächlich bereits in der Szene vorhanden und aktiv (`m_IsActive: 1`, `m_Enabled: 1`, `spawner`-Referenz gesetzt, Zeile ~2130 in `GameScene_InfinityMode.unity`). Per Live-Play-Mode-Test via Unity-MCP bestätigt: `PhaseManager.Instance` ist zur Laufzeit nicht null, ein `PhaseManager`-Component existiert genau einmal in der Szene. Vermutlich zwischenzeitlich durch manuelle User-Arbeit ergänzt (kein Night-Shift-Commit dafür auffindbar). Das 12-Phasen-System ist damit **nicht mehr inaktiv** — falls das unbeabsichtigt ist, sollte der User das prüfen, da es Balance/Flow direkt beeinflusst. Kein Handlungsbedarf durch Night Shift, nur Beobachtung/Korrektur.
- **[Beschreibung korrigiert, geprüft 2026-08-20 Fortsetzung Zyklus 6]** `DiamondPoint`-Prefab: entgegen dem alten Backlog-Text ("bauen") existiert `Assets/Prefabs/Diamond Point Collectable.prefab` bereits vollständig — `SpriteRenderer`, `CapsuleCollider2D`, sowie die Komponenten `DiamondPoint`, `PointPulse` und `SpawnGrowTarget` (per GUID-Abgleich in `Assets/Scripts/Gameplay/Logic/` verifiziert). Das Prefab ist zudem in `Assets/Scenes/GameScenes/GameScene_InfinityMode.unity:3173` als `diamondPrefab`-Feld verdrahtet (GUID `3a9f3f06d50854d3db2b00f79c831aa8` referenziert). Punkt kann aus dem Backlog, kein Handlungsbedarf. Vermutlich zwischenzeitlich durch manuelle User-Arbeit ergänzt.
- **[Erledigt/geprüft 2026-08-20 Zyklus 3]** Diagnose-Logs in `AdManager.cs` erneut geprüft: aktuell 8 `Debug.Log`/`Debug.LogWarning`-Aufrufe, alle mit `[Ads]`-Präfix, ausschließlich Fehler-/Statuswechsel-relevant (Consent-Fehler, Ad-Laden fehlgeschlagen, Init-Status) — kein Rauschen, kein Handlungsbedarf. Punkt kann aus dem Backlog.

## Niedrig / Recherche / Design (kein Blocker)

- **[Idee, 2026-08-22]** `life_lost`/Miss-Rate-Analytics (Routine Abschnitt 22 nennt "Tap-Fehlerrate; Swipe-Fehlerrate" explizit als Messwert). Aktuell komplett ungetrackt. Bewusst NICHT umgesetzt: `LivesManager.LoseLife()` wird von vielen verschiedenen Special-Mode-Skripten ohne "Ursache"-Parameter aufgerufen — eine sinnvolle Tap-vs-Swipe-Aufschlüsselung bräuchte Signatur-Änderungen an mehreren Call-Sites, was mehr Produktentscheidung (welche Granularität gewünscht?) als reiner Bugfix ist. Nur ein reines "life_lost"-Zählevent ohne Ursache wäre wenig aussagekräftig. Als Idee vermerkt statt spekulativ umgesetzt.

- **[QA-Fund, nicht night-shift-verursacht, 2026-08-22 Zyklus 18]** `Assets/Bundles/01 Default/Prefabs/Top Bar Default.prefab` wirft bei jedem Reimport "Transform child can't be loaded"/"Immediate cast failed from GameObject to Transform" (12x). Datei ist seit 20.08. unverändert (nicht Teil des heutigen WIP, nicht von mir angefasst) — vermutlich alte verwaiste FileID-Einträge aus einer früheren Bearbeitung. Prefab lädt trotzdem korrekt (3 Kinder erhalten), funktional aktuell kein bekannter Schaden. Nicht selbst repariert (YAML-Chirurgie an fremdem WIP-Bundle-Prefab zu riskant ohne genaue Kenntnis der beabsichtigten Struktur) — bei Gelegenheit im Editor öffnen und einmal manuell speichern lassen, das bereinigt solche verwaisten Einträge meist automatisch.

- **[Vorschlag, Recherche 2026-08-22]** Zeitlich begrenztes Leaderboard (täglich/wöchentlich) zusätzlich zu den bestehenden 2 All-Time-Boards — siehe `docs/RESEARCH.md` Abschnitt "Leaderboard-Design für Retention". Braucht neue UGS-Leaderboard-IDs + Reset-Logik, kein Quick-Fix.
- **[Vorschlag, Recherche 2026-08-22]** Dritte Diamant-IAP-Stufe (~15–20 Diamanten) zwischen den bestehenden 5er/50er-Paketen als Decoy-Option ergänzen — siehe `docs/RESEARCH.md` Abschnitt "IAP-Pricing-Patterns". Braucht neues Store-Produkt (App Store Connect/Play Console) + neues `ShopItem`-Asset, ist eine Preis-/Business-Entscheidung, keine Night-Shift-Aufgabe.

- Flow/Pacing-Redesign (Special Modes als Ruhephase, Ease-Ramp bei Phasenwechseln) — Diskussionsstand vom 2026-07-29, noch nicht umgesetzt. Braucht zuerst Klärung: gibt es reales Playtester-Signal für "zu stressig"?
- Magnet Mode (echte Mechanik statt Gravity-Platzhalter für Grün) — späteres Feature.
- Phase 13 Endless-Feindesign — bewusst zurückgestellt.
- 3D-Tap/Swipe-Elemente (ob Umstellung auf 3D für Gameplay-Elemente selbst sinnvoll ist) — als riskant geflaggt, braucht Prototyp/Test vor Entscheidung, nicht nur Fairy-Skins.

- **[Idee, Recherche 2026-08-21]** Reward-Preview am Feen-Energie-Füllbalken (`ColorProgressUI.cs`): laut UI-Pattern-Recherche sollte ein Fortschrittsbalken andeuten, was bei Erreichen des Ziels wartet, statt nur reinen Füllstand zu zeigen. NeonSmashs 3 Feen-Balken zeigen aktuell nur Fortschritt ohne Preview auf den Special-Mode-Trigger. Kleines Icon/Glow am Balkenende als möglicher nächster Schritt — siehe `docs/RESEARCH.md` Abschnitt "UI-Pattern für Progress-/Meta-Progression-Bars". Kein Quick-Fix (UI + Szene), daher nicht umgesetzt.

## Aus Night Shift 2026-08-22 (Zyklus 32) hinzugekommen

- **[ERLEDIGT, Cleanup]** Totes Duplikat `LeaderboardIds.cs` entfernt (definierte `infinity_highscore` ein zweites Mal, unbenutzt neben `LeaderboardApi.InfinityId`). Commit `1e53e6f2`.

## Aus Night Shift 2026-08-22 (Zyklus 31) hinzugekommen

- **[ERLEDIGT, stärkste Verifikation]** Gleicher Erfolgsfall-Test mit echter Cloud-Verbindung auch für `DiamondManager`, `DiamondSplinterManager` und `AchievementManager` durchgeführt (Fortsetzung Zyklus 30) — alle vier Dirty-Flags nach echtem Save auf 0, Cloud-Logs bestätigen tatsächliche Speicherung ("Cloud gespeichert: 5"/"10"). Damit sind jetzt ALLE VIER Cloud-Dirty-Fixes dieser Night Shift end-to-end mit echtem Backend verifiziert, nicht nur der Fehlerpfad im Editor-Mock.

## Aus Night Shift 2026-08-22 (Zyklus 30) hinzugekommen

- **[ERLEDIGT, stärkste Verifikation]** Dirty-Flag-Retry-Mechanismus (`DreamEnergyManager.RetryPendingCloudSaveIfNeeded`) mit ECHTER UGS-Cloud-Verbindung im Erfolgsfall getestet (nicht nur Fehlerfall wie zuvor): Dirty-Flag manuell auf 1 gesetzt, Retry aufgerufen → echter Cloud-Save gelang, Flag korrekt auf 0 zurückgesetzt. Log bestätigt: "[DreamEnergy] Cloud gespeichert: 50100". Damit ist der Kernmechanismus aller vier Cloud-Dirty-Fixes (Zyklus 4/10/11) jetzt End-to-End mit echtem Backend bestätigt, nicht nur die Fehlerpfad-Resilienz.

## Aus Night Shift 2026-08-22 (Zyklus 29) hinzugekommen

- **[ERLEDIGT, Play-Mode-Verifikation]** `BootstrapScene` end-to-end in Play Mode getestet (echte UGS-Verbindung, kein Editor-Mock): Anonymous Sign-In erfolgreich, `DiamondManager`/`DiamondSplinterManager.LoadFromCloudAsync()` (heutiger kritischer Fix aus Zyklus 14) liefen ohne Fehler durch den echten Bootstrap-Ablauf, 0 Errors insgesamt. Transiente "2 EventSystems/2 AudioListeners"-Warnung beim Bootstrap→MainMenu-Übergang beobachtet, aber selbstheilend (Endzustand nach Übergang: exakt 1 von jedem) — kein anhaltender Bug, nicht weiterverfolgt.

## Aus Night Shift 2026-08-22 (Zyklus 24) hinzugekommen

- **[BUG, gefunden & gefixt]** `DebugResetPrefs.cs` (Strg+R löscht `PlayerPrefs.DeleteAll()`) fehlte der `#if UNITY_EDITOR || DEVELOPMENT_BUILD`-Guard, den `RewardDebugHelper.cs` für dieselbe Art Aktion bereits hat. Reales Risiko, falls das Skript je wieder einem GameObject zugewiesen wird und in einen Production-Build landet. Aktuell inert (nicht referenziert), Guard trotzdem ergänzt statt nur zu löschen. Commit `cb693b51`.

## Aus Night Shift 2026-08-22 (Zyklus 22) hinzugekommen

- **[ERLEDIGT]** `InAppReviewManager` speicherte PlayCount/ReviewShown nie mit `PlayerPrefs.Save()` — bei App-Crash/Kill vor dem nächsten automatischen Flush wäre der Status verloren gegangen. Fix, Commit `27cc234b`. Niedrige Kritikalität (keine Spielerwährung betroffen).

## Aus Night Shift 2026-08-22 (Zyklus 21) hinzugekommen

- **[ERLEDIGT]** Ads/IAP-Analytics-Lücke geschlossen — `AdManager`/`IAPManager` riefen `NeonAnalytics` nirgends auf (kein Rewarded-Completion/Interstitial/Purchase-Tracking). Neue Events ergänzt, Commit `92759162`. Kompiliert fehlerfrei nach Fix eines falschen Firebase-Parameternamens, 20/20 Tests weiterhin grün. **Bitte bei Gelegenheit mit echtem Firebase-DebugView verifizieren** (Editor kann Firebase Analytics nicht wirklich senden).

## Aus Night Shift 2026-08-22 (Zyklus 20) hinzugekommen

- **[ERLEDIGT, Fallback-Blender-Design]** `GoldStar_Bonus` erstellt — farbneutrales Bonus-Collectible (nicht an Fee-Farbe gebunden), Commit `96a034bf`. In Unity importgeprüft (fehlerfrei). Noch nicht in `DemoDesignScene.unity` platziert (gleicher Grund wie LeafGem/StoneShard).

## Aus Night Shift 2026-08-22 (Zyklus 19) hinzugekommen

- **[ERLEDIGT, Cleanup]** Verwaistes Diagnose-Skript `DebugTimeScaleLogger.cs` entfernt — selbst als "nach Diagnose entfernen" markiert, zugehöriger Bug laut `FairyWingFlap.cs`-Kommentaren bereits behoben, nirgends mehr referenziert. Commit `dac2aec6`.

## Aus Night Shift 2026-08-21 (Zyklus 3) hinzugekommen

- **[ERLEDIGT, Fallback-Blender-Design]** `LeafGem_Boost` erstellt — Grün-Pendant zu `CrystalShard_Boost`, siehe Commit `2fcde0ea`. In Unity importgeprüft (fehlerfrei). **Offen:** Platzierung in `DemoDesignScene.unity` — Szene hat aktuell eine kleine uncommittete manuelle Änderung (Objekt-Rotation), daher nicht angefasst. Nachtrag sobald WIP geklärt ist.
- **[ERLEDIGT, Fallback-Blender-Design, Zyklus 9]** `StoneShard_Boost` erstellt — Pink/Gestein-Pendant, vervollständigt das Boost-Trio (Grün/Blau/Pink), Commit `9ef206bd`. In Unity importgeprüft (fehlerfrei). Gleiches Offen-Item wie LeafGem_Boost: Platzierung in `DemoDesignScene.unity` steht noch aus.
- **[Blocker, technisch]** `execute_blender_code_for_cli` (Headless-Blender-MCP-Tool) ist nicht nutzbar — `BLENDER_PATH`-Umgebungsvariable ist im MCP-Serverprozess nicht gesetzt, obwohl `Blender.app` unter `/Applications/Blender.app` existiert. Für zukünftige Blender-Fallback-Aufgaben ohne offene GUI-Session müsste das gesetzt werden (liegt außerhalb dessen, was ich als Night Shift selbst ändern kann/soll — Konfiguration des MCP-Servers, kein Projekt-Code).

## Aus Night Shift 2026-08-19 hinzugekommen

Siehe [reports/nightly/2026-08-19.md](../reports/nightly/2026-08-19.md) für Details zu Findings aus Code-QA-Scan und Recherche dieser Nacht.
