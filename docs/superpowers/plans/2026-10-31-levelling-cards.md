# Levelling Cards Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A levelling guide window and a pinnable reference card that show short, level-appropriate rotations for every combat job.

**Architecture:** Cards are hand-written JSON (`presets/levelling-cards.json`), embedded in the plugin and parsed in Core into `LevellingCard` records. Core validates each card against `IAbilityCatalog` (names resolve, unlock levels fit the band, bands tile the job's range). GameData adds unlock levels to `Ability` and exposes job start levels, class-to-job mapping and per-level unlocks. Two ImGui windows in `Ui/Levelling/` draw the cards with game icons and hotbar keybinds.

**Tech Stack:** C# / .NET 10, Dalamud.NET.Sdk 15, ImGui (Dalamud.Bindings.ImGui), Lumina, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-31-levelling-cards-design.md`

## Global Constraints

- Card copy: brief. No em dashes, no hyperbole. Notes are one short sentence.
- British spellings in all user-facing text and comments.
- The existing trainer (palette, preset import, practice engine, saved config) must behave exactly as before.
- No Claude attribution in commits or PRs.
- Build: `./build.sh --test`. Game-data tests need `FFXIV_SQPACK="/mnt/f/Program Files (x86)/SquareEnix/FINAL FANTASY XIV - A Realm Reborn/game/sqpack"` and `DALAMUD_HOME=/mnt/c/Users/Kevin/AppData/Roaming/XIVLauncher/addon/Hooks/dev`.
- Combat jobs with cards (21): PLD WAR DRK GNB, WHM SCH AST SGE, MNK DRG NIN SAM RPR VPR, BRD MCH DNC, BLM SMN RDM PCT. BLU and BST never get cards.
- Bands: `[start, 9]` (or the job's start band), `10–19` … `90–99`, `100–100`. A job's start level comes from `ClassJob.StartingLevel` (1, 30, 50, 60, 70 or 80).
- Waves, each its own branch and PR: 1 = Tasks 1–4 (engine, windows, tanks); 2 = healers; 3 = melee; 4 = ranged and casters.

## Review Focus

1. A player on a base class (GLA, ACN…) below 30: the guide must pick the job (ACN → SMN, SCH one click away), not show "no job". Test in Task 2.
2. A job whose cards aren't written yet (waves 2–4 pending): the guide shows "Cards for this job are coming", never throws. Test in Task 1 (`Find` returns null).
3. A level outside every band (e.g. slider at 100 when only 90–99 exists): `Find` returns null, not the nearest card. Test in Task 1.
4. A step naming an ability the player's level hasn't reached: drawn greyed with "Lv N", no keybind. Covered by unlock-level data in Task 2 and drawing in Task 4; checklist item in Task 4.
5. A malformed or partly invalid cards file: valid cards still load, bad ones are logged and skipped. Test in Task 1.

---

### Task 1: Core card model, parser, validation and lookup

**Files:**
- Modify: `src/RotationTrainer.Core/Model/Ability.cs` (add `int Level = 0` as the last positional parameter of `Ability`)
- Create: `src/RotationTrainer.Core/Levelling/LevellingCard.cs`
- Create: `src/RotationTrainer.Core/Levelling/LevellingDeck.cs`
- Modify: `tests/RotationTrainer.Core.Tests/Fakes/FakeCatalog.cs` (add a small `PLD` kit with levels: Fast Blade 1, Riot Blade 4, Total Eclipse 6, Fight or Flight 2, Rampart 8, Royal Authority 60, Requiescat 68)
- Test: `tests/RotationTrainer.Core.Tests/LevellingDeckTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record CardVignette(IReadOnlyList<Ability> Steps, string Note, int Threshold = 0);`
  - `public sealed record LevellingCard(string Job, int MinLevel, int MaxLevel, CardVignette Pull, CardVignette Single, IReadOnlyList<Ability> Weave, IReadOnlyList<Ability> Mitigation, string Tip);`
  - `public sealed record CardError(string Card, string Message);` where `Card` is `"PLD 60-69"`.
  - `public sealed class LevellingDeck { public static (LevellingDeck Deck, IReadOnlyList<CardError> Errors) Parse(string json, IAbilityCatalog catalog); public LevellingCard? Find(string job, int level); public bool HasJob(string job); public IReadOnlyList<LevellingCard> ForJob(string job); public IReadOnlyList<LevellingCard> All { get; } }`

JSON shape (array of cards; property names camelCase, `mitigation` and `tip` optional):

```json
[{ "job": "PLD", "minLevel": 60, "maxLevel": 69,
   "pull":   { "threshold": 3, "steps": ["Total Eclipse", "Prominence"], "note": "..." },
   "single": { "steps": ["Fast Blade", "Riot Blade", "Royal Authority"], "note": "..." },
   "weave": ["Fight or Flight"], "mitigation": ["Rampart"], "tip": "..." }]
```

Validation rules (a card with any error is dropped; others load):
- Unknown job → error.
- `minLevel > maxLevel`, or either outside 1–100 → error.
- Empty `pull.steps` or `single.steps` → error.
- Step name not found by `FindActionByName(job, name)` → error naming the step.
- Step `Level > maxLevel` → error naming the step and its level.
- Overlap with an earlier card of the same job → error. (Gaps are checked by the game-data test in Task 3, which knows start levels.)

- [ ] **Step 1: Write the failing tests** in `LevellingDeckTests.cs`:

```csharp
[Fact] public void ParsesValidCardAndResolvesSteps()
// one PLD 60-69 card → Errors empty; Find("PLD", 64) has Single.Steps names ["Fast Blade","Riot Blade","Royal Authority"], Pull.Threshold == 3
[Fact] public void FindReturnsNullOutsideEveryBand()
// cards PLD 60-69 only → Find("PLD", 59) == null, Find("PLD", 70) == null, Find("PLD", 60) != null, Find("PLD", 69) != null
[Fact] public void FindReturnsNullForJobWithoutCards()
// Find("BLM", 50) == null; HasJob("BLM") == false; HasJob("PLD") == true
[Fact] public void UnknownStepDropsOnlyThatCard()
// two cards (PLD 50-59 with "Nonsense Blade", PLD 60-69 valid) → one error with Card "PLD 50-59" and Message containing "Nonsense Blade"; All.Count == 1
[Fact] public void StepAboveBandIsAnError()
// PLD 50-59 listing "Royal Authority" (Level 60) → error Message contains "Royal Authority" and "60"
[Fact] public void OverlappingBandsAreAnError()
// PLD 60-69 and PLD 65-74 → one error on "PLD 65-74"
[Fact] public void UnknownJobAndBadRangeAreErrors()
// job "XYZ" → error; PLD 70-60 → error
[Fact] public void MalformedJsonReturnsErrorNotThrow()
// Parse("{not json", catalog) → Deck.All empty, one error
```

- [ ] **Step 2: Run** `./build.sh --test` → the new tests fail to compile (types missing).
- [ ] **Step 3: Implement** `Ability.Level`, the records and `LevellingDeck` using `System.Text.Json` (same approach as `RotationJson`). `ForJob` returns cards ordered by `MinLevel`.
- [ ] **Step 4: Run** `./build.sh --test` → all core tests pass, including the 210 existing ones.
- [ ] **Step 5: Commit** `feat: levelling card model, parser and lookup`

### Task 2: Game data: unlock levels, job start levels, classes and unlock lists

**Files:**
- Modify: `src/RotationTrainer.GameData/LuminaCatalog.cs`
- Modify: `src/RotationTrainer.Core/Model/Ability.cs` (catalog defaults below)
- Test: `tests/RotationTrainer.GameData.Tests/LevellingDataTests.cs`

**Interfaces:**
- Consumes: `Ability.Level` (Task 1).
- Produces (on `IAbilityCatalog`, with defaults so `FakeCatalog` compiles):
  - `int StartLevel(string job) => 1;` from `ClassJob.StartingLevel`.
  - `IReadOnlyList<string> JobsForClass(string classAbbreviation) => Array.Empty<string>();` combat jobs whose `ClassJobParent` is that class, in sheet order; a job abbreviation maps to itself.
  - `public sealed record Unlock(string Name, int Level, uint IconId, bool IsTrait);` in Core.
  - `IReadOnlyList<Unlock> Unlocks(string job, int minLevel, int maxLevel) => Array.Empty<Unlock>();` player actions (`IsPlayerAction`, job's kit, not role actions) and `Trait` rows whose `ClassJob` is the job or its parent class, ordered by level then name.

- [ ] **Step 1: Write the failing tests** (`[Collection("GameData")]`, `[SqpackFact]`):

```csharp
AbilitiesCarryUnlockLevel:   FindActionByName("PLD","Fast Blade").Level == 1; "Rage of Halone" == 26; "Royal Authority" == 60
StartLevelsMatchSheet:       StartLevel("PLD") == 1; "DRK" == 30; "SAM" == 50; "GNB" == 60; "RPR" == 70; "PCT" == 80
ClassesMapToJobs:            JobsForClass("GLA") == ["PLD"]; JobsForClass("ACN") == ["SMN","SCH"]; JobsForClass("PLD") == ["PLD"]
UnlocksIncludeActionsAndTraits: Unlocks("PLD",60,69) contains ("Royal Authority",60,IsTrait:false), ("Divine Magic Mastery",64,IsTrait:true), ("Requiescat",68,false); every Level in 60..69
```

- [ ] **Step 2: Run** the game-data tests → fail.
- [ ] **Step 3: Implement.** Set `Level` in `ToAbility` from `row.ClassJobLevel`. Read `ClassJob` (English) for start level and parent; read the `Trait` sheet once into a per-job list.
- [ ] **Step 4: Run** `./build.sh --test` with `FFXIV_SQPACK` set → all pass (33 existing + new).
- [ ] **Step 5: Commit** `feat: unlock levels, start levels and class mapping from game data`

### Task 3: Tank cards and the content validation test

**Files:**
- Create: `presets/levelling-cards.json` (PLD, WAR, DRK, GNB: every band from the job's start to 100)
- Modify: `src/RotationTrainer/RotationTrainer.csproj` (`<EmbeddedResource Include="..\..\presets\levelling-cards.json" LogicalName="RotationTrainer.levelling.json" />`)
- Test: `tests/RotationTrainer.GameData.Tests/LevellingContentTests.cs`

**Interfaces:**
- Consumes: `LevellingDeck.Parse`, `IAbilityCatalog.StartLevel` (Tasks 1–2).

Content rules (writer's checklist):
- Each band uses the actions active at the band's top level, with upgrades applied (Royal Authority from 60, not Rage of Halone).
- `pull.threshold`: the enemy count where the AoE combo beats single target for that job and band.
- Tanks fill `mitigation` with what is unlocked by the band's top level.
- Before the first AoE action unlocks, `pull.steps` repeats the single-target combo and `note` says "No AoE yet."
- Copy style per Global Constraints.

- [ ] **Step 1: Write the failing test** `CardsCoverEveryWrittenJob`: parse the file with the real catalog; assert `Errors` is empty; for each job in `deck.All`, cards ordered by `MinLevel` start at the first band for `StartLevel(job)`, have no gaps, and the last card is `100-100`.
- [ ] **Step 2: Run** → fails (file missing).
- [ ] **Step 3: Write** the tank cards.
- [ ] **Step 4: Run** `./build.sh --test` → pass. Fix any card the test names.
- [ ] **Step 5: Commit** `feat: levelling cards for tanks`

### Task 4: Guide and reference card windows

**Files:**
- Create: `src/RotationTrainer/Ui/Levelling/LevellingGuideWindow.cs`
- Create: `src/RotationTrainer/Ui/Levelling/ReferenceCardWindow.cs`
- Create: `src/RotationTrainer/Ui/Levelling/CardChips.cs` (shared icon, keybind and "Lv N" drawing)
- Create: `src/RotationTrainer/Game/LevellingState.cs` (current job, level and the chosen override)
- Modify: `src/RotationTrainer/Configuration.cs` (`ShowLevellingCard = false`, `LockLevellingCard = false`, `LevellingCardOpacity = 0.85f`)
- Modify: `src/RotationTrainer/Plugin.cs` (load `RotationTrainer.levelling.json`, log errors as warnings like `LoadBuiltIns`, add the windows, add the `level` subcommand and update the help text)
- Modify: `src/RotationTrainer/Ui/Settings/PracticeTab.cs` (a "Levelling guide" button)
- Modify: `docs/user-guide.md`, `docs/in-game-checklist.md`

**Interfaces:**
- Consumes: `LevellingDeck`, `IAbilityCatalog.JobsForClass/StartLevel/Unlocks`, `PracticeSession.KeybindLabel(StepType, uint)` (returns `"?"` when not found; draw nothing in that case).
- Produces:
  - `LevellingState(IPlayerState player, IDataManager data, IAbilityCatalog catalog)` with `string? PlayerClass`, `IReadOnlyList<string> PlayerJobs` (from `JobsForClass`), `int PlayerLevel` (`player.EffectiveLevel`, the synced level), `string? SelectedJob`, `int SelectedLevel`, `bool FollowsPlayer`, `void UsePlayer()`.
  - Selecting a job or moving the slider sets `FollowsPlayer = false`; "Use my level" calls `UsePlayer()`. While `FollowsPlayer` is true, each draw copies the player's job and level in. The card window reads the same state.

Behaviour (from the spec and mockup):
- Guide: role-grouped job picker (21 jobs); jobs without cards are dimmed but selectable. Level number plus band label; slider from `StartLevel(job)` to 100. Tabs: Dungeon (pull then single), Trial / boss (single first, mitigation expanded), Unlocks (from `Unlocks`, done ones solid, later ones dashed). Footer: "Pin as card" toggles `ShowLevellingCard`.
- Empty state when `Find` returns null: "Cards for this job are coming."
- Card: title "PLD · Lv 64" (class name when on a class, e.g. "GLA · Lv 22"); rows Single target, 3+ enemies (uses the card's threshold), Off-GCDs, Mitigation (only when non-empty); a tip line. Lock and opacity behave like the trail.
- Chips: game icon via `ITextureProvider.GetFromGameIcon`, keybind in the corner, greyed with "Lv N" when `Ability.Level > SelectedLevel`. Controller labels go through `PadButton.TryParse` like the trail.
- Both windows use `GlassTheme.PushWindow` and `GlassWidgets.DrawGlassBackground`.

- [ ] **Step 1: Write the failing test** for the state logic. Put the pure part in Core as `LevellingSelection` (`Follow(string? job, int level)`, `Select(string job)`, `SetLevel(int)`, `UsePlayer()`, `Job`, `Level`, `FollowsPlayer`) in `src/RotationTrainer.Core/Levelling/LevellingSelection.cs`. Test file: `tests/RotationTrainer.Core.Tests/LevellingSelectionTests.cs`.
  - `FollowsPlayerUntilChanged`: Follow("PLD",22) → Job PLD, Level 22; Follow("PLD",23) → Level 23.
  - `SelectingStopsFollowing`: Select("WAR") → FollowsPlayer false; Follow("PLD",30) leaves Job WAR.
  - `UsePlayerResumes`: after SetLevel(50), UsePlayer() then Follow("PLD",31) → Level 31.
  - `LevelClampsToRange`: SetLevel(150) → 100; SetLevel(0) → 1.
- [ ] **Step 2: Run** → fails.
- [ ] **Step 3: Implement** `LevellingSelection`, then `LevellingState` wrapping it, then the windows, config, command and button.
- [ ] **Step 4: Run** `./build.sh --test` → all pass and the plugin builds without warnings.
- [ ] **Step 5: Docs.** User guide: a "Levelling guide" section (`/rtrain level`, the card, what greys out). Checklist: open the guide on a tank and on GLA below 30; drag the slider and see locked chips; pin the card; check keybinds match the hotbar; open a job without cards and see the empty state; sync down in a dungeon and see the card follow.
- [ ] **Step 6: Commit** `feat: levelling guide and reference card windows`

Wave 1 ends here: push the branch and open a PR only when the user asks.

### Task 5: Healer cards (wave 2)

**Files:** Modify `presets/levelling-cards.json` (WHM SCH AST SGE). No code changes.

- [ ] **Step 1:** Write the cards. Healers' `pull` is the AoE damage loop; `tip` names which heal to use when (one sentence). `mitigation` empty.
- [ ] **Step 2:** `./build.sh --test` → `CardsCoverEveryWrittenJob` passes.
- [ ] **Step 3:** Add the healers to the checklist section. Commit `feat: levelling cards for healers`.

### Task 6: Melee cards (wave 3)

Same as Task 5 for MNK DRG NIN SAM RPR VPR. Positionals go in `note` only when they change the button order. Commit `feat: levelling cards for melee`.

### Task 7: Ranged and caster cards (wave 4)

Same as Task 5 for BRD MCH DNC BLM SMN RDM PCT. Commit `feat: levelling cards for ranged and casters`.
