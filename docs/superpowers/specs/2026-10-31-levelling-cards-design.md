# Levelling Cards: design

Mockup: https://claude.ai/artifact/Ejj1cKki9caNGFNAeErFhU

## Intent

Rotation Trainer drills level-100 openers and loops. Players levelling a job through dungeons and trials want something else: a quick reference for what to press at their current level. They don't need an opener or an optimised raid loop. They need short, practical sequences, such as "this is your trash-pull combo now" and "this is your boss loop now". These should change as abilities unlock.

Success: a player levelling any combat job can open a small card beside the hotbar mid-dungeon. One glance should tell them their AoE and single-target buttons, with their own keybinds.

## Goals

- A **Levelling guide** window. It has a job picker, a level slider and tabs, and shows the vignettes for the band the selected level falls in.
  - The job and level default to the player's current job and level. A "Use my level" button resets them.
- A **Reference card** window. "Pin as card" opens it: a compact, separate window showing the same band in icon form.
  - It follows the current job and level automatically, unless the player has moved the slider off their own level in the guide.
- Every **combat job**: all 21 (PLD WAR DRK GNB, WHM SCH AST SGE, MNK DRG NIN SAM RPR VPR, BRD MCH DNC, BLM SMN RDM PCT).
  - The user said "all 18 jobs", but the mockup's picker wrongly left out DNC, SCH and SGE. "All jobs" is the intent.
  - BLU is excluded.
- **Base classes:**
  - Below level 30, the class (GLA, MRD, CNJ, ACN…) uses its job's cards.
  - When the player is on a class, the job is resolved through `ClassJob.ClassJobParent`, and arcanist branches to both SMN and SCH.
  - The card shows the class name in its title.
- **Content is hand-written per job, per 10-level band** (1–9, 10–19 … 90–99, plus 100).
  - Jobs that start at 30, 50, 60, 70 or 80 begin at their start level.
- **Each band has three vignettes:**
  - **Trash pull:** AoE, with the enemy-count threshold where AoE beats single target, e.g. "3 or more enemies".
  - **Single target:** for bosses and the last enemy left.
  - **Notes line:** a short one, covering off-GCDs to use on cooldown and mitigation for tanks.
  - Healers' cards focus on the damage loop, plus one line on which heal to use when.
- **The game data checks and enriches the content:**
  - **Unlock level:** each ability's comes from `Action.ClassJobLevel`. Abilities above the selected level grey out and show "Lv N".
  - **Icons:** from the Action sheet.
  - **Keybinds:** the existing `KeybindIndex` / `PracticeSession.KeybindLabel()` path, including controller labels.
  - **Unlock strip:** "this band" lists the abilities and traits unlocking in the band, from the Action and Trait sheets.
- **A test** loads every card, resolves every ability name against the job's kit and checks unlock levels.
  - A card may not list an ability above its band's top level, except in a "coming up" slot that is marked as such.

## Non-goals

- Calculating optimal rotations. The game data stores no usable potencies or priorities. Content is authored.
- Gear and stat modelling. At levelling levels, gear changes how fast you press, not what you press.
- Openers, burst windows or raid timelines; the existing trainer covers those.
- Practising the vignettes on the trail. A later version could add "Practise this" on a single-target loop, but it is out of scope here.
- Per-duty guides (specific dungeon or trial mechanics).

## Anti-goals

- Wrong content. A card telling a level-45 player to press something they don't have, or the wrong combo order, is worse than no card. The validation test and a content review are the guard.
- Clutter. The card must stay small enough to sit beside a hotbar; no paragraphs.
- Breaking the existing trainer, its settings or its saved data.

## Content

- **Format:** cards live in `presets/levelling-cards.json`, embedded like `balance-openers.json`. One entry per job per band:

  ```json
  {
    "job": "PLD", "minLevel": 60, "maxLevel": 69,
    "pull":   { "threshold": 3, "steps": ["Total Eclipse", "Prominence"], "note": "Weave Circle of Scorn into the first combo." },
    "single": { "steps": ["Fast Blade", "Riot Blade", "Royal Authority"], "note": "Holy Spirit when you have to step away." },
    "weave":  ["Fight or Flight", "Circle of Scorn", "Spirits Within", "Intervene", "Requiescat"],
    "mitigation": ["Rampart", "Sentinel", "Sheltron", "Hallowed Ground"]
  }
  ```

  - Steps are ability names, resolved against the job's kit like the existing preset importer.
  - A step may be a short sequence, e.g. a pull that opens with a gap closer.
  - `mitigation` is for tanks only and optional for others.
