# Developer solo play

Run `Play-DeveloperSolo.cmd` in the project folder. It launches the latest build
with `--dev-solo`. Create a room, mark yourself ready, then start the expedition.
The waiting room displays a developer-mode indicator.

The executable is always `Builds/Latest/EarthRecovery.exe`, with Latest as the
working directory. There is no separate developer-build folder and no fallback
to an archived executable. If Latest is missing, build through `Earth Recovery >
Build Windows Prototype` first. Keep Latest plus at most three previous builds;
see `Builds.md`.

Only Unity Editor and Development builds honor the override. In the Editor,
enable `developerSolo` on the session's runtime rules before creating a room.
Normal launches retain the configured four-player minimum. Release builds ignore
the override, including when it is present in serialized rules.

This changes only the minimum player count. It does not add bots, invulnerability,
unlimited oxygen, free modules, or a solo solution to two-player puzzles.
An empty lobby and unready players still cannot start. Rules are cloned for the
session; the normal GameRules asset is not changed by the launch flag.
