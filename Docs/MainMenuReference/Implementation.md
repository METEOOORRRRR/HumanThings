# Title screen integration

## Source and rendering

- Original source: `C:\Users\yubin\Downloads\HumanThings_MainMenu_UnityAssets`.
- Runtime assets: `Assets/Resources/MainMenu` (original PNGs, not repainted).
- Composition: `Assets/Scripts/MainMenu.cs`, runtime uGUI Canvas with independent image, text and button objects.
- Reference: `MainMenu_Reference.png`, 1672 x 941. The JSON's alternative scaler suggestion (1664 x 936) does not match the actual raster size; the actual raster is the coordinate basis.
- CanvasScaler uses Scale With Screen Size, match 0.5. An inner aspect-fit canvas preserves coordinates and adds black bars when needed, instead of stretching or cropping.
- Korean text is live uGUI Text, using the OS Batang font with Korean/Arial fallbacks. This does not redistribute Windows font files. The JSON recommends TMP but does not contain a TMP font asset; this implementation uses the project's existing OS-font approach.

## Coordinates

All coordinates below are top-left reference pixels, before proportional viewport scaling.

| Layer | X | Y | Width | Height |
| --- | ---: | ---: | ---: | ---: |
| Background / screen-edge FX | 0 | 0 | 1672 | 941 |
| Logo 2 | 95 | 48 | 755 | 252.37 |
| Subtitle | 300 | 306 | 390 | 34 |
| Start | 204 | 405 | 473 | 66 |
| Archive | 204 | 476 | 473 | 66 |
| Settings | 204 | 547 | 473 | 66 |
| Quit | 204 | 618 | 473 | 66 |
| Arrow, relative to row | 20 | 17 | 32 | 32 |
| Polaroid, rotated 7 degrees | -15 | 611 | 205 | 255 |
| Archive poem | 73 | 39 | 200 | 100 |
| Waiting poem | 73 | 460 | 115 | 95 |
| Upper-right text | 1467 | 30 | 192 | 48 |
| Date | 1555 | 90 | 82 | 23 |
| Lower-right quote | 1467 | 846 | 195 | 46 |
| Version | 35 | 900 | 150 | 25 |

Menu and polaroid bounds follow the visible reference rather than the JSON's approximate MenuRoot/Polaroid bounds. The user subsequently selected Logo 2: its full 2169:725 aspect ratio is retained at 755px width above the subtitle. The arrow sits to the left of each label with a 6px gap between their rectangles.

## Asset coverage

Background, Logo 2, default/selected nine-slice backgrounds (18px borders), selected left accent, MenuArrow, separators, polaroid frame/photo and screen-edge FX are separate rendered layers. Decorative copy and button labels remain editable text. `Logo_HumanThings2.png` replaces the original logo; the two are not stacked. Flattened reference and preview sheets are not runtime UI backgrounds.

## Interaction

- Start opens the existing offline multiplayer lobby. It does not create or join a room automatically.
- Offline lobby has a return-to-title button; unavailable while connecting or connected.
- Continue is removed because the game has no resume feature. Remaining rows retain 71px top-to-top spacing.
- Archive opens the existing archive and returns to the title when closed.
- Settings opens master volume (persisted) and fullscreen controls. Close/Escape returns to the title.
- Quit exits the player.
- Pointer enter shows only that row's selected background, accent and arrow, with 42px bold text instead of 36px regular. Row and label rectangles remain fixed. Pointer exit and application focus loss restore the regular text and clear effects.
- Keyboard selection uses the same effects. Initial Start selection matches the reference.
- Existing network smoke runs using `--qa-role` bypass the title; ordinary players do not.

## Source limitations

The supplied clean background contains prominent inpainting smears where the old title/menu were removed. The original logo extraction also contains cropped/distressed edges and background fragments. The supplied polaroid frame and button textures are simpler than those baked into the reference. These are source-image differences, not coordinate errors. No flattened screenshot is used to conceal them, and no artwork has been regenerated without approval. Pixel-identical reproduction would require corrected source artwork.

## Reproducible verification

- Existing EditMode suite: `QA/Title-EditMode.xml`.
- Existing PlayMode suite: `QA/Title-PlayMode.xml`.
- Final results: 52 EditMode and 30 PlayMode tests passed; all title runtime assertions passed. Screenshots and report are retained in `QA/Title-final`.
- Development player: launch with `--title-qa`; outputs `TitleQA/verification.txt` and screenshots beside the executable, then exits with failure status if assertions fail.
- Checks include reference coordinates, logo/subtitle non-overlap, real Input System mouse hover/clicks, exclusive highlight/arrow state, absence of Continue, selected typography and text fit, pointer exit restoration, aspect-fit bounds at 1280x720 / 1920x1080 / 1024x768, Settings/Archive navigation and offline lobby routing.
- Screenshots are rendered UI, not camera-only captures. The development-build watermark is Unity's build overlay, not part of the reference composition.
- The smoke test uses a temporary Input System mouse and temporarily disables the game's physical mouse device to prevent physical input from overwriting injected coordinates. Devices are restored when the test component is destroyed; ordinary play does not run this code.
- Published executable: `Builds/Latest/EarthRecovery.exe`. Previous contents were copied to `Builds/Archive/Before-Title-20260914-202122`. Deployed runtime DLL and resources hashes match the tested build.
