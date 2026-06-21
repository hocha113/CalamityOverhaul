# ADV → Narrative Secondary Verification Audit

**Date:** 2026-06-21  
**Scope:** `Content/ADV/Scenarios/` vs `Content/Narrative/Scenarios/` (~130 dialogue scenarios)  
**Principle:** ADV is source of truth — especially portrait textures, line order, and hooks.

---

## Executive summary

| Line | Verdict | Notes |
|------|---------|-------|
| **Helen** | **PASS** (after fixes) | 22 boss gifts + core scenarios; portrait registry was missing 7 Helen expressions |
| **Old Duke** | **PASS** | 5/5 scenarios; OldDuke bust from campsite asset; chat tree spot-checked |
| **SupCal** | **PASS** (after fixes) | EternalBlazingNow `Serious2` fix; WitchFarewell full-body wired |
| **Draedon** | **PASS** (after fixes) | Deploy decline path missing `Alt` portrait — fixed |
| **Shepel** | **PARTIAL** | Dialogue/triggers OK; **full-body not ported** to Narrative (ADV still shows `ShepelFullBodyPortrait`) |
| **Acheron** | **PASS** | Apollia/Draedon/GalacticCrisis/DropPod; no bust portraits in ADV either |

**Issue counts (post-fix):**

| Severity | Count | Status |
|----------|------:|--------|
| **P0** | 32 line-level portrait bugs + 8 missing registry entries | **Fixed in this audit** |
| **P1** | 2 open | Shepel full-body rollout, GalacticCrisis transit |
| **P2** | 3 open | ADV dead-code cleanup, Apollia full-body (unused in ADV), duplicate `using` warning |

`dotnet build` — **0 errors** after P0 fixes.

---

## P0 — Wrong texture / missing registry (fixed)

### Root cause

`NarrativeHost.RegisterPortraits()` consolidated Helen expressions but **collapsed distinct ADV space-suffix keys** onto wrong assets:

| Narrative expression (before) | Mapped to | ADV asset (correct for many gifts) |
|------------------------------|-----------|-------------------------------------|
| `Serious` | `Helen2ADV` | `Helen_seriousADV` (gift L1 lines) |
| `Serious` | `Helen2ADV` | `Helen_serious2ADV` (EternalBlazingNow L4/L11) |
| `Enjoy` | `Helen_enjoyADV` | `Helen_enjoy3ADV`, `Helen_enjoy2ADV`, `Helen_naughty2ADV` |
| `Doubt` | `Helen_doubtADV` | `Helen_naughtyADV` (CalamitasClone L3) |
| *(missing)* | — | `Helen_slightAnnoyedADV`, `Helen_naughtyADV`, Draedon `Alt` |

### Registry additions (`NarrativeIds` + `NarrativeHost`)

| ExpressionId | ADVAsset |
|--------------|----------|
| `Naughty` | `Helen_naughtyADV` |
| `Naughty2` | `Helen_naughty2ADV` |
| `Enjoy2` | `Helen_enjoy2ADV` |
| `Enjoy3` | `Helen_enjoy3ADV` |
| `Stern` | `Helen_seriousADV` |
| `Serious2` | `Helen_serious2ADV` |
| `Alt` (Draedon) | `DraedonADV` |

`Serious` **retained** as `Helen2ADV` for story scenes (FirstMet, HelensInterference).

### Per-scenario fixes applied

| Scenario | ADV path | Narrative path | Lines fixed | Fix |
|----------|----------|----------------|-------------|-----|
| CalamitasCloneGift | `Helen/Gifts/CalamitasCloneGift.cs` | same | L1, L3 | `Stern`, `Naughty` |
| CryogenGift | `Helen/Gifts/CryogenGift.cs` | same | L1 | `Stern` |
| MoonLordGift | `Helen/Gifts/MoonLordGift.cs` | same | L0 | `Enjoy3` |
| PlanteraGift | `Helen/Gifts/PlanteraGift.cs` | same | L4 | `Naughty2` |
| PerforatorGift | `Helen/Gifts/PerforatorGift.cs` | same | L0, L1 | `Stern` |
| EyeOfCthulhuGift | `Helen/Gifts/EyeOfCthulhuGift.cs` | same | L4 | `Naughty` |
| HellGift | `Helen/Gifts/HellGift.cs` | same | L0–L3 | `SlightAnnoyed`, `Enjoy`, `Enjoy2` |
| WallOfFleshGift | `Helen/Gifts/WallOfFleshGift.cs` | same | L0 | `SlightAnnoyed` |
| HiveMindGift | `Helen/Gifts/HiveMindGift.cs` | same | L0, L1 | `SlightAnnoyed` |
| CrabulonGift | `Helen/Gifts/CrabulonGift.cs` | same | L0–L4 | `Solemn`/`Naughty` |
| GolemGift | `Helen/Gifts/GolemGift.cs` | same | L0–L4 | `Solemn`/`Stern` |
| SupremeCalamitasGift | `Helen/Gifts/SupremeCalamitasGift.cs` | same | L4 | `Stern` |
| EternalBlazingNow | `SupCal/End/EternalBlazingNows/EternalBlazingNow.cs` | `SupCal/End/EternalBlazingNow/EternalBlazingNow.cs` | L4, L11 | `Serious2` |
| DeploySignaltowerScenario | `Draedons/Quest/DeploySignaltowers/DeploySignaltowerScenario.cs` | `Draedon/Quest/DeploySignaltowers/DeploySignaltowerScenario.cs` | decline | `Alt` on decline line |

