# Five character texture refinement

The five supplied GLBs have separate clean variants. The original source GLBs and their embedded textures are unchanged. The corrected textures are also applied to separate gameplay material/prefab variants.

| Concept | Original filename key | Clean GLB | 4K diffuse atlas |
| --- | --- | --- | --- |
| HAZE | Tomorrow_s_Sentinel | [GLB](../Assets/Character/TomorrowSentinelClean/Meshy_AI_Tomorrow_s_Sentinel_Clean_All_Animations.glb) | [PNG](../Assets/Character/TomorrowSentinelClean/TomorrowSentinel_BaseColor_4K.png) |
| KAI | Ashen_Sentinel | [GLB](../Assets/Character/AshenSentinelClean/Meshy_AI_Ashen_Sentinel_Clean_All_Animations.glb) | [PNG](../Assets/Character/AshenSentinelClean/AshenSentinel_BaseColor_4K.png) |
| NOVA | Nova_Ghost_Scout | [GLB](../Assets/Character/NovaGhostScoutClean/Meshy_AI_Nova_Ghost_Scout_Clean_All_Animations.glb) | [PNG](../Assets/Character/NovaGhostScoutClean/NovaGhostScout_BaseColor_4K.png) |
| REI | Crimson_Reclaimer | [GLB](../Assets/Character/CrimsonReclaimerClean/Meshy_AI_Crimson_Reclaimer_Clean_All_Animations.glb) | [PNG](../Assets/Character/CrimsonReclaimerClean/CrimsonReclaimer_BaseColor_4K.png) |
| ASH | Wasteland_Sentinel | [GLB](../Assets/Character/WastelandSentinelClean/Meshy_AI_Wasteland_Sentinel_Clean_All_Animations.glb) | [PNG](../Assets/Character/WastelandSentinelClean/WastelandSentinel_BaseColor_4K.png) |

Names were matched by actual rendered appearance: **Ashen Sentinel is KAI; Wasteland Sentinel is ASH**.

## Painting and iteration

Built-in ImageGen repainted orthographic projections of each original mesh, with the supplied concept sheet as the secondary reference. Each character received a four-view body repaint, a separate face/gas-mask repaint, and higher-resolution front and side refinements. Full prompts are in [initial and front prompts](CharacterBatchTexturePrompts.md) and [side/local correction prompts](CharacterBatchTextureSidePrompts.md).

1. Extract the embedded albedo and UV surface without changing the source. Render original front, back, sides, face and three-quarter views under consistent lighting.
2. Project the generated four-view painting and dedicated face painting onto the original UVs. Render and inspect iteration 1 on the actual rigged mesh.
3. Refine the front clothing, straps, belt and boots. Calibrate projection sampling to the original garment heights and rebake iteration 2. Recheck the face and all viewing angles.
4. Inspect exported GLBs. Residual pale bleed on dark side equipment/trousers required a third pass. Repaint both side views, explicitly remove HAZE's erroneous pale trouser triangles and KAI's white hair streak, then rebake and inspect iteration 3.
5. Package the selected atlas into a separate GLB. Reimport that exported GLB, render six views and sample both Walking and Running. The final Blender review scene packs its textures.

HAZE keeps tan skin, brown/orange hair, orange equipment and flank tattoo. KAI keeps the monochrome palette and asymmetric white fabric. NOVA keeps pale hair, white coat, yellow equipment and exposed-leg boundaries. REI keeps black hair, red eyes/collar and ivory jacket. ASH retains deliberate camouflage, ragged cloak and opaque olive goggles; no human face was invented.

The 4096×4096 atlases are UV bakes assembled from the generated projection paintings, not native 4K image-generation outputs. Original topology, angular hair/folds and any mesh holes remain. Texture painting cannot repair those geometry limitations. Deep occluded surfaces outside all painted views retain original albedo, with nearby painted colors extended in 3D to reduce visible seams. Per-character coverage is recorded in `QA/CharacterBatch/<key>/projection_report.json`.

The clean GLBs use a matte material (metallic 0, roughness 0.82) with the noisy source normal map disabled. All original binary bytes are preserved; a new PNG is appended and material/image references are changed. Meshes, UVs, nodes, skins, accessors and animation data are identical to the source. Each export has an adjacent `.validation.json` report.

## Review files

- [Actual final models, five angles](../QA/CharacterBatch/final-contact.jpg)
- [Original/final face comparison on the actual meshes](../QA/CharacterBatch/faces-before-after.jpg)
- `QA/CharacterBatch/<key>/final/front-before-after.jpg` and `face-before-after.jpg`: original versus final under the same lighting.
- `QA/CharacterBatch/<key>/final/review.blend`: packed final model review scene.
- `QA/CharacterBatch/<key>/final/walking_sample.png` and `running_sample.png`: exported animation samples.
- `QA/CharacterBatch/original-preservation.json`: source SHA-256 before/after checks.
- `QA/CharacterBatch/package-verification.json`: geometry/UV/rig/animation preservation checks.

## Regeneration

`Tools/TextureRefinement/prepare_batch.ps1` extracts and renders the original sources. The selected ImageGen paintings, calibration and surface data are in `QA/CharacterBatch/<key>` (local QA files, not runtime dependencies). `bake_batch.ps1` projects them and can package/reimport each result. Never overwrite the original GLBs.

Run `EarthRecovery.Editor.AdditionalCharacterBuilder.Build` to rebuild the five gameplay variants. It imports originals only when the derived gameplay prefab is missing, preserves the original comparison material, registers the five stable IDs and applies each separate clean atlas. Source normal/metallic/weathering effects are neutralized for the repainted material. All eight roster entries render their own waiting-room portraits.

## Verification

Source SHA-256 checks and export binary/structure checks passed for all five. Exported GLBs were reimported and visually inspected in front, back, both sides, face and three-quarter views; Walking and Running were rendered. Repaint coverage including nearby occluded-surface extension is 94.70% HAZE, 98.20% KAI, 96.31% NOVA, 98.01% REI and 97.23% ASH. The remaining deeply hidden surface retains original colors.

Unity EditMode: 14/14 character roster tests passed. Unity PlayMode: 13/13 avatar and host/client roster tests passed. These verify all eight model assignments, source rig/clip preservation, actual bone animation, 1.8-metre gameplay scale, separate clean material references and network synchronization. Reports are in `QA/CharacterBatch/editmode.xml` and `playmode.xml`.

Published with `EarthRecovery.Editor.ProjectBuilder.Build`: `BUILD_RESULT Succeeded errors=0`, retaining Latest and three previous builds. The actual `D:/HumanThings/Builds/Latest/EarthRecovery.exe --dev-solo --character-qa` process passed all eight characters' idle, walking, sprinting and return-to-idle checks. All seven repainted roster entries reported their separate 4096-pixel albedos active. Process path/flags, build log and runtime result are recorded in `QA/CharacterBatch`.

Latest runtime DLL SHA-256: `BBA9F8194105CDFC3C64CE224AFEF94DAC1A658A8CAC5204D55BEEE090F5CE45`.

The same Latest player passed all **225 waiting-room checks** with `--dev-solo --waiting-qa`: six actual local transport participants, all eight character portraits, rotating the full roster through six cards, clean material references, cached portrait cameras and four UI resolutions. The five new in-game portraits were visually inspected. Verification and the exact process path/flags are in `QA/CharacterBatch/waiting-verification.txt` and `waiting-process.json`.
