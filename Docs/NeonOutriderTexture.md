# Neon Outrider texture refinement

The supplied MIRE concept sheet guides the repaint. The original GLB, embedded images, imported gameplay prefab, and original generated texture copies are preserved. The clean gameplay material references a separate derived atlas.

## Deliverables

- `Assets/Character/NeonOutriderClean/NeonOutrider_BaseColor_4K.png`
- `Assets/Character/NeonOutriderClean/NeonOutrider_Clean_All_Animations.glb`
- The companion `.validation.json` checks source preservation and identical geometry, UVs, rigs, animations, and original binary bytes.

Source SHA-256: `C127E83706A9DEEB0215730E6C455FC957513D41F93502FAD6DC8832916BF683`.

## Process

Built-in imagegen painted the actual model's orthographic projections using the supplied concept as the style reference. Numerical tools performed 3D projection, visibility checks, UV resampling and padding, and packaging; they did not substitute procedural artwork for the generated repaint.

1. First applied four-view repaint and a detailed face repaint. Eyes became visible; some jacket edges and straps remained blurred.
2. Repainted the front at greater detail and calibrated its framing at projection time. Sharper ivory jacket edges, dark cloth, cyan lining and hardware; inspected full-body and face renders.
3. Reduced blending between mismatched viewing directions to remove duplicate pouch markings. Selected this version after checking front, back, both sides and three-quarter views.

The delivered GLB keeps all original binary data and adds the clean base-color image. Its material uses metallic 0, roughness 0.82 and normal strength 0. The game profile also disables added dirt/dust/wear and restores the painted color values. Original normal and metallic textures are preserved.

The 4096x4096 atlas is a bake of lower-resolution projection paintings. About 48.18% of occupied UV texels are directly covered; nearby painted 3D surface colors extend coverage to about 95.59%. Deeply occluded areas retain source color. Existing angular geometry, hair shapes, self-shadowing and deformation remain; this is texture/material refinement, not a mesh or rig repair.

Review artifacts and iteration renders: `QA/NeonOutriderTexture`.

## Verification and game integration

On 2026-09-16, the delivered GLB was re-imported in Blender and rendered from six
angles, plus Walking frame 13 and Running frame 9. Unity's clean visual variant
was also rendered. All 13 character-roster EditMode tests passed, including the
new separate-texture/original-material preservation check.

`EarthRecovery.Editor.ProjectBuilder.Build` published the clean material to
`Builds/Latest`, retaining three previous builds. The exact Latest executable
was verified running with `--dev-solo --character-qa` and then
`--dev-solo --waiting-qa`. All three characters passed idle/walk/sprint/return-to-idle;
the player explicitly confirmed `NeonOutrider_BaseColor_4K` at 4096x4096 with
source normal/metallic effects and added weathering disabled. All 201 waiting-room
checks passed, and the actual Outrider portrait was visually inspected.

Latest runtime DLL SHA-256:
`7DE9B968127B539F151CA8A7EA749EB8EA5E24D83CC56CDDE31F9FED394B0229`.
Source hashes were checked again after publication and runtime verification.

Actual-model comparison images:
`QA/NeonOutriderTexture/final/front-before-after.jpg` and
`QA/NeonOutriderTexture/final/face-before-after.jpg`.

## Final prompt set — built-in imagegen

### Four-view repaint

