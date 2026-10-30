# Rotation Trainer — Design

Date: 2026-10-30
Status: Draft for approval

## What it is

Rotation Trainer is a Dalamud plugin for Final Fantasy XIV that helps players drill
openers and rotations into muscle memory, typically against a striking dummy. It
draws a horizontal "trail" of upcoming abilities on screen. When the player uses the
current ability in game, it pops off and the trail slides left to the next one, like
a rhythm game.

## Why

Guides (The Balance, Icy Veins) publish openers as static images or prose. Players
study them, then fumble in game. Rotation Trainer turns the guide into something you
play along with, using your own keybinds and your real speed, until it's automatic.

Success looks like: a player picks their job's opener and loop, starts a
`/countdown` at a dummy, and runs the opener cleanly with the trail guiding each press.

## Goals

- Every job, at level 100.
- Rotations come from users, not from the plugin scraping the web: JSON import
  (single or batch) and a visual drag-and-drop builder, with JSON export.
- A trail that shows icons, keybinds and weaves clearly, in three styles and two sizes.
- Manual advance (default) and auto advance.
- In-game countdown integration for pre-pull steps.
- A clean, modern settings window with a glass look.

## Non-goals

- Leveling or level-synced rotations (level 100 only for now).
- Fetching or scraping rotations from The Balance, Icy Veins or anywhere else. Both
  are reference material only. (Icy Veins actively blocks automated requests; The
  Balance publishes openers as images.)
- Playing the rotation for the user. This is a trainer, not automation; the plugin
  never presses anything.
- Real background blur behind windows. The user confirmed blur doesn't matter.
- Shipping or redistributing Square Enix icon art.

## Anti-goals (would make it a failure even if it "works")

- The trail lagging behind or mis-registering presses. If it can't be trusted to
  pop on the right ability, it's useless for muscle memory.
- A settings window that looks like a typical stack of ImGui checkboxes.
- Keybind labels that are wrong. A `?` for an ability not on any bar is fine; a
  wrong key is not.

## Constraints

- Dalamud plugin, C#, built on Dalamud's own ImGui bindings
  (`Dalamud.Bindings.ImGui`) and `Dalamud.NET.Sdk`, targeting the current Dalamud API.
- No dependency on AethertekUI or Glasswork: the former is a private repo, the latter
  isn't published. FateFrenzy and TTSL are AGPL; use them as visual inspiration only,
  do not copy code.
- Icons come from the game at runtime: each ability's icon ID from the Action sheet
  (Lumina), loaded through Dalamud's texture provider. They are always current for
  whatever patch the player has installed. For anything outside the game (docs,
  mockups), XIVAPI (v2.xivapi.com) serves icons from game data.

## Decided by the user

### Rotations and content

- A rotation is an ordered list of steps. Each step is a GCD or a weave (oGCD).
  Weaves render higher than the GCD line, between GCDs.
- Every rotation is tagged with a job and a kind: **opener** or **loop**. To practice,
  the user picks one opener and one loop for a job. The opener plays once, then the
  loop repeats until the user stops.
- **JSON import**, including batch files containing an array of rotations, each
  assigned to a job.
- **Visual builder**: drag abilities from a job palette onto a timeline. Plus **JSON
  export** from the builder.
- **Potions**: a "Use potions" option. Potions are consumable and used mainly in
  savage once a fight is under control. When the option is off, potion steps are
  hidden from openers. Default: **off**.
- **Countdown**: when the player starts an in-game `/countdown`, the trail starts from
  it, and pre-pull steps (e.g. Rainbow Drip at 4.5 seconds) are lined up against the
  seconds remaining.

### Trail

