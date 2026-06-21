# ADV → Narrative Migration Inventory

Last scanned: 2026-06-21  
Last updated: 2026-06-21 (Shepel 66/66 8b53fcd4; full-body portrait integration 0369c196)  
Scope: `Content/ADV/Scenarios/` → `Content/Narrative/Scenarios/`

## Progress snapshot

| Line | Done | Partial | Pending | Notes |
|------|-----:|--------:|--------:|-------|
| Helen | 30 | 0 | 0 | Complete |
| Old Duke | 5 | 0 | 0 | Complete |
| SupCal | 14 | 0 | 0 | Complete |
| Draedon | 11 | 0 | 0 | Complete (`FirstMetTzeentch` bb3c0681) |
| Shepel | 66 | 0 | 0 | Complete — incl. 7 reactive-event dialogues |
| Acheron | 4 | 0 | 0 | Complete (`GalacticCrisis` + Rebuttal baec0f66) |
| **Total dialogue scenarios** | **~130** | **0** | **0** | All scenarios migrated; polish only (see Deferred) |

---

## Migration conventions (refined from Helen + Old Duke WIP)

1. **Class names match ADV** — no `Story` suffix (`FirstMet`, not `FirstMetHelenStory`; rename `FirstMetOldDukeStory` → wire `FirstMetOldDuke` only).
2. **`LocalizationCategory => "ADV"`** — reuse `Localization/en-US/Mods.CalamityOverhaul.ADV.hjson`.
3. **Localization in scenario class** — `ILocalizedModType` + `SetStaticDefaults()`; no separate Text classes.
4. **Short speaker ids** — `n.Say("Helen", "Solemn", text)`; framework prefixes mod name via `VaultType.Mod`.
5. **`DefaultStyle => "Sea"`** (short form OK). Old Duke uses `"Sulfsea"`, Shepel uses `"SHPC"`, Draedon uses `"Draedon"`, SupCal uses `"Brimstone"`.
6. **ADV after migration:**
   - Policy scenarios: `ConfigurePolicy() => null`, empty `Update`.
   - Gift scenarios: `StartScenarioInternal() => false`, empty `Update`.
   - Remove dead nested branch classes when Narrative owns full flow (Helen still has cleanup debt).
7. **Save sync:** per-line `*StorySync` (StoryPlayer module + legacy ADVSave dual-write). Pattern: `HalibutStorySync` / `OldDukeStorySync`. `LegacyStorySaveImporter` already imports all module types.
8. **Boss gifts:** abstract `*BossGiftNarrative` + `*GiftNarrativeTracker` + `*GiftBossKillNPC` (Helen reference).
9. **Special triggers** (zone/condition): standalone `NarrativeScenario` with `ConfigurePolicy()` checks (e.g. `HellGift`).
10. **Quest entries stay in ADV** `EntrustManager`; trigger Narrative via `NarrativeRouter.Begin<T>()`.
11. **Nested ADV branches** fold into one Narrative `Build()` via labels/choices — do not register separate Narrative classes for private nested ADV types unless complexity demands split files.
12. **Incoming calls** (`IncomingCallScenarioBase`, e.g. `DropPodCallScenario`) — evaluate separate Narrative incoming-call API; not plain `ADVScenarioBase`.

---

## Infrastructure checklist by line

| Line | StoryData module | Sync class | Ticker | Gift tracker | Notes |
|------|------------------|------------|--------|--------------|-------|
| Helen | `HalibutStoryData`, `BossGiftStoryData` | `HalibutStorySync` | `HalibutNarrativeTicker` | `HelenGiftNarrativeTracker` + `HelenGiftBossKillNPC` | **Complete** |
| Old Duke | `OldDukeStoryData` | `OldDukeStorySync` | `OldDukeNarrativeTicker` | — | **Complete** — all 5 dialogue scenarios migrated; ADV stubbed |
| SupCal | `SupCalStoryData` | `HalibutStorySync` (SupCal R/W) | `SupCalNarrativeTicker` | — | **Complete** — 14 scenarios; quest entries stay ADV |
| Shepel | `ShepelStoryData`, `ShepelGiftStoryData` | `ShepelStorySync` | `NarrativeScenarioTicker` | `ShepelGiftNarrativeTracker` | **Complete** — 66 scenarios; `SHPCNarrativeRouter` wired |
| Draedon | `DraedonStoryData` | `DraedonStorySync` | `NarrativeScenarioTicker` | — | **Complete** — 11 scenarios incl. `FirstMetTzeentch` |
| Acheron | `ApolliaStorySync` | `ApolliaStorySync` | — | — | **Complete** — 4 scenarios incl. `GalacticCrisis` + Rebuttal |

