# Build retention

Keep at most four completed versions: the latest build and the three most recent previous builds. Do not create additional developer, QA, or per-change build directories.

- Stable player path: `Builds/Latest/EarthRecovery.exe`.
- Developer launch: `Play-DeveloperSolo.cmd` always starts that exact player with `--dev-solo`, using Latest as its working directory. It reports a missing Latest instead of falling back to an archive or QA executable.
- With `--dev-solo`, hosting fills a six-person party with five idle test characters. They use the normal random character/portrait assignment without duplicates within the party and are automatically ready, including after returning to the lobby. The local operator still presses Ready and Start. Real LAN participants replace stand-ins; leaving a lobby restores the vacant stand-in. Stand-ins have no network/voice connection and do not block solo death or hibernation. Normal launches do not add them.
- Developer party runtime verification uses this same Latest player with `--dev-solo --developer-solo-qa`; its report and screenshots go to `QA/DeveloperSolo/Runtime`. The existing `--waiting-qa` fixture explicitly disables stand-ins to continue testing one through six real connections.
- Previous versions: `Builds/Archive/`.
- Temporary build output: `Builds/.staging/` (not a published version).
- Unity command: `EarthRecovery.Editor.ProjectBuilder.Build`, or menu `Earth Recovery/Build Windows Prototype`.
- On successful compilation, `BuildRetention.Publish` archives the current player, promotes staging to the stable path, and prunes all but the three newest previous builds. Recency uses the runtime DLL timestamp, falling back to the executable timestamp, not the folder timestamp. Fewer than three existing previous builds are fine; never create duplicates to fill the limit.
- Failed compilation does not rotate published builds. Failed promotion restores the old latest path. Linked paths and paths outside the specified Builds root are rejected before recursive removal/moving.
- Do not bypass retention by copying new versions into ad hoc folders. `CameraDisplayVerification.Build` delegates to the normal builder. Procedural city preview verification runs in the Editor without producing a separate executable; runtime verification uses the normal Latest player with `--city-smoke`. QA logs and captures are not extra game builds.
- `EarthRecovery.Editor.BuildRetention.SelfTest` verifies five successive publications retain only the newest four versions, latest-path stability, and rejection of an incomplete build. It uses an isolated temporary directory that is removed afterward.

Only call a fix deployed after publishing it to Latest. Do not run an archived or separately built executable when asked to run developer mode. A failed or blocked build must not be described as applied to the running game.
