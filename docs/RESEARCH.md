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

---
Recherche durchgeführt von einem Subagenten via Websuche am 2026-08-19. Alle Quellen/Links wie vom Agenten zurückgegeben, nicht einzeln nachverifiziert — bei wichtigen Entscheidungen (z. B. Ad-Placement-Änderungen) Originalquelle vor Umsetzung selbst gegenchecken.