- **Source:** written from scratch for this plugin (user's decision). No external guide is transcribed, so the Credits tab needs no new source entry; the `source` field is omitted.
- **Copy style (user's rule):** brief. No em dashes, no hyperbole. Notes are one short sentence; tips name the button and the situation, nothing else.
- **Review:** each wave's cards are reviewed in game before the wave is merged (a checklist section in `docs/in-game-checklist.md`), on top of the validation test.

## UI

- **Styling:** both windows use the existing glass theme (`GlassTheme`, `GlassWidgets`) and follow the Theme and Accent settings.
- **Guide window, top to bottom:**
  - The job picker, grouped by role.
  - The level number with the band label, plus a slider over the job's full range.
  - Tabs: "Dungeon", "Trial / boss" and "Unlocks".
    - "Trial / boss" shows the single-target vignette first, with the tank mitigation row expanded.
  - The vignette chips: icon, name and keybind, with greyed "Lv N" chips for anything locked.
  - Footer: the "Pin as card" button.
- **Reference card:**
  - The title bar carries the job and level, plus the cog and close buttons.
  - Rows: single-target loop, 3+ enemies loop, off-GCDs, then mitigation for tanks. Each icon carries its keybind in the corner.
  - Below the rows sits one tip line.
  - The card can be locked like the trail, with the same lock and opacity behaviour.
- **Entry points:**
  - A new `/rtrain level` command.
  - A "Levelling guide" button in the settings window's Practice tab.
  - The card's open/closed state is saved in `Configuration`.

## Code layout (builder's call within this shape)

- **Core:**
  - A `LevellingCard` model, plus a loader with validation, in `RotationTrainer.Core/Levelling/`.
  - Card lookup: job + level → card.
- **GameData:**
  - Expose each action's unlock level on `Ability`. A new `UnlockLevel` field, or a catalog method, is the builder's call.
  - Also list trait unlocks per job and level.
- **Plugin:**
  - `Ui/Levelling/LevellingGuideWindow.cs` and `Ui/Levelling/ReferenceCardWindow.cs`.
  - The player's level comes from the local player object's level.
- **Tests:**
  - Core loader and lookup tests.
  - A game-data test that validates every card against the kits.

## Calls made during design review (designer's calls, not the user's)

- **Keybinds:**
  - Shown only for actions the player has on a hotbar right now, looked up by the existing `KeybindIndex` by action id.
  - Locked abilities show "Lv N" instead of a keybind.
  - An unlocked action with no hotbar slot shows no keybind; no "?".
  - No changes to `FindHosts` or the host table for lower-level actions.
- **Catalog:** a probe showed the existing kits already hold every action a job or its class learns from level 1 (Rage of Halone resolves for PLD, Ruin for SMN and SCH). No separate lookup is needed; `Ability` gains its unlock level and the trainer is otherwise untouched.
- **Arcanist:** below 30, the picker shows SMN selected, and SCH is one click away.
  - Class-only actions resolve through the job's levelling lookup; Ruin and Bio are SMN/SCH actions in the sheet.
- **Level source:** the **synced** level, i.e. the local player's current level. In a level-synced dungeon that is what the player can press.
  - It is read each frame; that is cheap.
- **Jobs with no base class** (DRK GNB AST SGE SAM RPR VPR MCH DNC PCT): the slider starts at the job's start level, so there is no empty range.
- **Config:** the card's open state, lock and opacity follow the trail's settings pattern. Position is ImGui's own.

## Left to the builder

- Exact sizes, spacing and chip drawing, matching the trail's chip style.
- Whether the guide and the card share one draw helper for chips.
- The slider's behaviour at band edges and the empty state for a job/level with no card.

## Rollout (user's decision: waves)

1. Both windows, the loader, the game-data checks and the four tanks (PLD WAR DRK GNB).
2. Healers (WHM SCH AST SGE).
3. Melee (MNK DRG NIN SAM RPR VPR).
4. Ranged and casters (BRD MCH DNC BLM SMN RDM PCT).

Each wave is its own PR. Jobs without cards yet show an empty state in the guide ("Cards for this job are coming") and are left out of the picker's default.