---

## Deferred / polish

Scenario migration is complete (~130/130). Remaining work is polish only:

- **Shepel full-body in narrative dialogues** — wired via `ShepelNarrativePortrait` + base `OnStarted`/`OnCompleted` (reactive/situational/gifts/idle/cyb courses); gift L3 face changes restored
- **CybCourse narrative routing** — tutorial leads use `NarrativeRunner.Begin` (not ADV `ScenarioManager`); gift spawn guards restored for ExoMechs/Twins
- **GalacticCrisis MachineWorld transit** — world cinematic + legacy import path
- **ADV dead-code cleanup** — strip stubbed `Build()`/branch code (Helen gifts, `HelensInterference`, `FishoilSubmitScenario`, migrated Shepel/Draedon/Acheron ADV)
- **Apollia full-body** — when Acheron scenarios adopt full-body portraits (no scenarios wired yet)

---

## Helen (30/30 done)

| Scenario | ADV path | Narrative path | Status | Notes |
|----------|----------|----------------|--------|-------|
| FirstMet | `Helen/FirstMet.cs` | `Helen/FirstMet.cs` | done | ADV stubbed; `OnScenarioComplete` dual-write remains |
| ResurrectionWarn | `Helen/ResurrectionWarn.cs` | `Helen/ResurrectionWarn.cs` | done | Policy on Narrative side |
| DyeProtest | `Helen/Everyday/DyeProtest.cs` | `Helen/Everyday/DyeProtest.cs` | done | |
| HelensInterference | `Helen/HelensInterference.cs` | `Helen/HelensInterference.cs` | done | ADV still has dead branch classes + `ScenarioManager` calls |
| FishoilQuestScenario | `Helen/Quest/FishoilQuest/FishoilQuestScenario.cs` | `Helen/Quest/FishoilQuest/FishoilQuestScenario.cs` | done | Ticker-driven |
| FishoilSubmitScenario | `Helen/Quest/FishoilQuest/FishoilSubmitScenario.cs` | `Helen/Quest/FishoilQuest/FishoilSubmitScenario.cs` | done | Entry triggers via `FishoilQuestEntry`; ADV sub-classes dead |
| HellGift | `Helen/Gifts/HellGift.cs` | `Helen/Gifts/HellGift.cs` | done | Standalone policy scenario |
| 22 boss gifts | `Helen/Gifts/*Gift.cs` | `Helen/Gifts/*Gift.cs` | done | All extend `HelenBossGiftNarrative`; ADV `StartScenarioInternal => false` |
| HalibutStorySync | — | `Helen/HalibutStorySync.cs` | done | Includes SupCal R/W helpers |
| HalibutNarrativeTicker | — | `Helen/HalibutNarrativeTicker.cs` | done | |
| HelenGiftNarrativeTracker | — | `Helen/Gifts/HelenGiftNarrativeTracker.cs` | done | Includes `HelenGiftBossKillNPC` |
| FishoilQuestEntry | `Helen/Quest/FishoilQuest/FishoilQuestEntry.cs` | — | N/A | Stays ADV; calls `NarrativeRouter.Begin<FishoilSubmitScenario>()` |

**Boss gifts (22):** AquaticScourge, BrimstoneElemental, CalamitasClone, Crabulon, Cryogen, DevourerOfGods, EyeOfCthulhu, Golem, HiveMind, KingSlime, Leviathan, MoonLord, Perforator, Plaguebringer, Plantera, Providence, QueenBee, Skeletron, SlimeGod, SupremeCalamitas, WallOfFlesh, Yharon.

