# NeonSmash — Nightly Backlog

Offene Aufgaben für Night Shifts, nach Priorität. Nicht jeder Eintrag ist eine Entscheidung — Status ist vermerkt.

## Hoch (kritisch/blockierend für Release oder bekannter Bug)

- **[BUG, bestätigt 2026-07-27]** `NEONSMASH_PROD` Define ist zwischen `Assets/Settings/Build Profiles/Android Release.asset` und `iOS.asset` vertauscht (hängt jeweils am falschen Platform-Eintrag). Vor jedem Store-Build in Unity prüfen — sonst geht Android/iOS Prod mit falscher Firebase-Umgebung raus. Nicht automatisch fixbar ohne Unity-Editor-Zugriff auf die Asset-Referenzen; für Night Shift geeignet, sobald Unity offen ist.
- **[OFFEN]** iOS AdMob Interstitial — letzter bekannter Stand: Propagation/Account-seitig, Code als korrekt verifiziert. Bei nächster Gelegenheit mit echtem Gerät erneut testen (siehe `project_admob_plan`).

## Mittel (hoher Nutzen, vertretbarer Aufwand)

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
