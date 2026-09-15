# Build retention

Keep at most three completed versions: the current build and the two most recent previous builds. Do not create additional release directories per change.

- Stable player path: `Builds/Latest/EarthRecovery.exe`.
- Previous versions: `Builds/Archive/`.
- Temporary build output: `Builds/.staging/` (not a published version).
- Unity command: `EarthRecovery.Editor.ProjectBuilder.Build`, or menu `Earth Recovery/Build Windows Prototype`.
- On successful compilation, `BuildRetention.Publish` archives the current player, promotes staging to the stable path, and prunes all but the two newest previous builds. Recency uses the runtime DLL timestamp, falling back to the executable timestamp, not the folder timestamp.
- Failed compilation does not rotate published builds. Failed promotion restores the old latest path. Linked paths and paths outside the specified Builds root are rejected before recursive removal/moving.
- Do not bypass retention by copying new versions into ad hoc folders. For a validation project, its final output now uses the same stable relative path, not `Builds/Windows`. Transfer a verified build into the main project's staging directory and use `BuildRetention.Publish` there. Remove redundant validation output after confirming deployed hashes.
- `EarthRecovery.Editor.BuildRetention.SelfTest` verifies five successive publications retain only the newest three versions, latest-path stability, and rejection of an incomplete build. It uses an isolated temporary directory.

Initial cleanup removed six obsolete builds and one byte-matching validation copy. Three completed versions remain; the launcher path is unchanged.
