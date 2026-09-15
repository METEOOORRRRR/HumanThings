# Display Ratio Settings

## User Behavior

- Title settings and waiting-room settings contain a scrolling aspect-ratio dropdown. During an expedition, open the pause menu and choose the screen-ratio entry.
- Choices: automatic monitor/window ratio, 4:3, 5:4, 3:2, 16:10, 16:9, 18:9 (2:1), 19.5:9, 20:9, 21:9, 24:10, 32:10, 32:9.
- Selection is local, saved as PlayerPrefs `Display.Aspect`, and shared by all settings surfaces. It is not a host-controlled room setting.
- This changes the game's display viewport, not the operating system's resolution. On a differently shaped monitor/window, unused space is black. Automatic mode uses the full available window. The existing fullscreen toggle remains independent.
- Authored menu artwork and UI keep their original proportions in a centered safe area. They are not stretched to fill ultrawide or narrow screens. The world camera uses the selected aspect. The portrait render texture is unchanged.
- Very short windows or a 32:9 viewport squeezed onto a 16:9 monitor necessarily make fixed-layout text smaller. Select Automatic or a matching display ratio for the largest readable presentation. This is not a claim that every arbitrary physical resolution is equally readable.

## Implementation

`DisplayPreferences` owns ratio selection, bounded viewport calculation, persistent preference, camera viewport, bars and the reusable UGUI dropdown. MainMenu, ExpeditionLobby and WaitingRoom fit their reference layouts into this viewport. GameHud uses the same viewport for both its 1280x720 coordinate system and the 1672x941 terminal.

Dropdowns include a scrollable masked list, scrollbar, standard Unity selection/navigation and synchronization between surfaces. OS-created Batang is owned and released when the TMP source font is unavailable to legacy UGUI. No downloaded font is required. The in-game ratio popup blocks world input and closes on Escape or its close button.

## Verification Scope

- EditMode: 278 tests passed, including 182 combinations of 13 selections and 14 physical pixel sizes. Each combination verifies centered viewport bounds, exact requested aspect and uniform containment of both UI coordinate systems.
- PlayMode regression suite: 34 passed, 0 failed. Final runtime QA: 99 nonblank captures, 49 viewport/dropdown checks, exit code 0, no runtime exceptions. The test run restored Automatic mode.
- Mathematical sizes: 800x600, 1280x720, 1280x1024, 1600x900, 1920x1080, 1920x1200, 2560x1440, 2560x1080, 3440x1440, 3840x2160, 3840x1600, 5120x1440, 5120x1600 and 7680x2160.
- Runtime automation `--display-qa`: title, six-player waiting room and camp terminal at every selection; native-size windows at 1280x1024, 1200x900, 1440x900, 1600x900, 1600x750, 1600x500 and 1600x450; room creation/list/code screens; dropdown first/last items and last-item selection; HUD, inventory, pause, ratio popup, facility, crafting, three puzzles and module station at 5:4, 16:9 and 32:9; result and archive screens.
- Runtime peers are render fixtures, not a replacement for a network gameplay test. Actual resulting window dimensions are encoded in native capture filenames. Screenshots and runtime result are under `Builds/Latest/DisplayQA`.
- 4K/5K/8K containment is mathematically tested; those resolutions were not visually tested on physical 4K/5K/8K displays. Different DPI, GPU drivers, monitor switching and all possible player-generated strings are not exhaustively validated.

## Regressions Caught During Review

- TMP OS font did not expose a legacy source Font for the waiting dropdown; added an owned Batang fallback.
- Scroll thumb inherited a fixed height and escaped its track; reset its anchors and offsets before binding the Scrollbar.
- Facility screenshot fixtures originally failed to move the actual character controller; now warp the fixture body and assert the requested facility/station panel remains open.
- Captures now use synchronous pixel readback and reject blank images; facility fixtures send the actual view command, and synthetic Maze data includes its required 36 cells. These are QA corrections, not changes to production puzzle rules.

Builds continue to use `Builds/Latest/EarthRecovery.exe` and retain only Latest plus two previous builds.
