# NeonSmash — Research

Validierte Markt-/Zielgruppen-/Monetarisierungs-/Konkurrenzinformationen. Quelle + Datum bei jedem Punkt, Fakten von Schlussfolgerungen getrennt.

## Retention-Benchmarks (Recherche 2026-08-19)

- **D1-Retention:** gesund 30–40%, Top-Titel 40–50%. Marktmedian ist 2026 auf ~22% gesunken; Hypercasual ist bei D1 besonders kompetitiv. Quellen: [Segwise](https://segwise.ai/blog/mobile-gaming-app-user-retention-strategies), [InvestGame 2026 Report](https://investgame.net/wp-content/uploads/2026/01/2026-01-27-2026-mobile-pc-benchmarks_compressed.pdf).
- **D7-Retention:** Hypercasual ~8–12%, Marktmedian breiter gefasst ~4%. Hypercasual verliert bis D30 fast alle Spieler — für kurze Zyklen gebaut (5–10 Min Sessions, ~4 Sessions/Tag), nicht für Langzeit-Retention. Quellen: [Business of Apps](https://www.businessofapps.com/data/mobile-game-retention-rates/), [Digital Minds BPO](https://digitalmindsbpo.com/blog/mobile-game-retention-benchmarks/).
- **Eigene Schlussfolgerung:** NeonSmash liegt zwischen Hypercasual und Casual (hat Meta-Systeme: Dream Energy, Boosts, Daily Rewards, Leaderboards) — das sind laut Quellen genau die Retention-Hebel, die "Casual" von "Hypercasual" unterscheiden. Zielwert sollte über reinem Hypercasual-D7 liegen (>12%), nicht am Hypercasual-Median gemessen werden.
- Genannte Treiber: kurzer, knackiger erster Session mit sofortigem Skill-/Combo-Hook; einfache FTUE ohne lange Tutorials; Daily-Reward-Loop als primärer D2–D7-Zug.

## Vergleichbare Spiele (Recherche 2026-08-19)

Direkte Genre-Nachbarn: **Tiles Hop / Hop (Ketchapp)**, **Colorwood Sort**, **Piano Tiles**. Quellen: [App Store – Tiles Hop](https://apps.apple.com/us/app/tiles-hop-music-ball-smash/id1344702806), [Google Play – Hop](https://play.google.com/store/apps/details?id=com.ketchapp.hop).

- **Gelobt:** befriedigendes taktiles/Combo-Feedback, "noch ein Run"-Loop, einfache Steuerung, visuelle Politur/Farbfeedback.
- **Kritisiert:** Ads, die mitten im Run statt an natürlichen Pausen unterbrechen (wiederkehrende Beschwerde bei Piano Tiles, Hop, Tiles-Hop-Klonen); erzwungene Interstitials fühlen sich schlechter an als Opt-in-Rewarded; Low-Effort-Klone mit inkonsistentem Feedback untergraben Vertrauen.
- **Eigene Schlussfolgerung:** Der Farb-Phasen/Combo-Mechanismus von NeonSmash passt gut zum Kern-Appeal dieses Genres (rhythmisches Treffer-Feedback). Größtes reputatives Risiko laut diesen Mustern ist Interstitial-Timing, nicht Rewarded-Ads.

## Rewarded-Ad-Placement Best Practices (Recherche 2026-08-19)

Best-konvertierende Platzierungen hängen an Fail-/Übergangs-Momenten, nicht mitten im Run. Quellen: [AppSamurai](https://appsamurai.com/blog/rewarded-ads-in-mobile-games-strategy-data-and-best-practices/), [Chartboost](https://www.chartboost.com/resources/blog/exploring-rewarded-videos-a-comparative-analysis-of-hypercasual-games-and-other-genres/).

- **Continue-after-death:** höchste Konversion (Moment höchster Motivationsverlust) — passt zu NeonSmashs offener Revive/Continue-Frage aus dem AdMob-Plan (aktuell KEIN Revive-Mechanismus vorhanden).
- **Score-/Reward-Multiplikator am Run-Ende:** Opt-in, verdoppelt Score/Dream-Energy für ein Video.
- **"Try before you buy":** Skin/Boost für einen Run per Rewarded-Ad freischalten — passt zum geplanten Skin-Monetarisierungsmodell.
- Rewarded-Completion-Rates liegen regelmäßig über 90% (Opt-in). Empfehlung: Rewarded-first, Interstitials selten und nur an natürlichen Loop-Grenzen (nach dem Run, nicht mitten in einer Combo) — deckt sich mit NeonSmashs aktueller Implementierung (Interstitial nur nach Game Over, jedes 3. Mal + 60s Cooldown, siehe Memory `project_admob_plan`).

## Markttrends 2025–2026 (Recherche 2026-08-19)

- **Hybrid-Casual ist das Wachstumssegment 2025:** Top-10-Hybrid-Casual-Titel mit ~100% YoY-IAP-Wachstum in Q2 2025 (Ad-Monetarisierung + leichte IAP/Meta-Progression). Quellen: [Udonis](https://www.blog.udonis.co/mobile-marketing/mobile-games/mobile-game-market-trends), [GameGrowthAdvisor](https://gamegrowthadvisor.com/blog/2026-04-16-hybrid-casual-game-design-strategy-2026/).
- **Monetarisierungs-Split nach Genre:** Casual-Spiele tendieren zu 40–60% IAP / 40–60% Ads (vs. Hypercasual 85–95% reine Ads) — stützt NeonSmashs Plan, AdMob mit Soft-Currency/IAP zu kombinieren statt reinem Ad-only.
- **CPI stieg ~30% YoY auf $0,56 in 2025** — für ein kleines Indie-Budget zählt organischer Hook/Viralität (teilbare Combo-Momente, Leaderboard-Flexing) mehr als bezahlte User Acquisition.
- **Referenzpunkt:** Block Blast! erreichte 368M Downloads ohne IAP, rein Ad-Monetarisierung — belegt, dass ein gut getunter Ad-only-Casual-Loop skalieren kann, als Fallback falls der IAP-Ausbau sich verzögert.

## Daily-Reward/Streak-Loop Best Practices (Recherche 2026-08-20)

Anlass: `DailyRewardManager`-Bugfix in dieser Night Shift (Reward-Zyklus wrappte ab Tag 7 nicht mehr, siehe Commit `2c169a7a`). Quellen: [StriveCloud – Hook Model 2026](https://www.strivecloud.io/blog/hook-model-user-retention), [Plotline – Streaks for Gamification](https://www.plotline.so/blog/streaks-for-gamification-in-mobile-apps), [Digia – Gamification in Mobile Apps](https://www.digia.tech/post/gamification-mobile-apps-streaks-rewards-retention/).

- **7-Tage-Streak ist der stärkste Frühindikator für Langzeit-Retention** (stärker als klassische D30-Werte); Nutzer mit 7+ Tagen Streak sind laut zitierten Duolingo-Zahlen ~2,3x wahrscheinlicher täglich aktiv. Bestätigt, dass NeonSmashs 7-Tage-Zyklus (jetzt korrekt wrapend) die richtige Zykluslänge ist.
- **Streak-Aktion muss an einem schlechten Tag noch machbar sein** — ein 10-Sekunden-Claim ist verteidigbar, eine 20-Minuten-Aktion nicht. NeonSmashs Daily-Claim (ein Tap) erfüllt das bereits.
- **Streak-Protection/Freeze fehlt bei NeonSmash:** Quellen empfehlen explizit einen "Freeze", der einen verpassten Tag abfedert — ohne ihn zerstört ein einziger verpasster Tag wochenlange Investition und erzeugt eher Frust als Motivation zum Neustart. `DailyRewardManager.ClaimTodayReward()` setzt den Streak aktuell hart auf 1 zurück, sobald `LastClaimedDate != gestern`, kein Freeze-Mechanismus vorhanden.
- **Alternative: rollierendes Kumulativ-Login** ("3 von 7 Tagen diese Woche") gilt als nachsichtiger/nachhaltiger als ein hart resettender Streak — als Design-Alternative im Hinterkopf behalten, falls Streak-Reset-Frust sich in Retention-Daten zeigt.
- **Eigene Schlussfolgerung/Backlog-Vorschlag:** Kein Blocker, aber ein möglicher nächster Schritt für den Daily-Reward-Loop wäre ein einfacher Streak-Freeze (z. B. 1 Freeze-Charge pro Woche, verdient oder per Rewarded-Ad) — passt auch zum bestehenden Rewarded-Ad-Placement-Research (Opt-in-Momente an Verlust-Punkten konvertieren am besten). Noch nicht umgesetzt, da neue Spielmechanik + UI nötig wäre.

## UI-Pattern für Progress-/Meta-Progression-Bars (Recherche 2026-08-21)

Anlass: In dieser Night Shift (Zyklus 1) wurden die 3 Feen-Energie-Füllbalken via `ColorProgressUI.cs` umgesetzt (siehe `project_multiplayer_status`/Memory) — Recherche prüft, ob das Design gängigen Patterns entspricht. Quellen: [UXPin – Progress Tracker Design 2026](https://www.uxpin.com/studio/blog/design-progress-trackers/), [Pixune – Game UI Design Guide](https://pixune.com/blog/game-ui-design/), [Eduardo Zmievski – UX Mobile Casual Game Study](https://medium.com/@eduardozmievski/ux-mobile-casual-game-study-improving-feature-engagement-b3ff86b0d39e), [Medium – Fill the Progress (Maxim Kosyakoff)](https://medium.com/@MaxKosyakoff/fill-the-progress-fc0fa99cabac).

- **Animation statt Sprung:** Füllbalken sollten zwischen Werten animiert füllen (nicht springen) plus dezente Farbverschiebung als Bewegungssignal — bestätigt den bereits in `ColorProgressUI.cs` genutzten Lerp-Ansatz (nicht instant `fillAmount`-Set); keine Codeänderung nötig, nur Validierung.
- **"Contextual Minimalism" fürs HUD:** mehrere gleichzeitige Ressourcen-/Fortschrittsanzeigen (hier: 3 Feen-Balken) sollen nur so viel Information zeigen wie nötig — kein Dauer-Numerikwert, sondern primär visuelle Füllung; passt zu NeonSmashs aktuellem Balken-Design ohne Prozentzahl-Overlay. Kein Handlungsbedarf, nur Bestätigung des gewählten Ansatzes.
- **Fortschritt muss auf Zukunft zeigen:** Best Practice ist, Spielern vorab zu signalisieren, was bei Balkenfüllung wartet (Reward-Preview) statt nur den reinen Füllstand zu zeigen — bei NeonSmash aktuell nicht vorhanden (die 3 Feen-Balken zeigen nur Fortschritt, kein Preview auf die Special-Mode-Belohnung beim Erreichen des 20er-Triggers). **Backlog-Vorschlag:** kleines Icon/Glow am Balkenende, das andeutet was bei Trigger passiert — kein Quick-Fix (UI-Arbeit + Szene), daher nur als Idee vermerkt, nicht umgesetzt.
- **Kern-Loop muss Meta-Layer füttern und umgekehrt:** bestätigt bestehendes NeonSmash-Design (Farb-Treffer im Run → Feen-Energie → Special-Mode-Trigger → zurück in den Run), keine Abweichung gefunden.
- **Eigene Schlussfolgerung:** Der in Zyklus 1 gebaute Feen-Füllbalken entspricht den recherchierten 2026-Patterns (Animation, Minimalismus, Kern/Meta-Rückkopplung). Einzige Lücke ist fehlendes Reward-Preview am Balkenende — als Low-Priority-Backlog-Idee vermerkt, kein Bug.

## FTUE/Tutorial-Onboarding Best Practices (Recherche 2026-08-21, Zyklus 5)

Anlass: Bestehende Research-Themen (Retention, Konkurrenz, Ad-Placement, Markttrends, Daily-Reward, Progress-Bars) sind bereits abgedeckt — neue Recherche zu einem bisher nicht untersuchten Bereich, gegen NeonSmashs tatsächlichen `TutorialManager` geprüft statt nur abstrakt notiert. Quellen: [Playio – Onboarding Decides Your D1](https://blog.playio.co/mobile-game-onboarding-retention), [Gamedeveloper.com – Best Practices for FTUE](https://www.gamedeveloper.com/design/best-practices-for-a-successful-ftue-first-time-user-experience-), [Udonis – FTUE in Mobile Games](https://www.blog.udonis.co/mobile-marketing/mobile-games/first-time-user-experience), [Adrian Crook – Mobile Game Onboarding](https://adriancrook.com/best-practices-for-mobile-game-onboarding/).

- **Erste 60 Sekunden entscheiden D1:** Spieler sollten binnen einer Minute im Kerngameplay sein, nicht in Tutorials/Settings/Account-Erstellung hängen. **Gegen NeonSmash geprüft** (`Assets/Scripts/Tutorial/TutorialManager.cs`, `RunTutorialSequence()`): Der allererste Tutorial-Schritt ist bereits ein echter Tap-Punkt (`textNormalPoint = "Tap to destroy!"`) — der Spieler interagiert sofort mit dem Kernmechanismus, keine vorgeschalteten Screens. Entspricht der Empfehlung, kein Handlungsbedarf.
- **Ein Mechanismus nach dem anderen, schrittweise freigeschaltet:** NeonSmashs Sequenz (Tap → Score-Hint → Swipe → Spawn-Hint → Lives-Hint → Special Orb) führt jede neue Mechanik einzeln ein, mit Hint-Text statt Sperr-Dialogen. Entspricht der Empfehlung.
- **Personalisierung des ersten Sessions ist der 2026-Trend** (unterschiedliche Onboarding-Pfade je nach Akquisitionsquelle/Verhalten, laut zitierter Studie bis zu 52% D30-Lift): NeonSmash hat aktuell EINEN festen Tutorial-Pfad für alle Spieler (kein Branching). **Eigene Schlussfolgerung:** Für ein Indie-Projekt dieser Größe ist volle Personalisierung vermutlich überdimensioniert (braucht Segmentierung + Tracking-Infrastruktur) — als Low-Priority-Idee vermerkt, kein Quick-Fix, kein aktueller Handlungsbedarf.
- **Fortschritts-Meilensteine/Checklisten als Motivationsanker:** NeonSmashs Tutorial hat keine sichtbare "Schritt 2 von 6"-Anzeige während der Sequenz. **Eigene Schlussfolgerung:** Bei einer so kurzen Sequenz (6 Schritte, vermutlich <60s Gesamtdauer) ist der Nutzen eines Fortschrittsindikators fraglich — Recherche bezieht sich eher auf längere Onboarding-Flows mit mehreren Sessions/Screens. Kein Backlog-Eintrag, da unklarer Mehrwert für die aktuelle Kürze.
- **Gesamtfazit:** NeonSmashs bestehendes Tutorial entspricht in den zentralen, hochwirksamen Punkten (Zeit-bis-Kernspaß, Ein-Mechanismus-nach-dem-anderen) bereits 2026-Best-Practices. Kein Bug, kein dringender Änderungsbedarf gefunden — Recherche bestätigt den bestehenden Ansatz, statt eine neue Aufgabe zu erzeugen.

---
Recherche durchgeführt von einem Subagenten via Websuche am 2026-08-19 sowie direkt am 2026-08-20 (Daily-Reward-Abschnitt), 2026-08-21 (Progress-Bar-UI-Abschnitt, Zyklus 7) und 2026-08-21 manueller Lauf (FTUE-Abschnitt, Zyklus 5). Alle Quellen/Links wie zurückgegeben, nicht einzeln nachverifiziert — bei wichtigen Entscheidungen (z. B. Ad-Placement-Änderungen) Originalquelle vor Umsetzung selbst gegenchecken.
