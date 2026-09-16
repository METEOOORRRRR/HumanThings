# Player Character

The character roster contains Neon Vanguard, Toxic Bunny R-31, Neon Outrider,
HAZE, KAI, NOVA, REI and ASH, imported from the eight
`Meshy_AI_*_All_Animations.glb` files in `Assets/Character` with Unity
glTFast 6.20.0. Their original GLBs, meshes, rigs and embedded textures are preserved.
Neon Vanguard has 27 bones; the other seven each have 28 bones.

## Runtime

- `Assets/Resources/PlayerCharacterCatalog.asset` registers stable IDs and visual
  profiles. Each profile references its own HumanThings prefab variant. The
  existing host CharacterController, camera, flashlight and visibility rules remain.
- The host randomly assigns an unused character on each successful room join.
  Characters cannot repeat within a party, including developer stand-ins. Leaving
  releases the character; new arrivals and replacement stand-ins pick only from
  the remaining roster without changing anyone else's assignment. `PlayerState.characterId` is serialized with
  every snapshot, so all peers use the same model. Mission starts and returning
  to the lobby preserve assignments; leaving and rejoining rolls again. Cosmetic
  randomness is separate from seeded city/mission generation. Network protocol 14
  prevents joining older hosts that still allow duplicate assignments.
- Unknown character IDs fall back to Neon Vanguard. The normal build validates
  the roster before publishing, including IDs, profiles and gameplay components.
- The mesh is scaled from its baked standing pose to 1.8 metres, with feet at
  the agent origin. The collision capsule remains 1.8 metres high.
- `PlayerAvatar` derives animation from actual rendered horizontal displacement.
  It smooths speed, ignores teleport corrections, and turns the visual rig toward
  travel. No new network messages or root-motion movement are used.
- The `Gait` blend tree uses Idle (`restpose`), Walk (`Walking`), Run (`Running`)
  and Sprint (`RunFast` on Neon Vanguard, `Running` at 1.2x on the other seven).
  With the default rules, normal movement reaches Walk,
  sprinting reaches Sprint, and intermediate speeds blend through Run.
- The source has no breathing idle clip: Idle holds the supplied resting pose.
  `All_Night_Dance`, `Boxing_Practice` and `You_Groove` remain in the source and
  are not assigned to gameplay controls.
- All characters use the `HumanThings/Weathered Character` shader, with separate
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

For Neon Outrider, run `EarthRecovery.Editor.NeonOutriderCharacterBuilder.Build`.
It imports the original gameplay prefab and clips under `Assets/Character/NeonOutrider`
when missing, then generates its visual variant, mipmapped textures, masks and
profile and registers `neon-outrider`. Use `.Import` to regenerate the gameplay
prefab and clips explicitly. Regeneration preserves generated-asset GUIDs and
updates the existing catalog entry. Waiting-room portraits render the registered
gameplay prefab automatically.

Neon Outrider's clean repaint is stored separately under
`Assets/Character/NeonOutriderClean`. `NeonOutriderCharacterBuilder.ApplyCleanTextures`
connects it only to the gameplay visual material and disables added weathering,
metallic response and the noisy source normal map. `Build` preserves this setup
when the clean atlas exists. The source prefab and original comparison material
continue to use the original embedded textures. See
[Neon Outrider texture refinement](NeonOutriderTexture.md) for the source
preservation report, iteration notes, prompts and deliverables.

For the five concept characters, run `EarthRecovery.Editor.AdditionalCharacterBuilder.Build`.
It imports separate gameplay prefabs, applies the corresponding `*Clean/*_BaseColor_4K.png`
albedos, and registers their stable IDs. Ashen Sentinel corresponds to KAI;
Wasteland Sentinel corresponds to ASH. Their clean materials neutralize region,
weathering, metallic and source-normal effects so the repainted diffuse colors
remain intact. The original comparison material remains available. See
[batch texture refinement](CharacterBatchTexture.md) for deliverables and verification.

The prefab and generated assets are normal Unity assets and should accompany
the GLB and its `.meta` file in version control. No runtime file download is needed.

## Verification

- `PlayerCharacterTests`: source clips/textures, prefab references, clip bindings,
  in-place motion and gait selection.
- `PlayerAvatarTests`: actual bone animation, independent rigs, standing bounds,
  teleport handling and inactive-player state.
- `WorldTests.ThirdPersonShowsLocalBodyAndFollowsBehind`: real world uses the skin
  and preserves its original collision/camera relationship.
- `CharacterRosterTests`: complete roster, all random outcomes, stable mission
  assignments, compressed snapshot round-trip, original rig and sprint fallback.
- `WorldTests.HostAssignmentsReachClientsAndSelectTheSameVisualPrefabs`: real
  transport synchronization, all model selections and stable mission assignment.
- `PlayerAvatarTests.RegisteredCharactersAnimateAtTheSameGameplayScale`: all
  characters' actual bone animation, floor alignment and 1.8-metre height.
- `Earth Recovery > Preview Player Character`: front/back and movement renders
  in the ignored `QA/Character` directory.
- Run a Development build with `--character-qa` for a solo fixture that records
  actual in-game idle/walk/sprint captures for all characters and a result in
  `QA/CharacterRoster/Game`. It
  uses a separate local port, does not save the archive, and quits afterward.
- Use only `Builds/Latest/EarthRecovery.exe` for runtime verification and
  `Play-DeveloperSolo.cmd` for normal developer play. No extra build folders.

Before Neon Outrider was added, verified on 2026-09-16: 437 EditMode tests and 48 PlayMode tests passed. The
published Latest player passed `--dev-solo --character-qa` for both models,
including captured idle/walk/sprint frames. Reports and comparisons are in
`QA/CharacterRoster*`. Original asset hash checks passed (18 baseline files and
the new GLB). Latest runtime DLL SHA-256:
`AFAA55371B95C109E19E42A07713E150A426357F7B9E085F25E4A960254F3815`.

Neon Outrider verification on 2026-09-16: 52 relevant EditMode tests and 11
PlayMode tests passed. The published Latest player passed
`--dev-solo --character-qa` for all three models and all 201 checks in
`--dev-solo --waiting-qa`, including nonblank distinct portraits and six connected
players displaying the three characters. Captures were visually inspected.
All three original GLB SHA-256 hashes remained unchanged. Reports, process paths
and command lines are in `QA/NeonOutrider`; runtime captures are in
`QA/CharacterRoster/Game` and `QA/WaitingPortrait/Runtime`. Publication retained
Latest plus three previous builds. Latest runtime DLL SHA-256:
`4C97D4E6BF0D9C053807BFE70124EFFBF0B68D72CF3233D76D9C628BCE4E61BC`.

Importer reference: [Unity glTFast editor import](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.20/manual/ImportEditor.html).

Five-character expansion verification on 2026-09-16: 14 relevant EditMode tests
and 13 PlayMode tests passed. The roster now contains eight characters. The
published Latest passed `--dev-solo --character-qa` for all eight, including
separate clean 4K albedos for HAZE, KAI, NOVA, REI and ASH. Original GLB hashes,
meshes, UVs, rigs and animation data remain unchanged. Final asset/texture hashes,
process paths, captures and detailed reports are in `QA/CharacterBatch` and
[CharacterBatchTexture.md](CharacterBatchTexture.md). Latest runtime DLL SHA-256:
`BBA9F8194105CDFC3C64CE224AFEF94DAC1A658A8CAC5204D55BEEE090F5CE45`.
The same Latest passed all 225 waiting-room checks, with six connected players
and every one of the eight character portraits verified and captured.
