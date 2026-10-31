# Rotation Trainer: in-game checklist

None of these have been run yet. The plugin builds and every automated test passes; these checks cover what only the game can show. They are grouped by the build task that introduced them.

## Setup and plugin shell (Task 9)
1. Build on Windows (`dotnet build src/RotationTrainer -c Debug`) or use the WSL build output.
2. /xlsettings → Experimental → Dev Plugin Locations: add `C:\Projects\RotationTrainer\src\RotationTrainer\bin\x64\Debug\RotationTrainer.dll`.
3. Enable "Rotation Trainer" in the dev plugins list; check /xllog shows no errors on load. Catalog build reads the Action and Item sheets at load.
4. `/rtrain` → a "Rotation Trainer" window opens; run it again → it closes. The plugin installer's settings/main buttons also toggle it.
5. `/rtrain trail` toggles the trail, `/rtrain builder` opens a new opener for your job, and `/rtrain debug` opens the debug window.
6. Unload/reload the plugin: no errors; %AppData%\XIVLauncher\pluginConfigs\RotationTrainer\ exists (library.json appears only after the library changes).


## Action detection, countdown, keybinds (Task 10)
Load the dev plugin (`src/RotationTrainer/bin/x64/Debug/RotationTrainer.dll`), import `examples/pct-2nd-gcd-starry.json` from the Rotation library tab and pick it as the PCT opener on the Practice tab. Turn on Use potions if you want the potion step. On PCT at a striking dummy, `/rtrain debug`:
1. Using Rainbow Drip advances the index by one.
2. Sprint changes nothing.
3. A party member's actions change nothing.
4. `/countdown 10` shows seconds counting down, and the clock reads -10 when it starts.
5. Cancelling the countdown resets the index to 0 (and letting a countdown run out does not).
6. The keybind shows your real key, and `?` for an ability removed from all bars (check the "Ctrl-3" stripping looks right; check a cross hotbar with a controller).
7. The GCD matches the game's character sheet.
8. No errors in /xllog on load, on job change, and on unload.

- Additional in-game check: on a controller, let a `/countdown` run with no input. Labels must stay on cross-hotbar buttons. Press a keyboard key and they switch to keyboard hints.

## Settings window (Task 11)
1. `/rtrain` shows the glass window with the sidebar and title row; the close button and dragging work.
2. Every control changes `Configuration`, and the change survives a plugin reload. Sliders and number inputs save only when you release or finish typing.
3. Importing `examples/` JSON adds it, and the result line shows "N rotations added."
4. Importing it again opens the Replace / Keep both modal. Replace swaps the rotation; Keep both adds "(copy)".
5. A file with a bad step shows `"Name" step N: message` and still imports the rest.
6. At Dalamud's global font scale 1.5 the window stays legible, and cards resize without clipping after one frame.
7. Opener preview icons render. Edit and New in builder open the builder. Delete asks Yes / No.
8. With a locked or corrupt library.json, the warning banner appears on the Rotation library tab.

## Trail overlay (Task 12)
For each of Rail, Compact Rail and Timeline, in both Big and Small, compare against the mockup:
1. The real icons and keybinds show; "?" appears when an ability is not on a bar; Off hides the chips.
2. Weaves sit higher than GCDs.
3. Steps pop on use (scale and fade), and the trail slides left about 280 ms after each press.
4. A wrong ability flashes the current step red (only with Flash wrong on).
5. `/countdown 5` shows 5…1, then "Start"; Rainbow Drip pulses gold at 4.5, and the caption shows "cast in …s" beforehand.
6. In Auto, the trail pauses when you stop pressing and resumes when you press; Timeline in Auto scrolls with the clock.
7. Leaving combat resets the trail, with no slide from the old position.
8. With potions off, the Gemdraught step is absent; with them on, it is present and captioned POTION.
9. Also check: LockTrail on makes the window click-through and immovable; with it off, the window is outlined and draggable, and dragging does not take the game's keyboard input. `/rtrain trail` toggles the window and the setting persists. The glass strip toggle, Accent, Window opacity and Trail opacity all apply. A job with nothing selected shows "Pick an opener and loop with /rtrain". An opener with no loop ends with "Opener complete".

10. On Timeline, Auto, Big, AbilitiesAhead 8+, `/countdown 15`: the window keeps a sensible width (about the Manual width), steps further out are clipped at the edge, and the countdown number stays at the window's right edge. Afterwards the width shrinks back as content narrows, e.g. "Opener complete" or switching to Small.
11. In Rail and Compact Rail, the Pull divider stays on the track after the last pre-pull step pops and slides off to the left.
12. Hide the trail (`/rtrain trail`), make wrong presses or use abilities, then show it again: there is no red flash and no slide from old positions.

## Rotation builder (Task 13)

1. `/rtrain builder` on PCT opens a new opener with only the Pull. On a non-combat job it logs "Log in to a combat job first."
2. Build the 2nd GCD Starry opener from scratch:
   - Drag Rainbow Drip before Pull and set "Use at countdown" to 4.5. The tile shows "-4.5s" in gold.
   - Click the potion and then the abilities in order. Each click appends after the selection.
