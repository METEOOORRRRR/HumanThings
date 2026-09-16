# Player Character

The character roster contains Neon Vanguard and Toxic Bunny R-31, imported from
the two `Meshy_AI_*_All_Animations.glb` files in `Assets/Character` with Unity
glTFast 6.20.0. Their original GLBs, meshes, rigs and embedded textures are preserved.
Neon Vanguard has 27 bones; Toxic Bunny has 28 bones.

## Runtime

- `Assets/Resources/PlayerCharacterCatalog.asset` registers stable IDs and visual
  profiles. Each profile references its own HumanThings prefab variant. The
  existing host CharacterController, camera, flashlight and visibility rules remain.
- The host rolls independently and uniformly on each successful room join.
  Duplicate characters are allowed. `PlayerState.characterId` is serialized with
  every snapshot, so all peers use the same model. Mission starts and returning
  to the lobby preserve assignments; leaving and rejoining rolls again. Cosmetic
  randomness is separate from seeded city/mission generation. Network protocol 10
  prevents older players from silently displaying the wrong appearance.
- Unknown character IDs fall back to Neon Vanguard. The normal build validates
  the roster before publishing, including IDs, profiles and gameplay components.
- The mesh is scaled from its baked standing pose to 1.8 metres, with feet at
  the agent origin. The collision capsule remains 1.8 metres high.
- `PlayerAvatar` derives animation from actual rendered horizontal displacement.
  It smooths speed, ignores teleport corrections, and turns the visual rig toward
  travel. No new network messages or root-motion movement are used.
- The `Gait` blend tree uses Idle (`restpose`), Walk (`Walking`), Run (`Running`)
  and Sprint (`RunFast` on Neon Vanguard, `Running` at 1.2x on Toxic Bunny).
  With the default rules, normal movement reaches Walk,
  sprinting reaches Sprint, and intermediate speeds blend through Run.
- The source has no breathing idle clip: Idle holds the supplied resting pose.
  `All_Night_Dance`, `Boxing_Practice` and `You_Groove` remain in the source and
  are not assigned to gameplay controls.
- Both characters use the `HumanThings/Weathered Character` shader, with separate
  skin/hair/cloth/equipment masks, weather masks and mipmapped texture copies.
  Toxic Bunny retains its hood, yellow trim and markings; exposed face, waist and
  wrists have a model-specific mask. Its profile starts from the existing game's
  saturation, matte finish, dirt, dust and environmental lighting values.

## Regeneration

Use `Earth Recovery > Rebuild Player Character`, or execute
`EarthRecovery.Editor.PlayerCharacterBuilder.Build` in Unity batch mode.
This rebuilds the prefab, controller and four derived clips in
`Assets/Character/Generated`. Existing generated-asset GUIDs are preserved.
The importer uses Mecanim clips. Derived clips loop, start at time zero, and
remove horizontal hip translation while preserving vertical gait motion.

For Toxic Bunny, run `EarthRecovery.Editor.ToxicBunnyCharacterBuilder.Import` to
rebuild its original gameplay prefab and derived clips under
`Assets/Character/ToxicBunny`. Then run
`EarthRecovery.Editor.ToxicBunnyCharacterBuilder.Build` to generate its visual
variant, texture copies, masks, profile and roster entry. This does not rebuild
Neon Vanguard or modify the original GLBs. Additional processed characters can be
registered in the catalog without changing the assignment/network code.

The prefab and generated assets are normal Unity assets and should accompany
the GLB and its `.meta` file in version control. No runtime file download is needed.

## Verification

- `PlayerCharacterTests`: source clips/textures, prefab references, clip bindings,
  in-place motion and gait selection.
- `PlayerAvatarTests`: actual bone animation, independent rigs, standing bounds,
  teleport handling and inactive-player state.
- `WorldTests.ThirdPersonShowsLocalBodyAndFollowsBehind`: real world uses the skin
  and preserves its original collision/camera relationship.
- `CharacterRosterTests`: complete roster, both random outcomes, stable mission
  assignments, compressed snapshot round-trip, original rig and sprint fallback.
- `WorldTests.HostAssignmentsReachClientsAndSelectTheSameVisualPrefabs`: real
  transport synchronization, both model selections and stable mission assignment.
- `PlayerAvatarTests.RegisteredCharactersAnimateAtTheSameGameplayScale`: both
  characters' actual bone animation, floor alignment and 1.8-metre height.
- `Earth Recovery > Preview Player Character`: front/back and movement renders
  in the ignored `QA/Character` directory.
- Run a Development build with `--character-qa` for a solo fixture that records
  actual in-game idle/walk/sprint captures for both characters and a result in
  `QA/CharacterRoster/Game`. It
  uses a separate local port, does not save the archive, and quits afterward.
- Use only `Builds/Latest/EarthRecovery.exe` for runtime verification and
  `Play-DeveloperSolo.cmd` for normal developer play. No extra build folders.

Verified on 2026-09-16: 437 EditMode tests and 48 PlayMode tests passed. The
published Latest player passed `--dev-solo --character-qa` for both models,
including captured idle/walk/sprint frames. Reports and comparisons are in
`QA/CharacterRoster*`. Original asset hash checks passed (18 baseline files and
the new GLB). Latest runtime DLL SHA-256:
`AFAA55371B95C109E19E42A07713E150A426357F7B9E085F25E4A960254F3815`.

Importer reference: [Unity glTFast editor import](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.20/manual/ImportEditor.html).