**Files touched:** `NarrativeIds.cs`, `NarrativeHost.cs`, 14 scenario files (listed above).

---

## P1 — Visual / behavioral regressions (open)

### Shepel full-body missing (all SHPC dialogue scenarios)

ADV bases call `ShowFullBodyPortrait<ShepelFullBodyPortrait>()` on scenario start:

- `ShepelGiftScenarioBase`, `ShepelReactiveDialogueBase`, `ShepelSituationalDialogueBase`
- CybCourse intro/hack/outro

**Narrative:** zero `ShowFullBodyPortrait` usages under `Scenarios/Shepel/`. Infrastructure exists (`DialoguePanelView` + `ShepelFullBodyPortrait` dual-register in `FullBodyPortraitBase.VaultRegister`).

**Fix:** Add `DialoguePanelView.Instance?.ShowFullBodyPortrait<ShepelFullBodyPortrait>()` in shared Shepel narrative base / `OnStarted` per scenario group (mirror ADV bases).

### HellGift — reward anchor line ✓ fixed

| | ADV | Narrative |
|---|-----|-----------|
| Reward timing | `Add(L4, onComplete: Give)` — popup after L4 spoken | `.Reward` after L4 `.Say`, before L5 (matches ADV onComplete) |
| Portrait L4/L5 | default bust | default bust ✓ |

**Fix applied:** Moved `.Reward` from before L4 to after L4 `.Say` (before L5 anchor line).

### Full-body lifecycle — `DialoguePanelView` vs `DialogueBoxBase` ✓ fixed (OnDialogueComplete)

| Behavior | ADV (`DialogueBoxBase`) | Narrative (`DialoguePanelView`) |
|----------|---------------------------|----------------------------------|
| Draw z-order | Full-body **under** panel (`Draw` before `DrawStyle`) | Same ✓ |
| Block advance/close | Checks `BlockDialogueAdvance/Close` | Binds `session.BlocksAdvance/BlocksCompletion` ✓ |
| Line advance hook | `OnDialogueAdvance()` in `StartNext` when `playedCount > 0` | `NotifyPortraitLineAdvance` on speaker:text change; skips first line ✓ |
| Queue complete | `OnDialogueComplete()` before close | `OnDialogueComplete()` on `Completed` phase before close ✓ |
| Advance index | `FullBodyPortraitBase.dialogueIndex` incremented in base | Same base class ✓ |

**Impact:** `SupCalFullBodyPortrait` smile/burn triggers via `dialogueIndex` on `OnDialogueAdvance` — unchanged; WitchFarewell index 10 smile still correct. `OnDialogueComplete` now fires on natural session completion (including when `BlockDialogueClose` defers `HideFullBodyPortrait`).

### GalacticCrisis MachineWorld transit

ADV nested cinematic + legacy import partially deferred (see `MIGRATION.md`). Narrative has render phases + Rebuttal branch; world transit polish still open.

---

## P2 — Polish (open)

- **ADV dead code:** stubbed `Build()` / nested branch classes in migrated ADV files (Helen gifts, Shepel, Draedon, Acheron).
- **Apollia full-body:** `ApolliaFullBodyPortrait` exists; no ADV scenario wires it yet — no Narrative action needed until content does.
- **Duplicate `using InnoVault.Narrative.Core`** in `DeploySignaltowerScenario.cs` (CS0105 warning).

---

## Portrait registry audit