3. Save to library. The button shows "Saved", and the rotation appears in the Library tab.
4. Copy the JSON and compare it with `examples/pct-2nd-gcd-starry.json`. The steps match step-for-step; the id and source differ, as expected.
5. Place three weaves after Wing Motif. Those three get gold rings, and the inspector lists "3 weaves after Wing Motif may clip your GCD".
6. Import the example, open it with Edit, change something and save. It replaces the same library entry (same ID, no duplicate). A second save of a new document also overwrites rather than duplicating.
7. Drag and drop:
   - Dragging a step or the Pull between items shows the accent bar and moves the item.
   - Dropping a step on the bin removes it; dropping the Pull on the bin does nothing.
   - Arrow keys and Delete act only while the builder is focused.
8. Switching Opener → Loop removes the Pull, and switching back re-adds it once.
9. Changing the job on a non-empty document asks first.
10. Closing with unsaved changes asks "Discard unsaved changes?".
11. Check at a UI scale other than 100%.


## After the final review fixes
1. **Auto, hard-cast opener (PCT Rainbow Drip at -4.5):** cast on time. The trail must not pause while you cast, and when the cast lands the next steps line up with real time (no ~4 s lag). Then cast late, and confirm it pauses only after cast time + latency, and resumes from there.
2. **Auto, Swiftcasted hard cast:** it pops as soon as it is used, with no pause.
3. **MCH/GNB/DNC/RPR/SGE/VPR:** Drill, Air Anchor, Gnashing Fang, Phlegma, Vicewinder and similar are scheduled one GCD after the previous GCD, not 20-60 s later. The Debug window's GCD readout is sane.
4. **DNC:** Standard Step, the steps and Standard Finish follow each other at about 1.5 / 1.0 / 2.5 s. Pressing Standard Finish (which fires Single/Double Standard Finish) pops the "Standard Finish" step. The same goes for Technical Finish (Quadruple...).
5. **NIN:** a second or third mudra (Ten/Chi/Jin variants 18805-18807) pops the matching mudra step instead of flashing wrong. Ninjutsu (Fuma Shuriken, Raiton...) pops its step.
6. **Controller labels:** switch cross hotbar sets (set 1 to 2). Labels must change to the new set's buttons. Actions only on the inactive set show `?`. Actions only on WXHB show `?` (expected for now). Opening a pet/vehicle bar shows `?`. Report it if any label ever names a button that fires something else.
7. **Library banner:** put a broken rotation in library.json (for example, job "XXX") and reload the plugin. The Library tab shows the count, the backup path and the error, and the backup file exists.
8. **Builder:** clear the name, or remove all steps. Save is disabled and the reason is shown. Typing 0 as the countdown time clears it.
9. **Show/Hide trail button** in the Practice tab: toggling mid-run keeps the current step.
10. **Debug window:** first open at 100% and 150% UI scale gives a sensible size (not double-scaled).

## Opacity, styles, Auto scrolling and press feedback
1. **Opacity:** Appearance has separate Window opacity and Trail opacity sliders. At 100% the settings and builder windows, and the trail's glass strip, are fully opaque; nothing behind them shows through. At 20% both are faint. Changing one does not change the other.
2. **Style names:** the Trail tab offers Rail, Compact Rail and Timeline, in that order. A config saved with the old Focus style opens as Compact Rail.
3. **Auto, Rail and Compact Rail:** once the clock starts (countdown or first press), the whole track moves left continuously at the rotation's pace, like an editing timeline. Each step reaches the highlight at its expected time. A popped step keeps moving left with the track and fades out instead of growing. When you fall behind, the track stops and waits, then continues once you press.
4. **Auto, early press:** pressing a step before it reaches the highlight pops it where it is (it fades off as the track carries it left); the track does not jump forward.
5. **Auto, Timeline:** popped steps also fade off to the left instead of growing.
6. **Press feedback (both advance modes):** pressing the current step's button while it is still on cooldown or the GCD is rolling makes its tile dip and flash with an accent ring, even though nothing pops. Pressing it from a macro does the same. A queued action firing on its own does not flash a second time. Pressing a different ability does not show the press flash.

## Gear button and logo
1. **Gear:** a small dark gear chip sits on the top edge of the trail's strip, near its right corner, in every style and size, and also on the empty "Pick an opener" trail. Hovering it shows "Settings (/rtrain)" and lightens it; clicking it opens the settings window, and clicking again closes it.
2. **Gear with the trail locked:** with Lock position on, the gear still takes the click, while the rest of the trail lets clicks through to the game. With the trail unlocked, dragging the trail moves the gear with it.
3. **Gear placement:** the gear stays above the keybind chips, including a weave's chip at the right end of the trail, in Big and Small and at your UI scale. If it overlaps anything, note the style and size.
4. **Logo:** the settings window title shows the logo before "Rotation Trainer". The dev plugin's entry in `/xlplugins` shows the logo as its icon.

## Known limitations to watch for
- After switching cross hotbar sets, controller labels can name the previous set's button for up to 0.5 s.
- GCDs whose GCD lock comes only from a secondary cooldown group are scheduled at a flat 2.5 s (Viper's Vicewinder may really be 3.0 s).
- Dance steps (1.0 s), Standard/Technical Step (1.5 s) and NIN mudras (0.5 s) use approximate GCD locks in Auto mode.
- Controller labels ignore the expanded hold (WXHB) bars; abilities only there show ?.
