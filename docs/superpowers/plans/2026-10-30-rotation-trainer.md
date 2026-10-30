# Rotation Trainer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Dalamud plugin that shows an on-screen trail of upcoming abilities for a chosen opener and loop, pops each one as the player uses it, and lets players import, build and export rotations as JSON.

**Architecture:** All rules live in a plain .NET library, `RotationTrainer.Core`, with no Dalamud or Lumina dependency and full xUnit coverage. That covers the models, JSON, library, sequence, timing, engine, trail layout, keybind lookup and builder document. `RotationTrainer.GameData` turns Lumina's Excel sheets into Core's `IAbilityCatalog`. The plugin project `RotationTrainer` is a thin shell: game hooks feed events into Core, and ImGui windows draw what Core computes.

**Tech Stack:** C# / .NET 10, Dalamud.NET.Sdk 15.0.0 (Dalamud 15.0.3.9), Dalamud.Bindings.ImGui, FFXIVClientStructs, Lumina / Lumina.Excel, xUnit, System.Text.Json.

**Spec:** `docs/superpowers/specs/2026-10-30-rotation-trainer-design.md`

## Global Constraints

- .NET SDK 10 (installed in WSL: 10.0.114). Dalamud dev files: `DALAMUD_HOME=/mnt/c/Users/Kevin/AppData/Roaming/XIVLauncher/addon/Hooks/dev` (Dalamud 15.0.3.9).
- Plugin project uses `<Project Sdk="Dalamud.NET.Sdk/15.0.0">`.
- `RotationTrainer.Core` references nothing from Dalamud, Lumina or FFXIVClientStructs.
- No dependency on AethertekUI or Glasswork. Copy no code from FateFrenzy or TTSL (AGPL); use them as visual inspiration only.
- Ship no icon images. Icons come from the game via `ITextureProvider.GetFromGameIcon(new GameIconLookup(iconId))`.
- Level 100 only.
- The plugin never presses, queues or sends any game input.
- Defaults: Use potions **off**; Advance **Manual**; Style **Rail**; Size **Big**.
- JSON format identifier: `"rotation-trainer/1"`.
- A keybind label for an ability that isn't on any bar is exactly `?`.
- Don't add a LICENSE file. The user will choose one.
- Every pixel size in UI code is multiplied by `ImGuiHelpers.GlobalScale`.

## Review Focus

