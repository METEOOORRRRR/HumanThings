# D3D12 warning investigation

Date: 2026-09-16. Unity 6000.3.21f1, Windows development player,
NVIDIA GeForce RTX 4070 Ti, 1280x720, seed 100, 60 FPS cap.

## Findings

### Info queue

`d3d12: failed to query info queue interface (0x80004002)` occurs before
BeforeSceneLoad, before application resource loading. Device creation succeeds
and reports Direct3D12 feature level 12.2. It is a diagnostic-interface query
failure, not a failure to initialize rendering. Microsoft documents that
ID3D12InfoQueue requires the debug layer to be enabled. We did not install or
enable a debug layer, and did not establish whether it is absent or disabled
on this machine. No device loss, crash or rendering exception was observed.

Reference: https://learn.microsoft.com/ko-kr/windows/win32/api/d3d12sdklayers/nn-d3d12sdklayers-id3d12infoqueue

### Upload buffer

`Size: 16777216. Requested: 67108864` first occurs inside
Resources.Load("PlayerCharacterCatalog"), both in isolated loading and the
normal WorldView.Awake path. It occurs once per D3D12 process, not once per frame.

Reference chain:

- WorldView.Awake -> PlayerCharacterCatalog.Load -> character visual prefabs.
- Each prefab's HumanThingsCharacterVisual.originalMaterial references
  BakedMaterial embedded in the original character GLB.
- BakedMaterial references embedded uncompressed ARGB32 textures.
- Both GLBs contain `texture_0_metallic_roughness`, 4096x4096 ARGB32.
  One top mip requires exactly 64 MiB, matching the request.
- These are separate from the compressed BC7 textures used by the visible
  HumanThings materials, including the cleaned Toxic Bunny base map.

Original files:

- Assets/Character/Meshy_AI_Neon_Vanguard_All_Animations.glb
- Assets/Character/Meshy_AI_Toxic_Bunny_R_31_All_Animations.glb

The six embedded original textures remain resident after mission creation.
Runtime Profiler.GetRuntimeMemorySizeLong totals 235,014,784 bytes (224.13 MiB):
64 MiB + 85.375 MiB metallic/roughness, plus two 16 MiB and two 21.375 MiB
color/normal textures, including reported allocation overhead. This is Unity's
runtime texture-memory estimate, not an independent measurement of dedicated VRAM.

The isolated catalog load took 255.2 ms. WorldView.Awake in regular runs took
191.4-257.4 ms with D3D12 and 230.5-243.3 ms with D3D11. These intervals also
include meshes, materials and other setup; they are NOT the cost of the warning
or buffer resize alone. No cold-cache or engine-level GPU upload trace was taken.

Changing QualitySettings.asyncUploadBufferSize from 16 to 128 MiB before scene
load did not remove or alter the native 16-to-64 MiB warning. Therefore changing
that quality setting is not a verified remedy for this upload path.

## Runtime comparison

Each run samples 20 seconds after mission initialization and a two-second
settling period. Values are application frame intervals, not isolated GPU times.
The camera remains at the same base-camp view; these are not full-city traversal
or long multiplayer soak tests. D3D11 run A overlapped an Editor dependency audit;
run B was repeated after that Editor exited.

| API / async MB / run | Frames | Median ms | P95 ms | Maximum ms | Frames >50ms | Upload warnings |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| D3D12 / 16 / A | 1200 | 16.667 | 16.669 | 23.988 | 0 | 1 |
| D3D12 / 16 / B | 1201 | 16.667 | 16.670 | 17.044 | 0 | 1 |
| D3D12 / 128 / A | 1200 | 16.667 | 16.672 | 19.261 | 0 | 1 |
| D3D12 / 128 / B | 1201 | 16.667 | 16.674 | 16.875 | 0 | 1 |
| D3D11 / 16 / A | 1200 | 16.667 | 16.672 | 17.897 | 0 | 0 |
| D3D11 / 16 / B | 1201 | 16.667 | 16.670 | 16.805 | 0 | 0 |