**Optional cleanup:** strip dead ADV `Build()`/branch code from gifts, `HelensInterference`, `FishoilSubmitScenario`.

---

## Old Duke (5/5 done)

| Scenario | ADV path | Narrative path | Status | Notes |
|----------|----------|----------------|--------|-------|
| FirstMetOldDuke | `Abysses/OldDukes/FirstMetOldDuke.cs` | `OldDuke/FirstMetOldDuke.cs` | done | Cooperation choice tree; `FirstMetOldDukeStory.cs` removed |
| ComeCampsiteFindMe | `Abysses/OldDukes/ComeCampsiteFindMe.cs` | `OldDuke/ComeCampsiteFindMe.cs` | done | ADV stubbed |
| CampsiteInteractionDialogue | `Abysses/OldDukes/CampsiteInteractionDialogue.cs` | `OldDuke/CampsiteInteractionDialogue.cs` | done | Camp hub menu |
| CampsiteChatDialogue | `Abysses/OldDukes/CampsiteChatDialogue.cs` | `OldDuke/CampsiteChatDialogue.cs` | done | Full multi-branch chat tree |
| FirstCampsiteDialogue | `Abysses/OldDukes/Quest/Findfragments/FirstCampsiteDialogue.cs` | `OldDuke/Quest/FindFragments/FirstCampsiteDialogue.cs` | done | Quest entry stays ADV |
| OldDukeStorySync | — | `OldDuke/OldDukeStorySync.cs` | done | |
| OldDukeNarrativeTicker | — | `OldDuke/OldDukeNarrativeTicker.cs` | done | |

**Non-scenario ADV (stay):** campsite/shop/raider systems, `FindCampsiteQuestEntry`, `FindFragmentQuestEntry`, `ModifyOldDuke`, `OldDukeEffect`.

---

## SupCal (14/14 done)

All dialogue scenarios migrated under `Content/Narrative/Scenarios/SupCal/`. ADV stubbed. `WitchFarewell` full-body wired (0369c196); `SupCalDefeat` uses `HalibutPlayer` speaker id.

---

## Shepel (66/66 done)

All dialogue scenarios migrated (8b53fcd4). ADV stubbed.

### Core & courses (6) — done
| Scenario | ADV path | Narrative path | Status |
|----------|----------|----------------|--------|
| FirstMetShepel | `Shepel/FirstMetShepel.cs` | `Shepel/FirstMetShepel.cs` | done |
| CybCourseIntroDialogue | `Shepel/CybCourses/CybCourseIntroDialogue.cs` | `Shepel/CybCourses/CybCourseIntroDialogue.cs` | done |
| CybCourseHackIntroDialogue | `Shepel/CybCourses/CybCourseHackIntroDialogue.cs` | `Shepel/CybCourses/CybCourseHackIntroDialogue.cs` | done |
| CybCourseOutroDialogue | `Shepel/CybCourses/CybCourseOutroDialogue.cs` | `Shepel/CybCourses/CybCourseOutroDialogue.cs` | done |
| ShepelIdleDialogue | `Shepel/Dialogues/ShepelIdleDialogue.cs` | `Shepel/Dialogues/ShepelIdleDialogue.cs` | done |
| ShepelCyberActiveDialogue | `Shepel/Dialogues/ShepelCyberActiveDialogue.cs` | `Shepel/Dialogues/ShepelCyberActiveDialogue.cs` | done |

### Reactive — events (7) — done
ShepelBloodMoonDialogue, ShepelBossDefeatedDialogue, ShepelCyberLevelUpDialogue, ShepelPlayerRespawnDialogue, ShepelRainDialogue, ShepelRAMOverloadDialogue, ShepelSolarEclipseDialogue