### Central registry (`NarrativeHost.RegisterPortraits`)

| CharacterId | ADV equivalent | Bust texture | Expressions | Match |
|-------------|----------------|--------------|-------------|-------|
| `OldDuke` | campsite NPC | `OldDukeCampsite` | silhouette | ✓ |
| `HelenUnknown` | FirstMet R1 | `HelenADV` | silhouette | ✓ |
| `Helen` | R1/R2 gifts + story | `HelenADV` | Doubt, Enjoy, **Serious→Helen2ADV**, Solemn, Amazed, Wrath, Silence, SlightAnnoyed, **+Naughty, Naughty2, Enjoy2/3, Stern, Serious2** | ✓ after fix |
| `SupCalUnknown` | shadow intro | `SupCalsADV[4]` | silhouette | ✓ |
| `SupCalShadow` | EBN Rolename2 | `SupCalsADV[4]` | BeTo | ✓ |
| `SupCal` | witch | `SupCalsADV[0]` | CloseEye, BeTo, Despise, Shock, Smile, Sigh | ✓ (Sigh=Despise asset matches ADV) |
| `Draedon` | 嘉登 | `Draedon2ADV` | Red, **Alt** | ✓ after fix |
| `Shepel` / `SHPC` | name only | *(none — ADV null)* | — | ✓ intentional |
| `HalibutPlayer` | SupCalDefeat | `HelenADV` | — | ✓ |
| `Apollia` | name only | *(none — ADV null)* | — | ✓ |
| `Tzeentch` | silhouette | `Tzeentch` | silhouette | ✓ |

Per-scenario ADV `RegisterPortrait` calls for migrated lines are **obsolete** — central registry + `ExpressionId` replaces space-suffix keys.

---

## High-risk scenario spot checks

| Scenario | Lines / choices | Portraits | Order / hooks | Result |
|----------|-----------------|-----------|---------------|--------|
| FirstMet (Helen) | 15 lines + choices | Serious→Helen2ADV | policy + sync | ✓ |
| HelensInterference | 5 branches | Serious→Helen2ADV | choices + heartcarver hooks | ✓ |
| CampsiteChatDialogue | multi-branch hub | OldDuke silhouette | labels match ADV tree | ✓ |
| EternalBlazingNow | 15 + choice | Amazed, Serious2, SupCalShadow | screen jitter, choice routing | ✓ after fix |
| WitchFarewell | 11 lines | null bust + full-body | OnStarted/OnCompleted hooks | ✓ |
| FirstMetSupCal | fish branch | SupCalUnknown, Helen, SupCal expr | choice tree | ✓ |
| DeploySignaltower | intro + accept/decline | Red, Alt on decline | render on L8, quest sync | ✓ after fix |
| GalacticCrisis | intro + rebuttal + brief | Draedon Red | render phases | ✓ (transit P1) |
| FirstMetShepel | core | null bust (full-body ADV only) | — | P1 full-body |
| Shepel gift ×23 | 4–5 lines each | null bust | reward anchor | ✓ text; P1 full-body |

---

## Full-body integration summary

| Scenario | ADV hook | Narrative hook | Status |
|----------|----------|----------------|--------|
| WitchFarewell | `OnScenarioStart` → `ShowFullBodyPortrait<SupCalFullBodyPortrait>` | `OnStarted` → same via `DialoguePanelView` | **Done** |
| Shepel gifts/reactive/situational/cyb | ADV base `OnScenarioStart` | *(none)* | **Missing** |
| Apollia scenarios | *(none in ADV)* | *(none)* | N/A |

`SupCalFullBodyPortrait.SmilePortraitDialogueIndex = 10` — aligned with 11-line WitchFarewell (advance count matches ADV `playedCount` semantics).

---

## Recommended follow-up (not done in this audit)

1. Wire Shepel `ShowFullBodyPortrait` in Narrative shared bases (largest visual gap).
2. ~~HellGift — move reward to L4 `onExit` to match ADV.~~ **Done**
3. ~~Optional: invoke `OnDialogueComplete()` in `DialoguePanelView.Close()` before `HideFullBodyPortrait()` for parity.~~ **Done**
4. Strip ADV dead `Build()` bodies per `MIGRATION.md` deferred cleanup.

---

## Audit methodology reference

See `Content/Narrative/MIGRATION.md` for migration inventory and conventions. This audit added automated Helen gift portrait line comparison (ADV `Add` suffix → `ADVAsset` vs Narrative `.Say` expression → registry).