All six processes exited normally. No sustained frame degradation or recurring
upload stall was observed in this limited workload. The FPS cap can hide
differences below the frame budget. No claim is made about lower-memory GPUs.

## Scope and next remedy

Only opt-in development diagnostics were added. Default graphics API, quality,
textures, materials, map and gameplay remain unchanged. No warning suppression
was added. The appropriate follow-up is to separate editor comparison assets
from gameplay dependencies and verify that original GLB texture subassets no
longer enter the player. Simply deleting the originalMaterial assignment may
not suffice if nested GLB prefab dependencies still retain them; mesh/animation
references and the original-look comparison must be preserved deliberately.

## Separation applied

The follow-up replaces the two visual prefabs' direct originalMaterial references
with GUID/local-file-ID locators. HumanThingsCharacterVisual resolves these only
in the Unity Editor, on demand. Runtime CompareOriginal cannot load editor assets
and safely retains the gameplay material. The normal Refresh path no longer
touches the comparison getter. Existing editor builders and comparison tools
retain their originalMaterial property API. Original GLBs, meshes, animation
controllers, material values and texture import settings are unchanged.

The build experiment confirms this is sufficient for the actual character load
path even though editor prefab variants preserve their base-prefab relationship:

- Catalog texture count: 88 -> 82.
- Catalog texture memory estimate: 348,556,832 -> 113,542,048 bytes.
- Exactly six original textures removed: 235,014,784 bytes (224.13 MiB).
- No upload-buffer warnings in isolation, performance, character or waiting-room
  runs on D3D12. The unrelated info-queue diagnostic query message remains.
- Isolated catalog load: 255.2 ms before, 65.1 ms after, single samples; this is
  not a controlled cold-cache benchmark.
- Post-change 20-second sample: 1200 frames, median 16.667 ms, P95 16.671 ms,
  maximum 17.090 ms, zero frames over 50 ms.
- EditMode: 447 passed. Focused PlayMode comparison/animation: 2 passed.
- Runtime character tests: both characters, correct 4K cleaned Bunny texture,
  idle/walk/sprint/idle transitions, no embedded original textures loaded.
- Waiting room: 200 checks passed, including assigned character portraits.
- Real two-process host/client mission: all three deliveries and three archive
  entries completed at simulation time 327.3 seconds. Scrap collection, module
  crafting/installation, skill checks and Maze/Alignment/Crane puzzles passed.
  This existing fixture disables monsters to isolate the recovery loop.
  Both processes exited normally with zero upload warnings and exceptions.

Saved before/after portrait captures were visually inspected. These are not
bit-identical rendered images: mean absolute RGB-channel differences on a 0-255
scale are 0.7863 (Neon Vanguard) and 0.5841 (Toxic Bunny), maximum 41. No source
geometry, textures, shaders or material values were edited. These small render
differences are recorded rather than claiming exact pixel equivalence.

Local separation evidence: QA/GraphicsSeparation, QA/separation-*.log.

## Reproduction

Build via EarthRecovery.Editor.ProjectBuilder.Build into Builds/Latest.
Run that same player with the following additional arguments:

```
--graphics-trace --graphics-isolate --graphics-output <absolute-output-folder>
--graphics-trace --dev-solo --upload-mb 16 --graphics-output <absolute-output-folder> -force-d3d12
--graphics-trace --dev-solo --upload-mb 128 --graphics-output <absolute-output-folder> -force-d3d12
--graphics-trace --dev-solo --upload-mb 16 --graphics-output <absolute-output-folder> -force-d3d11
```

Use separate output folders and -logFile paths. Run performance cases sequentially
in displayed windows. Do not pass --graphics-trace during ordinary play.
Editor method EarthRecovery.Editor.GraphicsUploadAudit.Capture writes dependency
details without modifying assets. Raw local evidence is in QA/GraphicsTrace and
QA/graphics-*.log (ignored QA outputs).