### Reactive — bosses (24) — done
ShepelBrimstoneElementalDialogue, ShepelCalamitasCloneDialogue, ShepelCrabulonDialogue, ShepelCryogenDialogue, ShepelDesertScourgeDialogue, ShepelDestroyerDialogue, ShepelDevourerofGodsDialogue, ShepelEyeOfCthulhuDialogue, ShepelGolemDialogue, ShepelHiveMindDialogue, ShepelLeviathanDialogue, ShepelMoonLordDialogue, ShepelPerforatorDialogue, ShepelPlaguebringerDialogue, ShepelPlanteraDialogue, ShepelProvidenceDialogue, ShepelQueenBeeDialogue, ShepelSkeletronDialogue, ShepelSkeletronPrimeDialogue, ShepelSlimeGodDialogue, ShepelSupremeCalamitasDialogue, ShepelTwinsDialogue, ShepelWallOfFleshDialogue, ShepelYharonDialogue

### Situational (6) — done
ShepelDungeonDialogue, ShepelFirstNightDialogue, ShepelJungleDialogue, ShepelOceanDialogue, ShepelSnowBiomeDialogue, ShepelUnderworldDialogue

### Gifts (23) — done
ShepelAquaticScourgeGift, ShepelBoCGift, ShepelBrimstoneElementalGift, ShepelCalamitasCloneGift, ShepelCultistGift, ShepelDestroyerGift, ShepelDevourerofGodsGift, ShepelEoCGift, ShepelEoWGift, ShepelExoMechsGift, ShepelGolemGift, ShepelHiveMindGift, ShepelMoonLordGift, ShepelPerforatorGift, ShepelPlanteraGift, ShepelPolterghastGift, ShepelProvidenceGift, ShepelSkeletronPrimeGift, ShepelSlimeGodGift, ShepelSupremeCalamitasGift, ShepelTwinsGift, ShepelWoFGift, ShepelYharonGift

**Infra:** `ShepelStorySync`, `ShepelGiftNarrativeTracker`, `SHPCNarrativeRouter` — done.

---

## Draedon (11/11 done)

| Scenario | ADV path | Narrative path | Status |
|----------|----------|----------------|--------|
| FirstMetTzeentch | `Draedons/Tzeentch/FirstMetTzeentch.cs` | `Draedon/Tzeentch/FirstMetTzeentch.cs` | done |
| ExoMechQuickDefeat | `Draedons/Defeats/ExoMechQuickDefeat.cs` | `Draedon/Defeats/ExoMechQuickDefeat.cs` | done |
| ExoMechSecondDefeat | `Draedons/Defeats/ExoMechSecondDefeat.cs` | `Draedon/Defeats/ExoMechSecondDefeat.cs` | done |
| ExoMechThirdDefeat | `Draedons/Defeats/ExoMechThirdDefeat.cs` | `Draedon/Defeats/ExoMechThirdDefeat.cs` | done |
| ExoMechHardDefeat | `Draedons/Defeats/ExoMechHardDefeat.cs` | `Draedon/Defeats/ExoMechHardDefeat.cs` | done |
| ExoMechdusaSum | `Draedons/ExoMechdusaSums/ExoMechdusaSum.cs` | `Draedon/ExoMechdusaSums/ExoMechdusaSum.cs` | done |
| ExoMechEndingDialogue | `Draedons/ExoMechdusaSums/ExoMechEndingDialogue.cs` | `Draedon/ExoMechdusaSums/ExoMechEndingDialogue.cs` | done |
| DeploySignaltowerScenario | `Draedons/Quest/DeploySignaltowers/DeploySignaltowerScenario.cs` | `Draedon/Quest/DeploySignaltowers/DeploySignaltowerScenario.cs` | done |
| FirstTowerBuiltScenario | `Draedons/Quest/DeploySignaltowers/FirstTowerBuiltScenario.cs` | `Draedon/Quest/DeploySignaltowers/FirstTowerBuiltScenario.cs` | done |
| QuestCompleteScenario | `Draedons/Quest/DeploySignaltowers/QuestCompleteScenario.cs` | `Draedon/Quest/DeploySignaltowers/QuestCompleteScenario.cs` | done |
| DraedonStorySync | — | `Draedon/DraedonStorySync.cs` | done |

Quest/deploy UI stays ADV.

---

## Acheron Protocols (4/4 done)

