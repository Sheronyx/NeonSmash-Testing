# NeonSmash — Game Direction

Bestätigte, langfristige Design-/Produktentscheidungen. Nur Dinge, die tatsächlich beschlossen (und wo vermerkt: implementiert) sind — keine offenen Ideen. Details/Historie stehen im Auto-Memory-System, hier nur der aktuelle Stand als Referenz für Night Shifts.

## Core Loop (aktuell, Stand 2026-08-19)

Infinity Mode: 12-Phasen-System (Farb/Phasen-Redesign, bestätigt 2026-07-09, Code fertig 2026-07-09).

- 3 Farben: **Pink / Grün / Blau** (`PointColor` Enum, ECHT umbenannt von Red/Green/Purple).
- Normal Mode: alle 3 Farben spawnen parallel (3-Slot-System, `MixedPointSpawner`).
- Pro Farbe läuft ein "zerstörte Elemente"-Zähler, persistiert über Normal-Phasen. Bei 20 → Special Mode der Farbe startet sofort (Pink→Gravity, Grün→Magnet-Platzhalter/Gravity, Blau→Fountain), Punkte 3x.
- Special Mode ist anzahl-basiert (nicht zeit-basiert), endet nach fester Elementzahl.
- Game Over bei Miss in JEDEM Mode + bei Shocker-Treffer überall. Diamanten sind ungefährlich (Miss = folgenlos).
- 12 Phasen mit steigender Intensität: Shocker ab Phase 5, Diamanten ab Phase 9/11, Diamant-Bonus-Multiplikator (15x gestapelt) in Phase 10/12. Phase 13 = Endless-Platzhalter (grob, TBD).

**Offene technische Restarbeit (User in Unity, nicht blockierend):**
1. ~~`PhaseManager`-GameObject in `GameScene_InfinityMode` anlegen + spawner-Referenz.~~ **ERLEDIGT** — per Night-Shift-Check 2026-08-20 bestätigt: GameObject existiert, ist aktiv, `PhaseManager.Instance` zur Laufzeit nicht null (siehe `docs/NIGHTLY_BACKLOG.md`).
2. ~~`DiamondPoint`-Prefab bauen → `MixedPointSpawner.diamondPrefab` zuweisen.~~ **ERLEDIGT** — `Assets/Prefabs/Diamond Point Collectable.prefab` existiert vollständig und ist in der Szene verdrahtet (per Night-Shift-Check 2026-08-20 bestätigt, `diamondPrefab`-Feld in `MixedPointSpawner.cs:146` referenziert es aktiv).
3. Optional `gravityDiamondPrefab`/`fountainDiamondPrefab` für Bonus-Elemente Phase 10/12 — **weiterhin offen**, diese Felder existieren nicht im aktuellen `MixedPointSpawner.cs` (Stand 2026-08-22 geprüft).
4. Bestehende Farb-Prefab-Zuweisungen in der Szene kurz visuell verifizieren.
5. Echter Magnet Mode (aktuell Platzhalter=Gravity) und Phase-13-Feindesign: spätere Aufgaben.

## Art Direction

- **Verbindlich:** Low-Poly-Stil, Mobile-Fokus. Alles andere visuell noch nicht final festgelegt.
- **3D-Pipeline-Wechsel (entschieden 2026-08-08/09):** Fairy-Charaktere wechseln von 2D Sprite-Skin-Rigs auf 3D Low-Poly-Modelle (Monetarisierung: viele kaufbare Skins pro Fairy mit eigenen Animationen, Brawl-Stars-artig). 2D bleibt bewusst für UI/HUD, Parallax-Hintergründe, vermutlich Partikel/VFX-Sprites.
  - Tool-Chain: **Tripo AI** (nicht mehr Meshy) → Blender (Decimate auf ~2.500–4.000 Tris) → manuelles Rigging in Blender (Mixamo Auto-Rig scheitert strukturell an Chibi-Proportionen).
  - Tap/Swipe/Special-Mode-Elemente auf 3D umzustellen ist explizit **offene, riskante Frage** (Lesbarkeit bei hoher Instanzzahl + reaktionszeit-basiertem Gameplay) — nicht wie bei den Fairies als Gewinn angenommen, braucht Prototyping vor Commitment.

## Monetarisierung (Stand 2026-08-19)

- **AdMob** (Rewarded + Interstitial, kein Banner, AdMob pur ohne Mediation zum Start). Android vollständig getestet und produktionsreif (2026-06-22). iOS: Rewarded + Consent laufen, Interstitial-Propagation war das offene Risiko, siehe Release-Checkliste.
- **IAP:** aktuell `diamonds_5`, `diamonds_50` (Currency-Consumables, siehe `Assets/Currency_5Diamonds.asset`/`Assets/Currency_50Diamonds.asset`) — Korrektur 2026-08-22, alte Bezeichnung "coins_1000/coins_200" war veraltet/falsch. Nur 2 Preisstufen vorhanden; Recherche empfiehlt 3-5 Stufen inkl. Decoy-Option (siehe `docs/RESEARCH.md`). Diamond Splinters (4 Stufen: 5/50/200/1000) sind reine Soft-to-Soft-Konvertierung gegen Dream Energy, kein IAP. Siehe Release-Checkliste für Store-Submit-Pflichten.
- Server-seitige Score-Validierung (Anti-Cheat) für Leaderboards ist gewünscht, aber bewusst auf später verschoben.

## Bewusst verworfen

- **Basket/Wave-Redesign** (Fruit-Ninja-artige Wellen/Korb-Mechanik, 2026-07-07 kurz bestätigt) — abgelöst durch das Farb/Phasen-Redesign, bevor technische Umsetzung begann. Nicht wieder aufgreifen ohne neuen, konkreten Grund.
- **Klon-Ansatz für FakePoints** — separate Fake-Prefabs pro Skin bleibt der Weg (optische Kontrolle gewollt).

## In Diskussion, NICHT umgesetzt

- **Flow/Pacing-Redesign** (2026-07-29 diskutiert): Special Modes als sichere Verschnaufpause (kein Lebensverlust), Shocker im Special Mode bricht nur den Modus ab statt Game Over, Ease-Ramp bei jedem Phasenwechsel proportional zur Reaktionszeit-Differenz. Offene Frage: gibt es echtes Playtester-Signal, dass das Spiel zu stressig ist, oder ist das eine Vermutung? Die Ramp-Idee (Punkt 3) ist unabhängig davon sinnvoll (Game-Feel/Fairness, kein Schwierigkeits-Statement).

---
Zuletzt aktualisiert: 2026-08-22 (Night Shift) — veraltete IAP-Produkt-IDs korrigiert und zwei bereits in früheren Night Shifts erledigte "offene Restarbeit"-Punkte (PhaseManager-GameObject, DiamondPoint-Prefab) als erledigt markiert; keine neuen Design-Entscheidungen getroffen. Vorherige Aktualisierung: 2026-08-19 (aus bestehendem Auto-Memory synthetisiert).