Use case: precise-object-edit. Asset type: UV projection painting for an EXISTING 3D game character. Input 1 is the exact edit target: a 3072x1024 four-panel orthographic projection, panels 768x1024 in this exact order FRONT, SIDE FACING LEFT, BACK, SIDE FACING RIGHT. Input 2 is ONLY the character concept reference. Repaint the surface colors of input 1 to match the clean MIRE reference. Preserve the exact input 1 canvas, camera, character positions, proportions, low-poly silhouettes, pose, clothing region boundaries, pockets, backpack and hair outline. Pixel registration to input 1 is critical because this image will be projected onto the same mesh. DO NOT create a new turnaround or change the shape to the reference. Remove ALL white glitter, smear marks, patchy black/white artifacts, cyan stains and baked specular highlights. Make the jacket and voluminous cargo pants clean matte charcoal, with controlled dark-gray fold planes only. White shoulder/sleeve panels, ivory hanging thigh pouches and ivory boot panels must be even warm light gray with clean boundaries and a few intentional seam lines. Shirt black, bare torso/forearms warm ivory skin. Hair pale ash blonde with defined separated painted locks and subtle beige-gray shading, not bleached blank patches. Black respirator with two tidy cyan circular filter rings; cyan inner collar, cyan backpack canisters, small mustard yellow straps/hardware. Keep existing A / HUMAN THINGS markings exactly at their existing locations and make them neat. Boots and gloves clean black and ivory color blocking. Front face needs visible open gray-brown anime eyes with neat dark upper lashes, not closed eyes or random dark streaks. No extra details, no weathering, no dirt, no grain, no photoreal skin. Flat diffuse albedo painting, no cast shadows or highlights. Background stays uniform RGB128,133,141. Return ONLY the same four-panel projection image, ideally 3072x1024, without labels, captions, diagrams, borders, or added views. Exact registration is more important than inventing geometry.

### Face repaint

Use case: precise-object-edit. Input 1 is the EXACT pixel-registered orthographic front head projection of an existing 3D mesh, 1024x1024. Input 2 is the MIRE concept/style reference only. Repaint input 1 as clean premium stylized game albedo; never change its pose, camera, canvas, head scale, center, outer hair silhouette, hair tie location, shoulders, collar contours or mask silhouette. Output is projected onto this same 3D mesh, so exact position matching is essential. Replace the muddy marks in the exposed face with TWO CLEAR OPEN almond-shaped gray-brown eyes positioned over the existing eye marks around (445,565) and (567,563). Distinct offwhite sclera, dark upper lash/eyeliner, iris and pupil, small restrained highlight, tidy dark eyebrows just above. Avoid closed eyes, blank eyes or extra eyes. Pale warm skin with gentle soft shading, no stains or black speckles. Restore clean pale ash-blonde hair with individually separated fine locks and gentle taupe shadows; preserve the high ponytail and all outline pieces, no white glare or dirty gray streaks. Keep respirator at its current contour and position, top edge around y615 and lower part near y775, clean matte charcoal with tidy cyan filter rings at both existing circular housings. Preserve cyan collar and black jacket, paint neat gold strap hardware. Clean every material artifact while keeping existing color-region boundaries. No dramatic lighting, gloss, metallic glare, cast shadows, new geometry, labels or layout change. Flat diffuse surface painting only. Background exactly uniform RGB128,133,141. The reference is a STYLE guide; do not recrop or reshape the edit target. Return one edited 1024x1024 square image only.

### Front refinement

Use case: precise-object-edit. Image 1 is the exact EDIT TARGET, a 768x1024 orthographic ALBEDO projection of an existing 3D character. Image 2 is concept reference only. Refine texture paint in image 1 in place for direct projection back onto the SAME model. Preserve image 1 exact 3:4 canvas, framing, silhouette, pose, proportions, all boundaries of clothes, hair, pockets, hands and boots. Do not move or resize anything. Main correction: remove blurred white/gray smears along the FRONT black jacket opening, breast/shoulder straps, cyan collar and pocket straps. Make those surfaces clean matte black/charcoal with crisply defined seams and buckles. Maintain two slim IVORY vertical jacket edge panels with small mustard rectangular tabs at their existing exact positions; NO wide yellow straps and no random white strokes on black. Ivory outer sleeve and hanging pouch regions should be even warm pale gray, well-separated from clean dark cloth. Trousers charcoal with subtle intentional fold planes, no speckles or random streaks. Gloves tidy dark panels, boots crisp black-and-ivory panel boundaries with existing small A logos. Retain clean visible gray-brown eyes, pale ash-blonde hair and black/cyan respirator. Clean warm skin without triangle-shaped stains. Cyan lining should have a smooth restrained teal fold treatment, no white glare. This is diffuse surface color only: no rendered gloss, harsh cast shadows, grain, texture noise or weathering. Keep every region REGISTERED EXACTLY to image 1; image 2 guides palette and finishing only. Uniform background RGB128,133,141. Return ONE 1536x2048 or equivalent 3:4 portrait, no labels, no extra views, no frame.