| Scenario | ADV path | Narrative path | Status | Notes |
|----------|----------|----------------|--------|-------|
| FirstMetApollia | `AcheronProtocols/ApolliaActors/FirstMetApollia.cs` | `Acheron/Apollia/FirstMetApollia.cs` | done | Nested: ChipPath, SuspicionPath |
| GargoyleWarningScenario | `AcheronProtocols/ApolliaActors/GargoyleWarningScenario.cs` | `Acheron/Apollia/GargoyleWarningScenario.cs` | done | |
| GalacticCrisis | `AcheronProtocols/GalacticCrisises/GalacticCrisis.cs` | `Acheron/GalacticCrisis/GalacticCrisis.cs` | done | Nested: Rebuttal; MachineWorld transit polish deferred |
| DropPodCallScenario | `AcheronProtocols/Machines/DropPodScens/DropPodCallScenario.cs` | `Acheron/DropPod/DropPodCallScenario.cs` | done | `IncomingCallScenarioBase` migrated |

---

## Recommended migration order

1. ~~**Old Duke finish**~~ — **done**
2. ~~**SupCal**~~ — **done**
3. ~~**Draedon**~~ — **done** (incl. `FirstMetTzeentch`)
4. ~~**Shepel parallel batch**~~ — **done** (gifts 23 + cyb 3 + reactive bosses 24 + core/situational 9)
5. ~~**Acheron**~~ — **done** (incl. `GalacticCrisis` + Rebuttal)
6. ~~**Shepel reactive events**~~ — **done**
7. **Deferred / polish** — Shepel full-body rollout, SCal glitch VFX, GalacticCrisis transit, ADV cleanup, Apollia full-body (see section above)

---

## Parallel agent split (7 workers) — complete

| Worker | Scope | ~Count | Status |
|--------|-------|--------|--------|
| **A** | Old Duke: `CampsiteChatDialogue` tree + FirstMet cleanup + ADV stubs | 1 large + 4 stubs | **done** |
| **B** | SupCal: all 12 scenarios + `SupCalNarrativeTicker` | 12 | **done** |
| **C** | Draedon: 10 scenarios + sync/ticker | 10 | **done** (+ `FirstMetTzeentch`) |
| **D** | Shepel gifts: 23 + tracker infra | 23 | **done** |
| **E** | Shepel core: FirstMet, CybCourses×3, Idle, CyberActive + router | 6 | **done** |
| **F** | Shepel reactive bosses (24) + situational (6) | 30 | **done** |
| **G** | Acheron: 4 scenarios + Apollia story module | 4 | **done** |

**Optional Worker H:** Helen ADV dead-code cleanup (non-blocking) — still open.

---

## Portrait / full-body integration

**Status: infrastructure done (0369c196); per-scenario rollout partial.**

| Item | Status | Notes |
|------|--------|-------|
| `DialoguePanelView` lifecycle | **done** | Update/Draw hosts full-body VFX under dialogue panel |
| `INarrativePanelAnchor` | **done** | Panel anchor contract for portrait placement |
| `NarrativeSession.BlocksAdvance` / `BlocksCompletion` | **done** | Advance/completion gating during burn-out / exit |
| `WitchFarewell` full-body | **done** | First scenario wired via `ShowFullBodyPortrait` |
| `SupCalDefeat` multi-speaker | **done** | `HalibutPlayer` speaker id |
| Reward `AnchorYOffset` | **done** | Configurable y-offset for quest/reward popup placement |
| Shepel full-body in dialogues | **done** | `ShepelNarrativePortrait` + shared lifecycle on SHPC bases |
| Apollia full-body | **deferred** | When Acheron scenarios adopt portraits |

### Layer summary

| Layer | Capability |
|-------|------------|
| **InnoVault Narrative** | Bust portraits via `PortraitRegistry`; advance/completion blockers on `NarrativeSession` |
| **CWR Narrative** | `DialoguePanelView` + `INarrativePanelAnchor` host ADV `FullBodyPortraitBase` lifecycle (spawn, Update, Draw, burn-out, line-advance hooks) |
| **CWR ADV** | `FullBodyPortraitBase` + `ShowFullBodyPortrait` — reused by Narrative panel, not duplicated per scenario |
