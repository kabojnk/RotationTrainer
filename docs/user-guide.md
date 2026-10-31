# Rotation Trainer: User Guide

## What it does

Rotation Trainer is a Dalamud plugin that helps you drill openers and rotations until they feel automatic. It puts a **trail** of upcoming abilities on your screen, with your own keybinds shown above each icon. When you use the current ability in game, it pops off the trail and the next one takes its place, a bit like a rhythm game.

Good to know:

- It is a **trainer**, not automation. The plugin never presses anything for you.
- Rotations come from you. You can import them from a JSON file or build them in the built-in drag-and-drop builder. One example is included (see [Importing and exporting](#importing-and-exporting-rotations)).
- Only **level 100** rotations are supported.
- It works on combat jobs, and practice is meant for a striking dummy.

## Installing

1. In game, type `/xlsettings` and open **Experimental**.
2. Find **Custom Plugin Repositories** and add this address:

   ```
   https://raw.githubusercontent.com/kabojnk/RotationTrainer/main/repo.json
   ```
3. Tick **Enabled** next to it and save.
4. Open `/xlplugins`, search for **Rotation Trainer**, and install it.

Updates arrive through Dalamud like any other plugin. You will see them in `/xlplugins`.

## Quick start

1. Log in on a combat job.
2. Type `/rtrain` to open the settings window.
3. Get a rotation for your job. The library already has openers for 18 jobs (all but Dancer, Scholar and Sage), transcribed from The Balance's guides (see [Credits](#credits)). Otherwise:
   - open the **Rotation library** tab and click **Import JSON...** (or **Paste from clipboard**), or
   - click **New in builder** and build an opener yourself (see [The rotation builder](#the-rotation-builder)).
4. Open the **Practice** tab and pick your **Opener** (and, if you have one, a loop under **Then loop**).
5. Walk up to a striking dummy.
6. Start a countdown with `/countdown 10` (any length works).
7. Follow the trail. Use each ability as it reaches the front. Steps that happen before the pull line up with the countdown, so you can cast them at the right moment.

The trail needs no setup beyond picking a rotation. If you have not picked one yet, it says "Pick an opener and loop with /rtrain".

## Commands

| Command | What it does |
|---|---|
| `/rtrain` | Opens or closes the settings window. |
| `/rtrain trail` | Shows or hides the trail. The choice is remembered. |
| `/rtrain builder` | Opens the rotation builder with a new, empty opener for your current job. You need to be logged in on a combat job, or nothing opens (a note appears in `/xllog`). |
| `/rtrain debug` | Opens a small debug window. Only useful when reporting a problem. |

Anything else shows a warning in `/xllog` listing the valid commands. You can also open the settings window from the plugin's entry in `/xlplugins`.

## The trail

The trail is a strip on screen showing what to press next.

### What you see

- **The title bar** along the top of the strip: "Rotation Trainer" and your job on the left, and four buttons on the right (see [Title bar buttons](#title-bar-buttons)).
- **Icons** for your upcoming abilities, in order. The one at the front is your current step.
- **Two rows.** GCD abilities sit on the lower row and weaves (oGCDs, potions) sit higher and smaller.
- **Keybind chips** above each icon, showing the key or button you have that ability on. On a controller, the chip shows the trigger to hold (LT or RT) and a small glyph: a D-pad with the direction to press lit, or the four face buttons with the one to press lit, like the game's own cross hotbar hints. For an ability that appears on another button when its turn comes (Pom Muse on Living Muse, Aero in Green on Fire in Red, Midare Setsugekka on Iaijutsu), the chip shows that button's key even before the button changes, for the families the plugin knows. Any other one shows its key as soon as your hotbar shows it, and the plugin remembers that button per job from then on. A `?` means the plugin could not find the ability on any hotbar you can press right now.
- **Pre-pull steps.** Steps before the pull, like a hard-cast opener, show a gold countdown tag under the icon, such as `-4.5s`, meaning "cast this when the countdown reaches 4.5". The tile pulses when it is due.
- **The Pull line.** A divider labelled "Pull" marks the moment the fight starts. Everything before it is pre-pull. In Rail and Compact Rail it stays on the track after the last pre-pull step and slides away.
- **The caption** along the bottom: a small tag (`GCD`, `WEAVE` or `POTION`) and "Next:" followed by the ability name. For a pre-pull step before its time, it adds "cast in 2.3s". When you finish an opener with no loop, it says "Opener complete". With **Potion counts** set to **Any potion**, a potion step reads "Next: any potion".
- **The Skip button** at the right end of the caption. It moves past the current step as if you had used it. Use it for an ability that isn't on your hotbar or keybound.
- **The countdown number** at the right edge, left of Skip. During a `/countdown` it shows the seconds left. It then briefly shows "Start" when the countdown reaches zero.

If the opener is followed by a loop, the trail carries on into the loop and repeats it for as long as you like.

### Styles

| Style | Look |
|---|---|
| **Rail** | Icons on an evenly spaced track with a highlight at the front. |
| **Compact Rail** | Like Rail but tighter, with the current step enlarged. It shows at most 5 steps ahead regardless of the "Abilities ahead" setting. |
| **Timeline** | Icons are placed by when they should happen, with time ticks and a marker, so gaps between abilities are visible. |

Change the style on the **Trail** tab.

### Advance modes

| Mode | Behaviour |
|---|---|
| **When I use it** (default) | The trail waits for you. Each step pops when you use it, and nothing moves on its own. |
| **Auto** | The trail moves with the clock at the rotation's real pace, like an editing track, so steps reach the front at the time they should be pressed. If you fall behind, it stops and waits for you, then carries on once you press. A step you press early pops where it is. |

Auto uses your real GCD and the timing settings (see [Speed & timing](#speed--timing-tab)). Hard-cast steps are waited on until the cast should have finished, plus your latency allowance, so casting on time does not pause the trail.

### Press feedback

When you press the right keybind for the current step, its tile dips and flashes with an accent ring, **even if the ability is not ready yet** (for example, you pressed during the GCD or while it was on cooldown). This also works for macros. You get instant confirmation that you pressed the right thing. The step itself only pops when the ability is actually used.

### Wrong presses

If you use a different ability from your rotation's kit, the current step **flashes red** and the trail waits for the right one. You can turn this off with **Flash red on a wrong ability**. Actions outside your job's kit, like Sprint or a mount, are ignored.

### Moving and locking the trail

- With **Lock position** off (the default), the trail has a coloured outline and you can drag it anywhere on screen.
- With **Lock position** on, the outline disappears and the trail ignores the mouse completely, so clicks pass through to the game.
- The trail starts near the lower middle of the screen and remembers where you put it.

### Title bar buttons

The buttons at the top right of the trail work like a window's title bar buttons. They, and Skip, stay clickable even when the trail is locked.

| Button | What it does |
|---|---|
| **Restart** (circular arrow) | Starts the run over from the first step, like **Restart opener** on the Practice tab. |
| **Lock** (padlock) | Toggles **Lock position**. Unlocked, you can drag the trail; locked, clicks pass through to the game. |
| **Settings** (gear) | Opens the settings window, like `/rtrain`. |
| **Hide** (no sign, far right) | Hides the trail. Bring it back with `/rtrain trail` or **Show trail** on the Practice tab. Your progress is kept. |

### When the trail is hidden

The trail only appears when you are logged in and **Show trail** is on. Leaving combat resets your run by default (see **Reset when leaving combat**). Hiding the trail with `/rtrain trail` or the Hide trail button keeps your current progress.

## Settings

Open the settings window with `/rtrain`. A sidebar on the left switches tabs and also shows your **Current job** and your **GCD** underneath. Changes apply and save straight away.

### Practice tab

Choose what to drill on your current job.

| Option | What it does |
|---|---|
| **Job** | Follows your current job. You cannot change it here. |
| **Opener** | Which opener to practise, or "None". |
| **Then loop** | A loop to continue into after the opener, or "None". |
| (preview) | A row of icons showing the start of the chosen opener, with "+N more" if it is longer. Hover for names. |
| **Use potions** | Adds potion weaves to openers that have them. Off by default because potions are consumed. |
| **Potion counts** | Shown when Use potions is on. **Any potion** (the default) counts any main-stat tincture or gemdraught for a potion step, so you can practise with cheap ones instead of burning your best. **Exact potion** counts only the potion the rotation names. |
| **Start with in-game countdown** | Typing `/countdown` lines up pre-pull steps (for example, a cast at 4.5). On by default. |
| **Reset when leaving combat** | Starts your run over when combat ends. On by default. |
| **Hide trail** / **Show trail** | Toggles the trail, same as `/rtrain trail`. |
| **Restart opener** | Starts the current run over from the first step. Greyed out when there is nothing to practise. |

If you have no rotations for your job, the tab says so and offers **Import JSON...** and **Open builder**. If you are not on a combat job, it says "Log in to a combat job to pick rotations." Your opener and loop choice is remembered per job.

### Trail tab

| Option | What it does |
|---|---|
| **Style** | **Rail**, **Compact Rail** or **Timeline**. |
| **Size** | **Big** or **Small**. |
| **Advance** | **When I use it** or **Auto**. |
| **Abilities ahead** | How many upcoming abilities to show, from 3 to 15 (default 8). |
| **Keybind labels** | **Auto-detect**, **Keyboard**, **Controller** or **Off** (see below). |
| **Flash red on a wrong ability** | Flashes the current step red when you use the wrong ability. The trail waits for the right one. |
| **Lock position** | Locks the trail in place and makes it click-through. Unlock to drag it anywhere on screen. |

**Keybind labels:**

- **Auto-detect** follows the last device you used. Pressing a gamepad button or stick switches to controller labels, pressing a keyboard key switches back. It never flips by itself while you idle, such as during a countdown.
- **Keyboard** always shows keyboard hints.
- **Controller** always shows cross hotbar button hints.
- **Off** hides the chips.

### Appearance tab

The glass look for the trail and the plugin's windows. 100% opacity is fully opaque.

| Option | What it does |
|---|---|
| **Theme** | The glass itself: **Midnight** (dark navy), **Graphite** (dark grey) or **Sakura** (light cherry-blossom pink with dark text). Picking Sakura also sets the Sakura accent and raises both opacities to at least 90%, since light glass looks muddy with the game showing through. |
| **Accent** | Highlight colour: **Sky**, **Violet**, **Mint**, **Gold** or **Sakura** (pastel pink). The trail's glass strip is tinted with it too. |
| **Window opacity** | How see-through the settings and builder windows are (20% to 100%). |
| **Trail opacity** | How see-through the trail's glass strip is (20% to 100%). |
| **Trail backing** | **Glass strip** draws a backing behind the trail. **None** shows icons straight over the game. |

### Speed & timing tab

Used by **Auto** advance and the **Timeline** style.

| Option | What it does |
|---|---|
| **Read from your character** | Shows the GCD the game reports for your character, and updates when you change gear. Shows "-" if you are not on a combat job. |
| **Source** | **My character** uses your real speed. **Custom** lets you practise another gear set's speed. |
| **Spell speed** / **Skill speed** | Your custom stats, with the resulting GCD shown next to each. Only editable with the **Custom** source. |
| **Latency allowance** | Extra time per weave, in ms (0 to 500, default 50). Animation lock is treated as 0.6 s plus this. Raise it if your connection is slow and Auto feels too strict. |

Number fields apply when you finish typing.

### Rotation library tab

Every opener and loop you have imported or built, plus the built-in ones. The plugin adds its built-in rotations to your library the first time it loads, and adds any new ones after an update. Built-in rotations are ordinary library entries: you can edit or delete them, and a deleted one is not added back.

| Button | What it does |
|---|---|
| **New in builder** | Opens the builder with a new opener for your current job (disabled when not on a combat job). |
| **Import JSON...** | Pick a `.json` file to import. |
| **Paste from clipboard** | Imports JSON you copied (for example, from a friend). |
| **Export selected** | Saves the selected rotations to a `.json` file. Click rows to select them. |
| **Restore built-ins** | Adds back any built-in rotation you have deleted. Ones you have edited are left as they are. |

The table lists **Job**, **Name**, **Kind** (Opener or Loop) and **Notes**. Notes show a gold `potion` chip if the rotation has a potion, and a chip like `-4.5s` for the earliest pre-pull time. Each row has **Edit**, **Export** and **Delete**. Delete asks "Delete?" with **Yes** and **No** first.

If your library file could not be read, a warning at the top explains what was left out, where a backup was saved, and the errors. If it cannot be saved at all, a banner says changes will not be kept this session and to check `/xllog`.

## The rotation builder

Open the builder with `/rtrain builder`, **New in builder**, or **Edit** on a library row.

**Top row**

- **Job**: which job the rotation is for. Changing it on a non-empty rotation asks first, because abilities differ per job and it clears the timeline.
- **Name**: what the rotation is called in the library.
- **Opener / Loop**: the kind. Openers have a **Pull** marker. Loops do not.
- **Clear** empties the timeline.
- **Save to library** saves it (the button briefly says **Saved**). If something is missing, a "Can't save yet" note says why (a name is needed, at least one step, and pre-pull times must be above 0 seconds).

**Building the timeline**

- The **palette** lists your job's abilities in groups: **GCD**, **Weaves**, **Role** and **Medicine** (potions). Hover for a name.
- **Click** an ability to add it after the selected step (or at the end if nothing is selected), or **drag** it onto the timeline ("Drag abilities here").
- GCDs show as large tiles, weaves as small ones.
- The **Pull** marker is part of an opener. Drag it to move it. Steps **before** the Pull are pre-pull steps.
- Drag steps to reorder them. Drop a step on the bin ("Drop a step here to remove it") to delete it. The Pull cannot be deleted. Switching from Opener to Loop removes the Pull, and switching back adds it again.

**Inspector**

Click a step to see its details: type (GCD, Weave or Potion), position, and for a pre-pull step **Use at countdown**, where you type the seconds left (for example `4.5` shows as `-4.5s` in gold). Leave it empty or enter 0 for no specific time. A potion step shows that it is only shown when **Use potions** is on. Arrow keys move the selected step, **Delete** removes it, and the **Remove** button does too.

The inspector also shows warnings in gold. For example, three or more weaves in a row after a GCD get gold rings and the note "3 weaves after Wing Motif may clip your GCD". An opener with no Pull marker warns that it needs one to line up the countdown. When all is fine it says "No problems found".

**Export JSON**

A live panel shows the rotation's JSON as you edit. **Copy** puts it on your clipboard. Closing the builder with unsaved changes asks "Discard unsaved changes?".

## Importing and exporting rotations

Rotations are shared as JSON files in the format `rotation-trainer/1`.

**To import:** on the **Rotation library** tab, click **Import JSON...** and choose a file, or copy the JSON and click **Paste from clipboard**. A file can hold one rotation or many.

- The result line says, for example, "2 rotations added."
- If a rotation is already in your library, a window asks **Replace** or **Keep both** for each one, then **Import** or **Cancel**. Keep both adds a copy.
- If part of a file is wrong, you see what was wrong (for example `"Name" step 4: message`) and everything valid is still imported.

**To export:** click rows to select them and use **Export selected**, or click **Export** on one row. Pick where to save the file.

**Example file:** the repository includes `examples/pct-2nd-gcd-starry.json`, a Pictomancer "2nd GCD Starry Opener". It is a good way to try the plugin. It starts with Rainbow Drip at 4.5 seconds before the pull, then the Pull marker, then a potion, Pom Muse, Striking Muse, Wing Motif, Starry Muse and onwards. A trimmed look at the format:

```json
{
  "format": "rotation-trainer/1",
  "rotations": [
    {
      "id": "5c0f3d2a-8e41-4b7a-9f26-1d3e7a9b2c40",
      "job": "PCT",
      "level": 100,
      "kind": "opener",
      "name": "2nd GCD Starry Opener",
      "steps": [
        { "action": 34688, "name": "Rainbow Drip", "at": -4.5 },
        { "pull": true },
        { "item": 49237, "name": "Grade 4 Gemdraught of Intelligence" },
        { "action": 34670, "name": "Pom Muse" }
      ]
    }
  ]
}
```

Each step is an `action` id, an `item` id (potions), or `{ "pull": true }`. A negative `at` is the countdown time in seconds. The file must be level 100 and name a real job, and the `kind` must be `opener` or `loop`.

## Troubleshooting and FAQ

**I can't see the trail.**
- You must be logged in to a character.
- Check that the trail is not hidden. Run `/rtrain trail` or press **Show trail** on the Practice tab.
- If it shows "Pick an opener and loop with /rtrain", you have not selected a rotation for your job yet.
- It may be off-screen or tiny against your UI scale. Unlock it and drag it back.

**The trail ignores my mouse.**
That is **Lock position** being on. Turn it off on the Trail tab to move it. The title bar buttons still work either way, and the padlock button unlocks it.

**Nothing happens on the Practice tab.**
Practice needs a combat job. The tab says "Log in to a combat job to pick rotations." when it cannot find one. The same goes for `/rtrain builder` and **New in builder**.

**A keybind chip shows `?`.**
The plugin reads your hotbars. A `?` means the ability is not on any hotbar you can press right now. Things to know:
- Only your own standard hotbars (1 to 10) and cross hotbars are read. Macros count if they show the ability's icon.
- On a cross hotbar, only the set currently on screen counts. Abilities on other sets show `?` until you switch to them.
- Expanded hold bars (WXHB) and pet or vehicle bars are not supported, so abilities only there show `?`.
- Labels refresh about twice a second. After changing hotbars or sets, wait a moment.
- If you use a controller, set **Keybind labels** to **Controller**, or press a button once so **Auto-detect** switches over.

**Keybind labels show the wrong device.**
Pick **Keyboard** or **Controller** instead of **Auto-detect** on the Trail tab.

**The pre-pull step is not lining up with the countdown.**
Make sure **Start with in-game countdown** is on in the Practice tab, and that the opener's pre-pull step has a countdown time. Use the in-game `/countdown`. Cancelling it resets the run. Letting it run out is the pull and does not.

**The potion step is missing.**
Turn on **Use potions** on the Practice tab.

**My potion flashes as wrong.**
**Potion counts** is set to **Exact potion** and you used a different one. Set it to **Any potion**, or use the potion the rotation names.

**An ability isn't on my hotbar.**
Click **Skip** at the right end of the caption to move past it.

**My run reset by itself.**
**Reset when leaving combat** restarts the run when combat ends. Turn it off if you would rather keep your place.

**In Auto the trail stops.**
That is by design. It waits for you when you fall behind, and carries on when you press the right ability. If it pauses even when you are on time, raise the **Latency allowance** or check **Source** on the Speed & timing tab.

**The trail does not accept my ability.**
It must be the exact step. Some buttons that turn into other actions (for example, combo or dance buttons) are matched to their step. If you press something else from your job's kit, it counts as wrong (with the red flash on).

**My imported file failed.**
The import result lists what was wrong. Common causes: an unknown job, a `level` other than 100, a missing name or steps, or an unsupported `format`. Valid rotations in the same file are still imported.

**The Rotation library tab shows a warning banner.**
Your saved library could not be loaded or saved. The banner says what was skipped and where a backup is. See `/xllog` for details.

**Where can I report a problem?**
Open `/rtrain debug` and include what it shows, plus any errors from `/xllog`.

## Credits

Rotation Trainer is made by Big Bear. The **Credits** tab in the settings window shows the same information.

The built-in openers and loops are transcribed from the opener charts in [The Balance](https://www.thebalanceffxiv.com)'s job guides. Each rotation's source names the chart it came from. Thank you to The Balance's guide writers; read their guides for the reasoning behind each opener and how to adapt it to a fight.
