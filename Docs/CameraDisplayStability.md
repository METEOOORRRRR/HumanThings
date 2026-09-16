# Camera and Display Stability

## Changes

- Local look and body rotation are presented together in LateUpdate at render rate. Network command (20 Hz) and snapshot (10 Hz) rates are unchanged. Remote body rotation is smoothed.
- The camera boom retracts immediately on obstruction and eases outward. Its collision radius covers the near-plane corners, including ultrawide viewports. It never enforces a minimum distance through a wall.
- A followed avatar becomes shadows-only when the camera approaches its renderer bounds; hysteresis prevents toggling at the boundary. Other avatars remain visible. Visibility restores when the camera moves away or changes spectator target.
- The existing environment treatment uses TAA with moderate history contribution and mild sharpening. History resets on camera cuts, nearby-body visibility changes and viewport changes. The character has a URP skinned MotionVectors pass.
- Separate mipmapped character texture copies retain source resolution, glTF channel encoding and sRGB/linear interpretation. Original GLB, importer, rig, animations and original prefab are unchanged.
- Fullscreen requests the current display's native dimensions in borderless mode. Windowed size is remembered. Startup fullscreen and Alt+Enter transitions are also handled.
- Canvas scaleFactor now owns the final UI scale, rather than additionally stretching a nested layout. Pixel-perfect UI positioning is enabled. Existing legacy fonts and TMP styling are preserved.
- IMGUI HUD and terminal text rasterize at their output pixel font size, with matching measurements for wrapped/scrolling text. Scroll clips and glyphs now share native pixel coordinates; changing the GUI matrix inside an already-created reference-scale clip displaced terminal/archive text. GUI transforms are restored after drawing.
- Large menu/terminal background images and the title logo use mipmaps with trilinear minification. Their source PNG pixels and existing compression settings are unchanged.
- The waiting-room portrait RenderTexture grows with its on-screen pixel size and is rendered again after a size change, instead of enlarging a fixed 256x320 image.

## Verification

- Unity 6000.3.21f1: 426 EditMode tests and 21 targeted WorldTests PlayMode tests passed on September 16. The earlier full PlayMode run passed 45 tests.
- The initially running `Builds/Latest` was stale: its runtime DLL SHA-256 began `4B81F864`, versus `F4D34C96` in the separate QA player. That separate build had not been deployed to the normal launch path. Old players were closed with the user's approval and `ProjectBuilder.Build` now publishes through staging/retention to `Builds/Latest/EarthRecovery.exe`.
- `--camera-qa -batchmode`: actual seed-100 city rendered offscreen; 324 frame samples, maximum local body/camera yaw difference 0.000031 degrees, 110 close-wall hidden frames, collision clearance and visibility restoration passed.
- Visible-window `--camera-qa` from Latest also passed: 325 samples, maximum body/camera yaw difference 0.000031 degrees, 110 close-wall hidden frames, restoration and collision clearance. Camera images and logs: `QA/CameraDisplay/Game/` and `VisibleCamera.log`, including FXAA/TAA orbit captures and close-wall/restored views. These demonstrate rendering and camera behavior, not a quantitative perceptual shimmer benchmark.
- Original-source SHA-256 check: all 18 baseline files unchanged.
- `--display-qa -batchmode`: earlier 49 canvas-bounds/dropdown checks passed across all 13 aspect choices and seven window sizes. The final visible-window run passed 89 checks, including fullscreen title/settings/HUD/terminal, other game panels, and pixel containment of scrolled text. Captures are under `Builds/Latest/DisplayQA/`. Fullscreen capture dimensions are 2560x1440, matching this display; HUD and terminal scroll text were inspected at 1:1 pixels.
- Final published runtime DLL SHA-256: `9E4F0F90614C5D159208C7A587CF7719B46A71035E956B1AE93650463B09824D`. Build succeeded with zero errors. Retention was subsequently updated to Latest plus at most three previous builds; developer launches always use that same Latest player with `--dev-solo`.

## Limits

The menu, waiting-room and terminal background artwork is 1672x941 pixels. Native-resolution UI text can remain sharp on a larger monitor, but enlarging those bitmap backgrounds cannot create missing source detail. Their artwork has not been regenerated or artificially sharpened.

TAA can trade some fine-detail sharpness for reduced temporal aliasing; testing on other GPUs, real remote peers and more city viewpoints is still useful. No claim is made that all perceptual shimmer is eliminated in every scene.
