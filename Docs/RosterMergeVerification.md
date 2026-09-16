# Roster merge and runtime reference verification

Verified against main at 3a8346b with the local comparison separation changes.
No unresolved Git index entries or conflict markers remain. The scene merge
keeps the existing Bootstrap object IDs; roster startup branches remain intact.

## Findings and corrections

1. Six incoming character prefabs still serialized direct original-material
   references. All eight now use editor-only GUID/local-ID resolution for
   comparison, preserving their source IDs and actual gameplay materials.
2. The legacy original PlayerCharacter prefab still lived under Resources,
   implicitly packaging original textures even without a runtime lookup.
   AssetDatabase.MoveAsset moved it to Assets/Character/Generated with its GUID
   preserved. Runtime fallback and PlayMode tests now use the catalog's default
   gameplay prefab. Source GLBs remain necessary for mesh data and were retained.
   The build report's Neon Vanguard GLB contribution fell from 97.6 MB to 1.6 MB.
   This is packaged contribution, not a claim about measured VRAM savings.
3. Character/performance QA inherited the new five developer stand-ins. These
   single-avatar fixtures now explicitly disable filling developer slots. Normal
   developer mode retains the party and has its own six-slot verification.

ProjectBuilder now validates every catalog gameplay material and rejects legacy
Resources originals, direct comparison material fields, and embedded GLB
textures assigned to gameplay materials. CharacterVisualTests covers the guard.

## Reviewed references retained intentionally

- Visual/ruin treatment original-material dictionaries support restoration;
  material copies share existing texture references rather than copy textures.
- Original GLB mesh references and editor comparison remain available.
- Metallic/roughness textures still drive roughness even with zero metallic.
- Waiting portraits cache character renders and disable their cameras/lights
  after rendering; runtime verification exercises changes and mission entry.
- No character source geometry, texture pixels, shaders, material values, map
  layout or generator algorithms were changed by this review.

## Verification

- Latest build: ProjectBuilder.Build succeeded with zero errors; Latest plus
  three archives retained. Executable: Builds/Latest/EarthRecovery.exe.
- Final EditMode: 459/459 passed. Full PlayMode: 55/55 passed.
- Player: all eight character models, cleaned textures and idle/walk/sprint
  transitions passed; original embedded comparison textures not loaded.
- Waiting room: 231 checks passed, including roster, portraits and disconnect.
- Developer party: creation, unique stand-ins, readiness, mission entry,
  death, return, second mission, hibernation and rehost passed.
- Two separate player processes: actual LAN transport, collection, module
  crafting/install, skill checks, cooperative puzzles, three deliveries and
  three archive entries passed on both host and client. This fixture disables
  monsters to isolate the mission loop; it is not a live combat stress test.
- Six-slot waiting and in-game character captures inspected visually.
- Runtime upload-buffer warnings and managed exceptions were absent in the
  verification logs. Native D3D12 info-queue interface diagnostic remains at
  engine startup; device creation and rendering still succeed.

## Evidence and limits

Local logs: QA/roster-review-*.log. Mission evidence: QA/RosterReview/Mission.
Performance samples: QA/RosterReview/Performance*, each a 20-second stationary
seed-100 measurement after scene setup, at 1280x720 D3D12. These are frame
intervals, not isolated GPU timings or a whole-map traversal benchmark.

The first sample had one 120.785 ms frame (median 16.667 ms, P95 16.670 ms).
This outlier's cause is not established and is not attributed to GC, drivers or
asset loading without evidence. Repeat results are recorded below.

| Run | Median ms | P95 ms | Maximum ms | Frames over 50 ms |
| --- | --- | --- | --- | --- |
| Initial | 16.667 | 16.670 | 120.785 | 1 |
| Repeat 2 | 16.667 | 16.674 | 16.930 | 0 |
| Repeat 3 | 16.667 | 16.672 | 16.910 | 0 |

The two repeats ran sequentially after the other game tests finished. The
initial hitch did not recur in those samples; its cause remains unconfirmed.

This bounded verification does not guarantee absence of all gameplay bugs,
six-real-client network issues, or problems on other hardware.
