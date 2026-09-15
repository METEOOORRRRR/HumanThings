# Base Camp Terminal

Applied the transparent 1672x941 popup hardware and Batang font from the approved asset pack. GameHud.Terminal.cs owns rendering; the existing camp gating, map data, mission selection, puzzle commands and two-click hibernation remain in use. No simulated map or mission data ships in the normal UI.

Resources are under Assets/Resources/BaseCampTerminal. The importer preserves source alpha, uses uncompressed textures without mipmaps, and does not resize the hardware below its source resolution. Only the display has an opaque backing, so the world remains visible outside the hardware. Font is scoped to the camp panel and matches MainMenu, ExpeditionLobby and WaitingRoom.

The map preserves all 15 positions, unknown labels, discovered names, five zones, live players and module stations. Puzzle inputs stay disabled before the existing linked condition; Maze omits the action button. Empty mission lists and changing selection indices are handled. Hibernation confirmation resets when more than one player is alive.

Verification: Windows development build succeeded. EditMode suite: 94 passed, 0 failed. Development-only --terminal-qa rendered actual game snapshots at 1280x720, 1672x941 and 1920x1080 with 15 sites and 3 missions and no runtime exceptions. This render fixture uses synthetic peers, not an end-to-end multiplayer puzzle test. Production minimum-player rules are unchanged.

Latest executable: Builds/Latest/EarthRecovery.exe. Build retention keeps Latest and two previous versions. Image-generation prompts and the complete design pack remain in the workspace sibling HumanThings_BaseCampTerminal_UnityAssets. Lucide license is included with resources.