1. **The same ability appearing twice in a row, or twice in a rotation** (Rainbow Drip, Hammer combo): each use must advance exactly one step. Pinned in Task 5 (`SameAbilityTwiceAdvancesOneStepPerUse`).
2. **Actions the trainer should ignore** (Sprint, auto-attacks, other players' actions, mounts): must not flash red or advance. Pinned in Task 5 (`IrrelevantActionIgnored`); the local-player filter is checked by hand in Task 10.
3. **Missing or dangling selections** (no opener, no loop, a deleted rotation, a job with nothing set up): must show an empty state, not crash. Pinned in Task 3 (`BothNullIsEmpty`, `DanglingSelectionResolvesToNull`).
4. **Messy import files** (HQ potion IDs at +1,000,000, different capitalisation or spacing in names, an action from another job): must resolve or be reported per step. Pinned in Task 1.
5. **Countdown edge cases** (cancelled countdown, a second countdown mid-run, using the pre-pull ability during the countdown): reset cleanly, or pop as normal. Pinned in Task 5 (`CountdownCancelledResets`, `NewCountdownMidRunRestarts`, `PrepullPressDuringCountdownPops`).

---

## File Structure

```
RotationTrainer.sln
.gitignore
src/RotationTrainer.Core/
  RotationTrainer.Core.csproj          net10.0 classlib
  Model/Rotation.cs                    Rotation, Step, enums
  Model/Ability.cs                     Ability, AbilityCategory, IAbilityCatalog, ItemIds
  Serialization/RotationJson.cs        import/export
  Library/RotationLibrary.cs           ids, conflicts, upsert, delete
  Library/JobSelection.cs              per-job opener/loop picks
  Practice/PracticeSequence.cs         opener + repeating loop → PlannedStep
  Practice/Timing.cs                   SpeedProfile, SpeedMath, ITimingSource, StatTimingSource
  Practice/Scheduler.cs                expected clock time per step
  Practice/PracticeEngine.cs           manual/auto state machine, run log
  Trail/TrailLayout.cs                 positions for Rail/Timeline/Focus × Big/Small
  Keybinds/KeybindIndex.cs             hotbar observations → label
  Builder/BuilderDocument.cs           editable step list + warnings
src/RotationTrainer.GameData/
  RotationTrainer.GameData.csproj      net10.0, refs Lumina from DALAMUD_HOME
  LuminaCatalog.cs                     IAbilityCatalog over Lumina sheets
src/RotationTrainer/
  RotationTrainer.csproj, RotationTrainer.json
  Plugin.cs, Configuration.cs
  Services/LibraryStore.cs
  Game/ActionListener.cs, CountdownSource.cs, GameTimingSource.cs, HotbarScanner.cs, PracticeSession.cs
  Ui/DebugWindow.cs
  Ui/Glass/GlassTheme.cs, GlassWidgets.cs
  Ui/Settings/SettingsWindow.cs, PracticeTab.cs, TrailTab.cs, TimingTab.cs, LibraryTab.cs, AppearanceTab.cs
  Ui/Trail/TrailWindow.cs
  Ui/Builder/BuilderWindow.cs
tests/RotationTrainer.Core.Tests/       xUnit, + Fakes/FakeCatalog.cs
tests/RotationTrainer.GameData.Tests/   xUnit, skipped unless FFXIV_SQPACK is set
examples/pct-2nd-gcd-starry.json
```

---

### Task 1: Solution, core model and JSON import

**Files:**
- Create: `.gitignore`, `RotationTrainer.sln`, `src/RotationTrainer.Core/RotationTrainer.Core.csproj`, `src/RotationTrainer.Core/Model/Rotation.cs`, `src/RotationTrainer.Core/Model/Ability.cs`, `src/RotationTrainer.Core/Serialization/RotationJson.cs`
- Test: `tests/RotationTrainer.Core.Tests/RotationTrainer.Core.Tests.csproj`, `tests/RotationTrainer.Core.Tests/Fakes/FakeCatalog.cs`, `tests/RotationTrainer.Core.Tests/RotationJsonImportTests.cs`

**Interfaces:**
- Produces (namespace `RotationTrainer.Core`):
  ```csharp
  public enum RotationKind { Opener, Loop }
  public enum StepType { Action, Item, Pull }
  public sealed record Step(StepType Type, uint Id, string Name, double? At = null);
  public sealed record Rotation(string Id, string Job, int Level, RotationKind Kind, string Name,
      IReadOnlyList<Step> Steps, string? Source = null, string? Patch = null);
  public enum AbilityCategory { Spell, Weaponskill, Ability, Item }
  public enum PaletteGroup { Gcd, Weave, Role, Medicine }
  public sealed record Ability(StepType Type, uint Id, string Name, uint IconId, AbilityCategory Category,
      bool IsGcd, int BaseCastMs, int BaseRecastMs, PaletteGroup Group);
  public interface IAbilityCatalog {
      bool IsKnownJob(string job);
      Ability? FindAction(string job, uint id);          // null if not in that job's level-100 kit (incl. role actions)
      Ability? FindActionByName(string job, string name);
      Ability? FindItem(uint id);                         // medicine only; id already normalized
      Ability? FindItemByName(string name);
      IReadOnlyList<Ability> Palette(string job);
  }
  public static class ItemIds { public static uint Normalize(uint id) => id % 1_000_000; }
  public sealed record ImportError(string Rotation, int? Step, string Message);
  public sealed record ImportResult(IReadOnlyList<Rotation> Rotations, IReadOnlyList<ImportError> Errors);
  public static class RotationJson {
      public const string Format = "rotation-trainer/1";
      public static ImportResult Import(string json, IAbilityCatalog catalog, Func<string> newId);
      public static string Export(IEnumerable<Rotation> rotations);   // Task 2
  }
  ```

- [ ] **Step 1: Initialise the repo and scaffold.** Run `git init`. Add a `.gitignore` covering `bin/`, `obj/`, `.vs/` and `.superpowers/`. Create the solution, the Core classlib (`net10.0`, `Nullable` enabled) and the xUnit test project, referencing Core. Add both projects to the sln.

- [ ] **Step 2: Write `FakeCatalog`.** Its IDs are illustrative. It knows jobs `PCT` and `BLM`:
  - PCT GCDs (Spell): Rainbow Drip (1, cast 4000, recast 6000), Wing Motif (2, 3000/4000), Hammer Stamp (3, 0/2500), Fire in Red (4, 1500/2500).
  - PCT weaves (Ability, 0/0): Pom Muse (10), Striking Muse (11), Starry Muse (12).
  - Role: Swiftcast (20), shared by PCT and BLM.
  - Medicine item: Gemdraught of Intelligence (49235).
  - BLM: Fire IV (30, Spell 2000/2500).
  - Name matching is case-insensitive with whitespace collapsed.

- [ ] **Step 3: Write the failing tests** in `RotationJsonImportTests`:
  ```csharp
  ImportsWrappedBatch()                 // {"format":"rotation-trainer/1","rotations":[PCT opener, BLM opener]} → 2 rotations, 0 errors
  ImportsSingleRotationObject()         // bare rotation object → 1 rotation
  ImportsBareArray()                    // [ {...} ] → 1 rotation
  GeneratesIdWhenMissing()              // newId = () => "gen-1" → r.Id == "gen-1"; keeps provided "id" otherwise
  ResolvesStepByNameWhenIdMissing()     // {"name":"rainbow  DRIP","at":-4.5} → Step(Action,1,"Rainbow Drip",-4.5)
  NormalizesHqItemId()                  // {"item":1049235} → Step(Item,49235,"Gemdraught of Intelligence")
  SkipsRotationWithUnknownActionAndNamesStep() // PCT step 2 = {"action":30} → 0 rotations; error Step==2, Message contains "not a PCT action"
  KeepsOtherRotationsWhenOneFails()     // batch of 2, second bad → 1 rotation, 1 error
  RejectsAtAfterPull()                  // error Message contains "only allowed before the pull"
  RejectsNonNegativeAtBeforePull()      // at: 1.0 before pull → error
  RejectsSecondPullAndPullInLoop()      // two pulls in opener → error; pull in loop → error
  RejectsLevelOtherThan100()            // level 90 → error Message contains "level 100"
  RejectsUnknownJobAndKind()            // job "XYZ" → error; kind "burst" → error
  RejectsUnknownFormat()                // format "other/2" → 0 rotations, 1 error with Rotation "(file)"
  MalformedJsonReturnsFileError()       // "{" → Errors.Single().Rotation == "(file)"
  ```

- [ ] **Step 4: Run them and confirm they fail.**
  Run: `dotnet test tests/RotationTrainer.Core.Tests --filter FullyQualifiedName~RotationJsonImportTests`
  Expected: FAIL. The types are missing, so it won't compile.

- [ ] **Step 5: Implement the model types and `RotationJson.Import`.** Parse with `JsonDocument`. Rules:
  - The root may be a wrapper object, a single rotation object or an array.
  - `job` is upper-cased. `kind` is case-insensitive. `level` defaults to 100.
  - A step has exactly one of `action`, `item` or `pull:true`. If it has an ID, the ID wins; otherwise `name` is used. The resolved `Name` is the catalog's name.
  - Step numbers in errors are 1-based.
  - A rotation with no steps or no name is an error. An unnamed rotation is reported as `"(unnamed #n)"`.

- [ ] **Step 6: Run the tests and confirm they pass.** Same command. Expected: PASS.

- [ ] **Step 7: Commit.** `git add -A && git commit -m "feat(core): rotation model and JSON import"`

---

### Task 2: JSON export and rotation library

**Files:**
- Modify: `src/RotationTrainer.Core/Serialization/RotationJson.cs`
- Create: `src/RotationTrainer.Core/Library/RotationLibrary.cs`
- Test: `tests/RotationTrainer.Core.Tests/RotationJsonExportTests.cs`, `tests/RotationTrainer.Core.Tests/RotationLibraryTests.cs`

**Interfaces:**
- Consumes: Task 1 types.
- Produces:
  ```csharp
  public enum ConflictChoice { Replace, KeepBoth }
  public sealed class RotationLibrary {
      public RotationLibrary(IEnumerable<Rotation> rotations);
      public IReadOnlyList<Rotation> All { get; }
      public IEnumerable<Rotation> ForJob(string job, RotationKind? kind = null);
      public Rotation? Get(string id);
      public IReadOnlyList<Rotation> Conflicts(IEnumerable<Rotation> incoming);   // incoming whose Id already exists
      public void Add(IEnumerable<Rotation> incoming, Func<Rotation, ConflictChoice> resolve, Func<string> newId);
      public void Save(Rotation rotation);    // upsert by Id
      public bool Delete(string id);
      public event Action? Changed;
  }
  ```

- [ ] **Step 1: Write the failing tests.**
  ```csharp
  // RotationJsonExportTests
  ExportWritesWrapperAndFormat()       // root.format == "rotation-trainer/1", rotations.Length == 1
  ExportWritesIdAndNameForEachStep()   // {"action":1,"name":"Rainbow Drip","at":-4.5}, {"pull":true}, {"item":49235,"name":...}
  ExportOmitsNullSourceAndPatch()
  ExportWritesKindLowercase()          // "opener" / "loop"
  RoundTripPreservesRotation()         // Import(Export(r)).Rotations.Single() equals r (compare Steps with SequenceEqual)
  // RotationLibraryTests
  ConflictsReturnsIncomingWithExistingIds()
  ReplaceOverwritesInPlace()           // All.Count unchanged, Get(id).Name == new name
  KeepBothAssignsNewIdAndCopySuffix()  // new Id == "gen-1", Name == "2nd GCD Starry Opener (copy)"
  SaveUpsertsById()
  DeleteRemovesAndRaisesChanged()
  ForJobFiltersByJobAndKind()
  ```

- [ ] **Step 2: Run them and confirm they fail.** `dotnet test tests/RotationTrainer.Core.Tests --filter "FullyQualifiedName~RotationJsonExportTests|FullyQualifiedName~RotationLibraryTests"`. Expected: FAIL.

- [ ] **Step 3: Implement `RotationJson.Export`** with an indented `Utf8JsonWriter` and the field order `id, job, level, kind, name, source, patch, steps`. Implement `RotationLibrary` over a `List<Rotation>`, keeping insertion order. Raise `Changed` after every change.

- [ ] **Step 4: Run the tests and confirm they pass.** Expected: PASS.

- [ ] **Step 5: Commit.** `git commit -am "feat(core): JSON export and rotation library"` (run `git add` on the new files first)

---

### Task 3: Practice sequence and job selections

**Files:**
- Create: `src/RotationTrainer.Core/Practice/PracticeSequence.cs`, `src/RotationTrainer.Core/Library/JobSelection.cs`
- Test: `tests/RotationTrainer.Core.Tests/PracticeSequenceTests.cs`

**Interfaces:**
- Consumes: `Rotation`, `IAbilityCatalog`, `RotationLibrary`.
- Produces:
  ```csharp
  public sealed record PlannedStep(Ability Ability, double? At, bool IsPrepull, int? GcdNumber, bool FromLoop);
  public sealed class PracticeSequence {
      public static PracticeSequence Build(Rotation? opener, Rotation? loop, bool usePotions, IAbilityCatalog catalog);
      public static readonly PracticeSequence Empty;
      public bool IsEmpty { get; }
      public int OpenerLength { get; }      // planned opener steps (pull marker excluded)
      public bool IsEndless { get; }        // true when a non-empty loop exists
      public PlannedStep? this[int index] { get; }   // null past the end when not endless
  }
  public sealed class JobSelection { public string? OpenerId { get; set; } public string? LoopId { get; set; }
      public (Rotation? Opener, Rotation? Loop) Resolve(RotationLibrary library, string job); }
  ```

- [ ] **Step 1: Write the failing tests.**
  ```csharp
  OpenerThenLoopRepeats()              // opener 5 planned steps, loop 3 → seq[5] is loop[0], seq[8] is loop[0] again, FromLoop true
  PullMarkerIsNotAStep()
  PrepullStepsCarryAtAndFlag()         // seq[0].At == -4.5, IsPrepull true; seq[1].IsPrepull false
  PotionsHiddenWhenOff()               // usePotions:false → no step with Category Item
  PotionsShownWhenOn()
  GcdNumbersSkipWeavesAndPrepull()     // first post-pull GCD has GcdNumber 1, weaves null, prepull null; loop restarts at 1
  NoOpenerStartsWithLoop()
  NoLoopEndsAfterOpener()              // seq[OpenerLength] == null, IsEndless false
  BothNullIsEmpty()                    // IsEmpty true, seq[0] == null
  DanglingSelectionResolvesToNull()    // OpenerId points to deleted id → Opener null
  SelectionForOtherJobResolvesToNull() // OpenerId is a BLM rotation, job "PCT" → null
  ```
  Steps whose ability the catalog no longer knows are dropped when the sequence is built.

- [ ] **Step 2: Run them and confirm they fail.** `--filter FullyQualifiedName~PracticeSequenceTests`. Expected: FAIL.
- [ ] **Step 3: Implement both types.** Map each `Step` to an `Ability` with `catalog.FindAction(rotation.Job, id)` or `FindItem(id)`. Precompute the opener list and the loop list. The indexer wraps around the loop.
- [ ] **Step 4: Run the tests and confirm they pass.** Expected: PASS.
- [ ] **Step 5: Commit.** `feat(core): practice sequence and job selections`

---

### Task 4: Speed math and scheduler

**Files:**
- Create: `src/RotationTrainer.Core/Practice/Timing.cs`, `src/RotationTrainer.Core/Practice/Scheduler.cs`
- Test: `tests/RotationTrainer.Core.Tests/TimingTests.cs`, `tests/RotationTrainer.Core.Tests/SchedulerTests.cs`

**Interfaces:**
- Consumes: `Ability`, `PlannedStep`.
- Produces:
  ```csharp
  public sealed record SpeedProfile(int SpellSpeed, int SkillSpeed);
  public static class SpeedMath { public const int Sub = 420; public const int Div = 2780;
      public static double AdjustSeconds(int baseMs, int speed); }
  public interface ITimingSource { double CastSeconds(Ability a); double RecastSeconds(Ability a); }
  public sealed class StatTimingSource(SpeedProfile profile) : ITimingSource;
  public sealed class Scheduler {
      public Scheduler(ITimingSource timing, double animationLockSeconds = 0.65);
      public double Next(PlannedStep step);   // expected clock time of this step; call in sequence order
      public void Reset();
  }
  ```

- [ ] **Step 1: Write the failing tests.**
  ```csharp
  // TimingTests — AdjustSeconds(baseMs, speed)
  [InlineData(2500, 420, 2.50)] [InlineData(2500, 744, 2.46)] [InlineData(2500, 1020, 2.43)] [InlineData(1500, 420, 1.50)]
  SpellsUseSpellSpeedWeaponskillsUseSkillSpeed()
  AbilitiesAndItemsAreNotScaled()
  // SchedulerTests (StatTimingSource with speed 420, lock 0.65, FakeCatalog timings)
  OpenerTimesMatchRules()
  // [RD at -4.5] [pull] [Gemdraught] [Pom] [Striking] [Wing Motif] [Starry] [Hammer Stamp]
  // expected: -4.5, 0.0, 0.65, 1.30, 1.95, 4.95, 5.95   (tolerance 1e-6)
  PrepullWithoutAtFollowsPrevious()    // second prepull step w/o At → previous + max(cast, lock)
  FirstPrepullWithoutAtIsJustBeforePull() // → -0.65
  ResetStartsOver()
  ```
- [ ] **Step 2: Run them and confirm they fail.** Expected: FAIL.
- [ ] **Step 3: Implement.**
  - `AdjustSeconds`: `f = floor(130 * (speed - Sub) / Div) + 1000`; `ms = floor((2000 - f) * baseMs / 1000)`; return `floor(ms / 10) / 100.0`.
  - Scheduler state: `nextGcdAt`, `nextActionAt`, `pulled`.
    - A prepull step with `At` uses `At`.
    - The first post-pull step first sets `nextActionAt = max(nextActionAt, 0)`.
    - **GCD:** `t = max(nextGcdAt, nextActionAt)`; `nextGcdAt = t + recast`; `nextActionAt = t + max(cast, lock)`.
    - **Weave or item:** `t = nextActionAt`; `nextActionAt = t + lock`.
- [ ] **Step 4: Run the tests and confirm they pass.** Expected: PASS.
- [ ] **Step 5: Commit.** `feat(core): speed math and scheduler`

---

### Task 5: Practice engine

**Files:**
- Create: `src/RotationTrainer.Core/Practice/PracticeEngine.cs`
- Test: `tests/RotationTrainer.Core.Tests/PracticeEngineTests.cs`

**Interfaces:**
- Consumes: `PracticeSequence`, `Scheduler`, `ItemIds`.
- Produces:
  ```csharp
  public enum AdvanceMode { Manual, Auto }
  public enum EngineEventKind { Popped, WrongPress, Paused, Resumed, Finished }
  public sealed record EngineEvent(EngineEventKind Kind, int Index);
  public sealed record StepResult(int Index, uint AbilityId, double Expected, double Actual, int WrongPresses);
  public sealed class PracticeEngine {
      public PracticeEngine(PracticeSequence sequence, Scheduler scheduler, AdvanceMode mode, Func<StepType, uint, bool> isRelevant);
      public int CurrentIndex { get; }
      public double? Clock { get; }          // null until started
      public bool IsPaused { get; }          // Auto only
      public bool IsFinished { get; }
      public double ExpectedTime(int index); // cached scheduler results, computed lazily in order
      public IReadOnlyList<StepResult> RunLog { get; }
      public event Action<EngineEvent>? Event;
      public void OnActionUsed(StepType type, uint id);
      public void OnCountdown(double secondsRemaining);
      public void OnCountdownCancelled();
      public void Tick(double deltaSeconds);
      public void Reset();
  }
  ```

- [ ] **Step 1: Write the failing tests.** These use the opener from Task 4, plus a loop of `[Fire in Red]` where noted.
  ```csharp
  ManualPopsOnCorrectAction()            // Event Popped(0); CurrentIndex 1
  ManualWrongActionFlashesAndWaits()     // use Swiftcast at index 0 → WrongPress(0), index stays 0; after correct use RunLog[0].WrongPresses == 1
  IrrelevantActionIgnored()              // isRelevant false (Sprint) → no event, index unchanged
  SameAbilityTwiceAdvancesOneStepPerUse()// loop [Fire in Red]; each use advances exactly 1
  HqPotionMatches()                      // OnActionUsed(Item, 1049235) pops Gemdraught step
  FirstPressStartsClockAtItsExpectedTime() // no countdown; use RD → Clock == -4.5
  CountdownSetsClock()                   // OnCountdown(5) → Clock == -5
  AutoPausesWhenStepReachesMarker()      // Auto; OnCountdown(5); Tick(0.5) → IsPaused, Event Paused(0); Tick(1) → Clock still -4.5
  AutoResumesAfterLatePress()            // then use RD → Popped(0), Resumed; Tick(0.2) → Clock -4.3
  AutoEarlyPressPopsImmediately()        // OnCountdown(5); use RD at Clock -5 → Popped(0), Clock still -5
  ManualClockNeverPauses()               // Manual; OnCountdown(5); Tick(2) → Clock -3, IsPaused false
  PrepullPressDuringCountdownPops()      // Manual; OnCountdown(5); Tick(0.3); use RD → Popped(0)
  CountdownCancelledResets()             // → CurrentIndex 0, Clock null, RunLog empty
  NewCountdownMidRunRestarts()           // pop 3 steps, OnCountdown(5) → CurrentIndex 0, Clock -5
  FinishedWhenNoLoop()                   // pop every opener step → Finished event, IsFinished, further uses ignored
  RunLogRecordsExpectedAndActual()       // RunLog[i].Expected == ExpectedTime(i), Actual == Clock at pop
  ResetClearsEverything()
  ```
- [ ] **Step 2: Run them and confirm they fail.** Expected: FAIL.
- [ ] **Step 3: Implement.**
  - Item IDs are normalized with `ItemIds.Normalize` before matching.
  - In Auto, `Tick` advances the clock, but never past `ExpectedTime(CurrentIndex)` while that step is unpopped. Reaching it raises `Paused` once.
  - In Manual, the clock always advances once started.
  - A wrong press counts against the current index.
- [ ] **Step 4: Run the tests and confirm they pass.** Expected: PASS.
- [ ] **Step 5: Commit.** `feat(core): practice engine`

---

### Task 6: Trail layout

**Files:**
- Create: `src/RotationTrainer.Core/Trail/TrailLayout.cs`
- Test: `tests/RotationTrainer.Core.Tests/TrailLayoutTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public enum TrailStyle { Rail, Timeline, Focus }
  public enum TrailSize { Big, Small }
  public enum TrailRow { Gcd, Weave }
  public sealed record TrailMetrics(float Gcd, float Weave, float PixelsPerSecond) {
      public static TrailMetrics For(TrailSize size); }      // Big (56, 40, 72), Small (38, 28, 48)
  public readonly record struct TrailInput(int Index, bool IsGcd, double Expected);
  public sealed record TrailItem(int Index, float CenterX, TrailRow Row, float Size, float Scale, bool IsCurrent);
  public static class TrailLayout {
      public static IReadOnlyList<TrailItem> Layout(TrailStyle style, TrailSize size,
          IReadOnlyList<TrailInput> upcoming, double referenceTime, float markerX);
      // upcoming[0] is the current step; caller passes at most "abilities ahead" + 1 items
  }
  ```
  Unscaled units. The plugin multiplies by `GlobalScale`. Callers use `referenceTime = Clock` in Auto and `ExpectedTime(current)` otherwise.

- [ ] **Step 1: Write the failing tests.**
  ```csharp
  RailPutsCurrentAtMarker()            // items[0].CenterX == markerX
  RailSpacing()                        // Big, [GCD, weave, weave, GCD], marker 110 → CenterX 110, 190, 238, 298
                                       // (unshifted 28, 108, 156, 216: weave gap 8, +4 before a GCD after weaves, 32 after a GCD)
  FocusUsesGap22AndScalesCurrent()     // [GCD, GCD] → 110, 188; items[0].Scale == 1.3f, others 1f
  TimelineUsesPixelsPerSecond()        // Big, reference 0, expected [0, 2.5] → 110, 290
  WeavesUseWeaveRowAndSize()           // Row Weave, Size 40 (Big) / 28 (Small)
  SmallMetrics()                       // TrailMetrics.For(Small) == (38, 28, 48)
  ```
- [ ] **Step 2: Run them and confirm they fail.** Expected: FAIL.
- [ ] **Step 3: Implement `Layout`.** For Rail and Focus, accumulate x as in the comments, then shift so `upcoming[0]` lands on `markerX`. For Timeline, use `markerX + (Expected - referenceTime) * pps`.
- [ ] **Step 4: Run the tests and confirm they pass.** Expected: PASS.
- [ ] **Step 5: Commit.** `feat(core): trail layout`

---

### Task 7: Keybind index

**Files:**
- Create: `src/RotationTrainer.Core/Keybinds/KeybindIndex.cs`
- Test: `tests/RotationTrainer.Core.Tests/KeybindIndexTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public enum KeybindMode { Auto, Keyboard, Controller, Off }
  public enum InputMode { Keyboard, Controller }
  public sealed record HotbarObservation(int Bar, int Slot, bool IsCross, uint CommandId, uint ApparentActionId, string? Hint);
  public sealed class KeybindIndex {
      public const string Unknown = "?";
      public void Update(IReadOnlyList<HotbarObservation> snapshot);
      public string Label(uint actionId, InputMode mode);
      public void Clear();
  }
  ```
- [ ] **Step 1: Write the failing tests.**
  ```csharp
  ApparentMatchReturnsHint()               // slot shows Fire in Red with hint "1" → Label(FireInRed) == "1"
  LearnsReplacedAction()                   // snapshot A: slot(CommandId FireInRed, Apparent Blizzard, "1"); snapshot B: apparent back to Fire → Label(Blizzard) == "1"
  LearnedSlotForgottenWhenCommandChanges() // later the slot's CommandId changes → Label(Blizzard) == "?"
  CommandIdMatchWhenNotApparent()
  NotOnAnyBarIsQuestionMark()              // == "?"
  EmptyHintIsQuestionMark()
  ModeFiltersBarType()                     // keyboard ignores IsCross slots; controller uses only IsCross
  PrefersLowestBarThenSlot()
  ClearForgetsLearned()
  ```
- [ ] **Step 2: Run them and confirm they fail.** Expected: FAIL.
- [ ] **Step 3: Implement the lookup order:** an apparent match in the current snapshot, then a learned apparent ID whose `(Bar, Slot, IsCross)` still has the same `CommandId`, then a `CommandId` match, then `?`.
- [ ] **Step 4: Run the tests and confirm they pass.** Expected: PASS.
- [ ] **Step 5: Commit.** `feat(core): keybind index`

---

### Task 8: Builder document

**Files:**
- Create: `src/RotationTrainer.Core/Builder/BuilderDocument.cs`
- Test: `tests/RotationTrainer.Core.Tests/BuilderDocumentTests.cs`

**Interfaces:**
- Consumes: `Rotation`, `Step`, `IAbilityCatalog`.
- Produces:
  ```csharp
  public sealed record BuilderWarning(int? StepIndex, string Message);
  public sealed class BuilderDocument {
      public BuilderDocument(string job, RotationKind kind, string name, IAbilityCatalog catalog);
      public static BuilderDocument Edit(Rotation rotation, IAbilityCatalog catalog);
      public string? Id { get; }  public string Job { get; }  public string Name { get; set; }  public RotationKind Kind { get; set; }
      public IReadOnlyList<Step> Steps { get; }
      public int PullIndex { get; }                    // -1 if none
      public void Insert(int index, Step step);        // Pull ignored for loops and when a pull exists
      public void Move(int from, int insertBefore);    // insertBefore is in pre-removal indices (as in the mockup)
      public void Remove(int index);
      public void SetAt(int index, double? at);        // only stored for steps before the pull
      public bool IsPrepull(int index);
      public IReadOnlyList<BuilderWarning> Warnings();
      public Rotation ToRotation(Func<string> newId);  // keeps Id when editing; drops At on post-pull steps
  }
  ```
- [ ] **Step 1: Write the failing tests.**
  ```csharp
  NewOpenerStartsWithPull()               // Steps == [Pull]
  NewLoopStartsEmptyAndRejectsPull()
  InsertAndMove()                          // Move(3, 1) and Move(0, 3) produce the mockup's ordering
  ThreeWeavesAfterGcdWarns()               // Message == "3 weaves after Wing Motif may clip your GCD", flags all three
  WeavesRightAfterPullDoNotWarn()          // potion + 2 muses after pull, no GCD before → no warning
  OpenerWithoutPullWarns()                 // "No Pull marker. Openers need one to line up the countdown."
  ToRotationKeepsIdWhenEditing()
  ToRotationDropsAtAfterPull()
  ```
- [ ] **Step 2: Run them and confirm they fail.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the tests and confirm they pass.** Then run the whole suite: `dotnet test tests/RotationTrainer.Core.Tests`. Expected: all PASS.
- [ ] **Step 5: Commit.** `feat(core): builder document`

---

### Task 9: Game data catalog and plugin shell

**Files:**
- Create: `src/RotationTrainer.GameData/RotationTrainer.GameData.csproj`, `src/RotationTrainer.GameData/LuminaCatalog.cs`
- Create: `src/RotationTrainer/RotationTrainer.csproj`, `src/RotationTrainer/RotationTrainer.json`, `src/RotationTrainer/Plugin.cs`, `src/RotationTrainer/Configuration.cs`, `src/RotationTrainer/Services/LibraryStore.cs`
- Test: `tests/RotationTrainer.GameData.Tests/RotationTrainer.GameData.Tests.csproj`, `tests/RotationTrainer.GameData.Tests/LuminaCatalogTests.cs`

**Interfaces:**
- Consumes: `IAbilityCatalog`, `RotationJson`, `RotationLibrary`, `JobSelection`, the enums from Tasks 5–7.
- Produces:
  ```csharp
  public sealed class LuminaCatalog : IAbilityCatalog { public LuminaCatalog(Lumina.Excel.ExcelModule excel); }
  public sealed class Configuration : IPluginConfiguration {
      public int Version { get; set; } = 1;
      public Dictionary<string, JobSelection> Selections { get; set; } = new();
      public bool UsePotions { get; set; } = false;
      public bool StartWithCountdown { get; set; } = true;
      public bool ResetOnLeavingCombat { get; set; } = true;
      public bool ShowTrail { get; set; } = true;
      public TrailStyle Style { get; set; } = TrailStyle.Rail;
      public TrailSize Size { get; set; } = TrailSize.Big;
      public AdvanceMode Advance { get; set; } = AdvanceMode.Manual;
      public int AbilitiesAhead { get; set; } = 8;
      public KeybindMode Keybinds { get; set; } = KeybindMode.Auto;
      public bool FlashWrong { get; set; } = true;
      public bool LockTrail { get; set; } = false;
      public bool UseCustomSpeed { get; set; } = false;
      public int CustomSpellSpeed { get; set; } = 420;
      public int CustomSkillSpeed { get; set; } = 420;
      public int LatencyMs { get; set; } = 50;            // animation lock = 0.6 + LatencyMs/1000
      public Accent Accent { get; set; } = Accent.Sky;    // enum Accent { Sky, Violet, Mint, Gold }
      public float GlassOpacity { get; set; } = 0.7f;
      public bool TrailGlassStrip { get; set; } = true;
  }
  public sealed class LibraryStore {    // library.json in PluginInterface.GetPluginConfigDirectory()
      public LibraryStore(string directory, IAbilityCatalog catalog, IPluginLog log);
      public RotationLibrary Load();     // via RotationJson.Import; logs per-rotation errors
      public void Save(RotationLibrary library);   // via RotationJson.Export, on library.Changed
  }
  ```
  `Plugin` has the services `IDalamudPluginInterface`, `ICommandManager`, `IDataManager`, `ITextureProvider`, `IClientState`, `ICondition`, `IFramework`, `IGameInteropProvider`, `IPlayerState`, `IGamepadState` and `IPluginLog`. It registers `/rtrain` (toggle settings), `/rtrain trail`, `/rtrain builder` and `/rtrain debug`.

- [ ] **Step 1: Write the failing integration tests.** Each is skipped when the `FFXIV_SQPACK` environment variable is unset. They build a `Lumina.GameData` from that path.
  ```csharp
  PctKitHasRainbowDripAsGcd()            // FindActionByName("PCT","Rainbow Drip").IsGcd && Category == Spell
  PctMusesAreWeaves()                    // "Pom Muse".IsGcd == false
  RoleActionsIncluded()                  // FindActionByName("PCT","Swiftcast") != null, Group == Role
  OtherJobsActionsExcluded()             // FindActionByName("PCT","Fire IV") == null
  MedicineFoundByNameAndNormalizedId()
  PaletteGroupsPopulated()               // Palette("PCT") has Gcd, Weave, Role, Medicine entries
  AllCombatJobsKnown()                   // IsKnownJob for every combat job abbreviation (ClassJob rows with JobIndex > 0, excluding DoH/DoL)
  ```
- [ ] **Step 2: Run them and confirm they fail.** `FFXIV_SQPACK="/mnt/c/Program Files (x86)/SquareEnix/FINAL FANTASY XIV - A Realm Reborn/game/sqpack" dotnet test tests/RotationTrainer.GameData.Tests`. Adjust the path to the user's install. Expected: FAIL.
- [ ] **Step 3: Implement `LuminaCatalog`.** The GameData csproj references `Lumina.dll` and `Lumina.Excel.dll` through a `HintPath` under `$(DALAMUD_HOME)`, with `Private=false`. Rules:
  - **Player actions:** the job's kit is player actions (`IsPlayerAction`, not PvP, `ClassJobLevel <= 100`) whose `ClassJobCategory` includes the job.
  - **GCD:** `CooldownGroup == 58`.
  - **Category:** from `ActionCategory` (Spell / Weaponskill / Ability).
  - **Base timings:** `Cast100ms * 100` and `Recast100ms * 100`.
  - **Role group:** actions shared with the job's role. Look it up from `ClassJobCategory` rows such as "All tanks / melee / ranged / casters / healers".
  - **Medicine:** items in the Medicine UI category that grant a main-stat buff ("Gemdraught" or "Tincture"). In the palette, the newest grades come first.
- [ ] **Step 4: Run the tests and confirm they pass.** Expected: PASS.
- [ ] **Step 5: Implement the plugin shell.** Write the csproj (Dalamud SDK, referencing Core and GameData), the manifest (`Name` "Rotation Trainer", `InternalName` "RotationTrainer", `Punchline` "Drill openers and rotations into muscle memory.", `Tags` ["rotation","training","opener"]), `Plugin`, `Configuration` and `LibraryStore`. The commands open placeholder windows for now.
- [ ] **Step 6: Build.** `DALAMUD_HOME=/mnt/c/Users/Kevin/AppData/Roaming/XIVLauncher/addon/Hooks/dev dotnet build src/RotationTrainer -c Debug`. Expected: `Build succeeded`, with 0 errors.
- [ ] **Step 7: Check it in game (manual).** In `/xlsettings` → Experimental → Dev Plugin Locations, add `C:\Projects\RotationTrainer\src\RotationTrainer\bin\x64\Debug\RotationTrainer.dll`, or whichever output path the build prints. Enable the plugin and run `/rtrain`. Expected: a window opens, and the Dalamud log has no errors.
- [ ] **Step 8: Commit.** `feat: Lumina catalog and plugin shell`

---

### Task 10: Game inputs and practice session

**Files:**
- Create: `src/RotationTrainer/Game/ActionListener.cs`, `Game/CountdownSource.cs`, `Game/GameTimingSource.cs`, `Game/HotbarScanner.cs`, `Game/PracticeSession.cs`, `src/RotationTrainer/Ui/DebugWindow.cs`

**Interfaces:**
- Consumes: `PracticeEngine`, `PracticeSequence`, `Scheduler`, `StatTimingSource`, `KeybindIndex`, `Configuration`, `RotationLibrary`, `LuminaCatalog`.
- Produces:
  ```csharp
  public sealed class ActionListener : IDisposable { public event Action<StepType, uint>? ActionUsed; }  // local player only; ActionType Action→Action, Item→Item
  public sealed class CountdownSource { public double? SecondsRemaining { get; } public void Update(); }
  public sealed class GameTimingSource : ITimingSource { }   // ActionManager.GetAdjustedCastTime / GetAdjustedRecastTime (ms → s)
  public sealed class HotbarScanner { public IReadOnlyList<HotbarObservation> Scan(); }  // 10 standard + 8 cross bars; Hint from slot keybind hint
  public sealed class PracticeSession : IDisposable {
      public string? Job { get; }
      public PracticeSequence Sequence { get; }
      public PracticeEngine? Engine { get; }
      public KeybindIndex Keybinds { get; }
      public InputMode InputMode { get; }
      public event Action<EngineEvent>? EngineEvent;
      public void Rebuild();      // on job change, selection/library/config change
      public void Restart();
  }
  ```
  Behaviour of `PracticeSession`:
  - On `IFramework.Update` it calls `Engine.Tick(dt)`.
  - It polls the countdown. A countdown appearing or rising calls `OnCountdown`, but only when `StartWithCountdown` is on. A countdown disappearing while `Clock < 0` calls `OnCountdownCancelled`.
  - It rescans hotbars every 500 ms.
  - When `ResetOnLeavingCombat` is on, an `InCombat` change from true to false calls `Reset`.
  - It calls `Rebuild` on job change (`IClientState.ClassJobChanged`), and the job change also clears `Keybinds`.
  - `isRelevant` is `catalog.FindAction(job, id) != null`, or `catalog.FindItem(ItemIds.Normalize(id)) != null` for items.
  - The timing source is `GameTimingSource`, or `StatTimingSource` when `UseCustomSpeed` is on. The animation lock is `0.6 + LatencyMs / 1000`.
  - `InputMode` is Controller when `Keybinds == Controller`, or when it's Auto and the gamepad is in use (the builder decides how to detect that). Otherwise it's Keyboard.

- [ ] **Step 1: Implement `ActionListener`.** Hook `ActionEffectHandler.Receive` through `IGameInteropProvider`. Filter to the local player's entity ID, and dispose the hook in `Dispose`.
- [ ] **Step 2: Implement `CountdownSource`.** The builder chooses how to read the in-game countdown. Document the chosen approach in a code comment with its source.
- [ ] **Step 3: Implement `GameTimingSource`, `HotbarScanner` and `PracticeSession`.** Add a `DebugWindow` (`/rtrain debug`) that shows:
  - the last action used (type and ID)
  - countdown seconds
  - the engine's index, clock, paused state and current step name
  - the current step's keybind label
  - the GCD (`RecastSeconds` of a GCD in the current kit)
- [ ] **Step 4: Build.** Use the command from Task 9. Expected: `Build succeeded`.
- [ ] **Step 5: Check it in game (manual)** on PCT against a striking dummy, after importing `examples/pct-2nd-gcd-starry.json`. Write that file now, using real IDs from the catalog. Expected in the debug window:
  - Using Rainbow Drip advances the index by one.
  - Sprint changes nothing.
  - A party member's actions change nothing.
  - `/countdown 10` shows seconds counting down, and the clock reads −10 when it starts.
  - Cancelling the countdown resets the index to 0.
  - The keybind shows your real key, and `?` for an ability removed from all bars.
  - The GCD matches the game's character sheet.
- [ ] **Step 6: Commit.** `feat: game inputs and practice session`

---

### Task 11: Glass styling and settings window

**Files:**
- Create: `src/RotationTrainer/Ui/Glass/GlassTheme.cs`, `Ui/Glass/GlassWidgets.cs`, `Ui/Settings/SettingsWindow.cs`, `PracticeTab.cs`, `TrailTab.cs`, `TimingTab.cs`, `LibraryTab.cs`, `AppearanceTab.cs`

**Interfaces:**
- Consumes: `Configuration`, `RotationLibrary`, `LibraryStore`, `PracticeSession`, `RotationJson`, `LuminaCatalog`.
- Produces:
  ```csharp
  public static class GlassTheme {
      public static Vector4 AccentColor(Accent a);   // Sky #8FB4FF, Violet #C9A6FF, Mint #7CF0B0, Gold #FFD27A
      public const float WindowRounding = 16f, CardRounding = 12f, FrameRounding = 8f;
      public static IDisposable PushWindow(float opacity);   // style vars/colors for a glass window
  }
  public static class GlassWidgets {
      public static void DrawGlassBackground(float opacity);   // gradient rgba(40,46,64,.78)→(18,20,30,.82) × opacity, 1px border white .18, top highlight white .28, soft shadow
      public static bool SidebarTab(string label, FontAwesomeIcon icon, bool selected);
      public static void SectionHeader(string title, string subtitle);   // 1.4× title, dim subtitle, 40×3 accent bar
      public static IDisposable Card(string id);
      public static bool Segmented<T>(string id, ref T value, params (T Value, string Label)[] options);
      public static bool Toggle(string id, string label, string? help, ref bool value);
      public static void Chip(string text, Vector4 color);
  }
  ```
- [ ] **Step 1: Implement `GlassTheme` and `GlassWidgets`.**
- [ ] **Step 2: Implement `SettingsWindow`** with a 180 px sidebar holding the tabs Practice, Trail, Speed & timing, Rotation library and Appearance. Use the copy from the mockup at https://claude.ai/artifact/VhEiD9ctbgcmzwQB9KxaHe. Tab contents:
  - **Practice.** The job follows the current job. The opener and "Then loop" combos list `library.ForJob(job, kind)` and write to `Selections[job]`. Show an opener preview as small icon strip, then the Use potions, Start with in-game countdown and Reset when leaving combat toggles, and the buttons Show trail and Restart opener. If no rotations exist for the job, show "No rotations for {job} yet. Import a file or open the builder." with both buttons.
  - **Trail.** Style, Size and Advance (labels "When I use it" / "Auto") as segmented controls. An "Abilities ahead" slider from 3 to 15, a Keybind labels segmented control, and the Flash red on a wrong ability and Lock position toggles.
  - **Speed & timing.** Show the GCD read from the character, a Source segmented control (My character / Custom), the custom spell and skill speed inputs with their computed GCD (`SpeedMath.AdjustSeconds(2500, speed)`), and the latency allowance.
  - **Rotation library.** A table of Job, Name, Kind and Notes, with potion and pre-pull chips in Notes. Buttons:
    - New in builder.
    - Import JSON… uses Dalamud's `FileDialogManager`.
    - Paste from clipboard.
    - Export selected uses a save dialog.
    - Per-row Edit, Export and Delete. Delete asks for confirmation inline.

    After an import, show a result line with the count added and each `ImportError` ("{Rotation}" step {n}: {message}). Conflicts open a modal that lists each conflicting rotation with Replace / Keep both.
  - **Appearance.** Accent, Glass opacity and Trail backing.
- [ ] **Step 3: Build.** Expected: `Build succeeded`.
- [ ] **Step 4: Check it in game (manual).** Expected:
  - `/rtrain` shows the glass window with the sidebar.
  - Every control changes `Configuration` and the change survives a plugin reload.
  - Importing the example adds it.
  - Importing it again prompts Replace / Keep both.
  - A file with a bad step shows the step-numbered error and still imports the rest.
  - The window stays legible at Dalamud's global font scale 1.5.
- [ ] **Step 5: Commit.** `feat(ui): glass styling and settings window`

---

### Task 12: Trail overlay

**Files:**
- Create: `src/RotationTrainer/Ui/Trail/TrailWindow.cs`

**Interfaces:**
- Consumes: `PracticeSession`, `TrailLayout`, `TrailMetrics`, `KeybindIndex`, `Configuration`, `GlassTheme`, `ITextureProvider`.

- [ ] **Step 1: Implement `TrailWindow`** as a Dalamud `Window` with no title bar and no background. When `LockTrail` is on, it uses `NoInputs | NoMove`. When unlocked, it shows a drag outline and stays movable. Each frame:
  - **Inputs:** build the `TrailInput`s for `Engine.CurrentIndex .. +AbilitiesAhead`, using `referenceTime = Clock` in Auto and `ExpectedTime(current)` otherwise. Call `TrailLayout.Layout` with a marker x of 110 × scale.
  - **Rows:** GCD row y = `34 + raise`, where `raise = 0.6 × Gcd` for Rail and Focus and `0.95 × Gcd` for Timeline. The weave row is `raise` higher.
  - **Each item:** the game icon, a keybind chip above it (`Keybinds.Label` with the session's `InputMode`; hidden when the mode is `Off`), rounded corners, and a white ring with a glow on the current step.
  - **Pre-pull steps:** a "−4.5s" tag under the icon, and a gold pulse once `Clock >= At`.
  - **Movement:** ease the displayed offset toward the target over 280 ms, except Timeline in Auto, which follows the clock directly. A popped step scales to 1.35 and fades out over 220 ms. A `WrongPress` event (when `FlashWrong` is on) flashes the current step red for 400 ms.
  - **Rail extras:** the rail line and a "Pull" divider. **Timeline extras:** a fixed marker line and one-second ticks labelled every 5 s.
  - **Glass strip** behind everything when `TrailGlassStrip` is on.
  - **Caption** bottom-left: a GCD, WEAVE or POTION chip and "Next: {name}", plus " · cast in {s}s" before a pre-pull step is due.
  - **Countdown number** bottom-right while `Clock < 0`, "Start" for 0.8 s at 0.
  - **Empty state:** "Pick an opener and loop with /rtrain".
  - **When finished with no loop:** "Opener complete".
- [ ] **Step 2: Build.** Expected: `Build succeeded`.
- [ ] **Step 3: Check it in game (manual)** against the mockup at https://claude.ai/artifact/7apZhMyHbN3Mn8nkmkiyzY, for each of Rail, Timeline and Focus in both Big and Small:
  - The real icons and keybinds show.
  - Weaves sit higher.
  - Steps pop on use, and a wrong ability flashes red.
  - `/countdown 5` lights Rainbow Drip at 4.5.
  - In Auto, the trail pauses when you stop pressing and resumes when you press.
  - Leaving combat resets it.
  - With potions off, the Gemdraught step is absent; with them on, it's present.
- [ ] **Step 4: Commit.** `feat(ui): trail overlay`

---

### Task 13: Rotation builder window

**Files:**
- Create: `src/RotationTrainer/Ui/Builder/BuilderWindow.cs`

**Interfaces:**
- Consumes: `BuilderDocument`, `IAbilityCatalog.Palette`, `RotationLibrary.Save`, `RotationJson.Export`, `GlassWidgets`, `ITextureProvider`.
- Produces: `public void Open(BuilderDocument document)`, called by the Library tab's New and Edit buttons and by `/rtrain builder` (a new opener for the current job).

- [ ] **Step 1: Implement `BuilderWindow`**, following the mockup at https://claude.ai/artifact/QJrwJybifs6BQvVQPKh7eU.
  - **Top row:** a Job combo, a Name input, an Opener/Loop segmented control, Clear, and "Save to library", which calls `library.Save(doc.ToRotation(newId))`.
  - **Palette:** grouped GCD / Weaves / Role / Medicine. Each icon is a drag source with payload `"RT_ABILITY"` (type and ID). Clicking an icon inserts it after the selected step.
  - **Timeline:** horizontal scrolling, with GCDs on the line and weaves raised. Post-pull GCDs are numbered. There's a drop target between each pair of items and at both ends, and the insertion point is highlighted with an accent bar.
  - **Steps:** each step is a drag source with payload `"RT_STEP"` (its index). The Pull marker is draggable the same way. Left and Right arrows move the selected step and Delete removes it.
  - **Inspector:** the selected step's name and type. For pre-pull steps, a "Use at countdown" input that calls `SetAt` with a negative value. For potions, a note that they're shown when "Use potions" is on. Below that, `Warnings()` in gold, or "No problems found".
  - **JSON panel:** shows `RotationJson.Export([doc.ToRotation(...)])`, with a Copy button (`ImGui.SetClipboardText`) and a bin drop target.
- [ ] **Step 2: Build.** Expected: `Build succeeded`.
- [ ] **Step 3: Check it in game (manual).** Build the 2nd GCD Starry opener from scratch. Expected:
  - Saving it shows it in the library.
  - Its export matches `examples/pct-2nd-gcd-starry.json` step-for-step.
  - Putting three weaves after Wing Motif warns.
  - Editing an imported rotation and saving it replaces that rotation, keeping the same ID.
- [ ] **Step 4: Commit.** `feat(ui): rotation builder`

---

## Self-review notes

- **Spec coverage:**
  - Trail styles, sizes and advance modes: Tasks 5, 6 and 12.
  - Countdown: Tasks 5 and 10.
  - Potions: Tasks 3, 11 and 12.
  - Import, export, batch and IDs: Tasks 1 and 2.
  - Builder: Tasks 8 and 13.
  - Settings tabs: Task 11.
  - Speed from the character or custom: Tasks 4 and 10.
  - Keybinds and `?`: Tasks 7, 10 and 12.
  - Wrong-press flash: Tasks 5 and 12.
  - Reset on leaving combat and job following: Task 10.
  - Run log for a future summary: Task 5.
  - Example file: Task 10.
- **Manual-only verification:** the game hooks and UI (Tasks 9–13) can't be unit tested without the game. Each of those tasks ends with an explicit in-game checklist instead.
