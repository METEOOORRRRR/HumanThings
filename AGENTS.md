# Project Build and Launch Rules

- When asked to run the game, use the latest published executable in developer mode: `Play-DeveloperSolo.cmd` launches `Builds/Latest/EarthRecovery.exe --dev-solo`.
- Do not create a separate developer build, QA build, or per-change build directory. Never launch an Archive executable as the latest developer version.
- Build with `EarthRecovery.Editor.ProjectBuilder.Build`. Its only temporary output is `Builds/.staging`; successful publication promotes it to `Builds/Latest`.
- Keep the latest build plus at most three previous builds in `Builds/Archive`. Do not create duplicate builds just to fill the archive.
- Use the same Latest player for runtime QA flags. Keep logs/captures separate from build versions. Editor-only verification must not generate a separate preview executable.
- Do not claim changes are applied until the actual Latest player contains them. Confirm the running path and developer flag when verifying execution.
- Preserve an active user play session unless authorized to close it. A locked Latest is not a reason to publish into another folder.
- See `Docs/Builds.md` for the build lifecycle and retention self-test.