- All three styles available as a setting (see mockup
  https://claude.ai/artifact/7apZhMyHbN3Mn8nkmkiyzY):
  - **Rail**: even spacing, GCDs on a line, weaves raised between them. Current ability
    under a highlight at the left; trail slides left after each press.
  - **Timeline**: spacing proportional to real time; in Auto it scrolls continuously
    past a fixed marker, rhythm-game style.
  - **Focus**: current ability enlarged, only the next few shown.
- **Big** and **Small** sizes.
- Ability icons, with the keybind or controller button shown above each icon.
- **Advance**: manual (default) or auto.
  - **Manual**: the trail waits for the player to use each ability.
  - **Auto is clock-driven** (user decision): the trail moves on its own at the ideal
    pace, whatever the player does, like a rhythm game. Each step's timing is how long
    it ought to take to use that ability: its cast time and the recast/cooldown before
    the next one can go, computed from the speed in Speed & timing (the character's
    real stats, or custom ones). Weaves are spaced within their GCD window.
  - **Falling behind in Auto** (user decision): when a step reaches the marker and
    the player hasn't used it yet, the trail **pauses and waits**. Once the right
    ability is used, the clock resumes from there. Wrong abilities flash red, as in
    Manual. Using a step early, before it reaches the marker, pops it right away.

### Settings window

Approved as mocked up (https://claude.ai/artifact/VhEiD9ctbgcmzwQB9KxaHe): a glass
window (translucent gradient panel, bright top highlight, hairline border, soft shadow,
large radii, no blur) with a left sidebar of icon tabs and a content panel, in the
spirit of FateFrenzy's layout. Tabs:

- **Practice**: job, opener, "then loop", opener preview, Use potions, start with
  countdown, reset on leaving combat, show trail / restart.
- **Trail**: style, size, advance mode, number of abilities ahead, keybind label mode
  (auto-detect / keyboard / controller / off), flash red on wrong ability, lock position.
- **Speed & timing**: GCD read from the character, or custom stats to practice another
  gear set's speed.
- **Rotation library**: all rotations, filterable by job; new in builder, import JSON
  file, paste from clipboard, export selected, edit, delete; import results with
  per-step errors.
- **Appearance**: accent color, glass opacity, trail backing (glass strip / none).

Speed is **read from the character** by default (user confirmed); manual stat entry
is only for simulating other gear.

### Builder

Approved as mocked up (https://claude.ai/artifact/QJrwJybifs6BQvVQPKh7eU):

- Palette of the current job's abilities grouped GCD / weaves / role / medicine.
  Drag onto the timeline, or click to insert after the selected step.
- Timeline: GCDs on the line, weaves raised automatically. GCDs after the pull are
  numbered. Drag to reorder; a draggable **Pull** marker; drop on a bin to delete;
  arrow keys and Delete work on the selected step.
- Inspector for the selected step: type, countdown time for pre-pull steps, note
  that potion steps depend on Use potions.
- Soft warnings (never block saving): 3+ weaves in one GCD window ("may clip"), an
  opener with no Pull marker.
- Live JSON export panel with copy.

## My calls (not explicitly confirmed by the user — flagged for review)

- **Rail is the default style.** It reads most like the guides people study.
- **Advance on actual use, not keypress.** The trail listens for the local player's
  successfully used actions, so keyboard, controller and macros all behave the same.
- **Wrong ability**: the trail waits for the correct one and flashes the current step
  red. Only actions from the current job's kit (including role actions and
  potions) count as "wrong"; general actions like Sprint and other items are ignored.
- **Reset on leaving combat**, so resetting the dummy restarts the opener.
- **Job follows the player**: switching jobs switches to that job's chosen opener/loop.
- **Lane comes from game data**: whether a step is a GCD or a weave is looked up from
  the game, not stored in JSON.
- **Imports are forgiving**: invalid rotations are skipped with a message naming the
  rotation and step; the rest of the file still imports.
- **Keybind labels**: taken from where the ability sits on the player's hotbars or
  cross hotbars. If an ability isn't on any bar, the label shows **`?`** (user decision). Actions that change
  in place (e.g. Fire in Red → Blizzard in Cyan under Subtractive Palette, Hammer
  Stamp → Brush → Polishing) resolve to the button that currently shows them.
- **Rotation identity**: each rotation has a stable `id` (generated when created or
  first imported, written on export). Re-importing a rotation whose `id` already
  exists asks: replace or keep both. Saving in the builder overwrites the rotation
  being edited. Per-job opener/loop picks reference rotations by `id`, so they
  survive guide updates.
- **No end-of-run summary in v1**: the live trail is the feedback. But the engine
  records each step's expected and actual time and any wrong presses for the run,
  so a summary (clips, early or late pre-pull steps, mistakes) can be added later
  without reworking the core.
- **Early press in Auto pops the step immediately** (see above).
- **No bundled rotations.** The user will supply content; the repo may include one
  example file (PCT 2nd GCD Starry) for testing.

## JSON format (v1)

```json
{
  "format": "rotation-trainer/1",
  "rotations": [
    {
      "id": "b7f3c2e0-4a1d-4c55-9a8e-2f6d1e0c9b11",
      "job": "PCT",
      "level": 100,
      "kind": "opener",
      "name": "2nd GCD Starry Opener",
      "source": "https://www.thebalanceffxiv.com/jobs/casters/pictomancer/openers/",
      "patch": "7.5",
      "steps": [
        { "action": 34688, "name": "Rainbow Drip", "at": -4.5 },
        { "pull": true },
        { "item": 49235, "name": "Gemdraught of Intelligence" },
        { "action": 34670, "name": "Pom Muse" }
      ]
    }
  ]
}
```

- `id`: optional on import (generated if missing), always written on export.
- `job`: three-letter job abbreviation. `kind`: `opener` or `loop`.
- A step is an `action`, an `item` (potions), or the `pull` marker. `name` is for
  readability and as a fallback; on import, a step may give only a `name`, which is
  resolved against the job's actions. Export always writes both ID and name.
- `at`: seconds relative to the pull, only for steps before the `pull` marker.
- `source`, `patch`: optional, shown in the library.
- The IDs above are illustrative; the builder should look up real IDs from game data.
- A file containing a single rotation object (no wrapper) should also import.

## Left to the builder

- Project structure, service layout, and how actions are detected (hooking action
  effects vs. `UseAction`) — as long as it counts only successful uses by the local player.
- How hotbar keybinds and controller button glyphs are read and rendered.
- How countdown state is read from the game.
- The speed/GCD math for custom stats, and how auto-advance spaces weaves.
- The styling layer's exact palette, radii and components, within the glass look
  described above.
- Persistence of config and library (Dalamud plugin config vs. a separate JSON file).
- Testing approach.
