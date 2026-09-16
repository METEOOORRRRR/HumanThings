param([string[]]$Keys, [string]$Tag = 'iteration_1', [switch]$Package, [switch]$SkipBake)
$ErrorActionPreference = 'Stop'
Set-Location 'D:/HumanThings'
$python = 'C:/Users/haeso/AppData/Local/Programs/Python/Python39/python.exe'
$blender = 'C:/Program Files/Blender Foundation/Blender 3.3/blender.exe'
$characters = Get-Content 'QA/CharacterBatch/characters.json' -Raw | ConvertFrom-Json
foreach ($character in $characters) {
    if ($Keys -and $character.key -notin $Keys) { continue }
    $qa = "D:/HumanThings/QA/CharacterBatch/$($character.key)"
    $source = "D:/HumanThings/Assets/Character/Meshy_AI_$($character.key)_All_Animations.glb"
    $texture = "$qa/BaseColor_$Tag.png"
    if (!$SkipBake) {
        & $python 'Tools/TextureRefinement/project_textures.py' $qa $texture 1.50 .48 1.32
        if ($LASTEXITCODE -ne 0) { throw "Bake failed: $($character.key)" }
    }
    if ($Package) {
        $destination = "D:/HumanThings/Assets/Character/$($character.prefix)Clean"
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        $finalTexture = "$destination/$($character.prefix)_BaseColor_4K.png"
        $finalModel = "$destination/Meshy_AI_$($character.key)_Clean_All_Animations.glb"
        Copy-Item -LiteralPath $texture -Destination $finalTexture
        & $python 'Tools/TextureRefinement/package_glb.py' $finalTexture $finalModel '-' $source $character.prefix
        if ($LASTEXITCODE -ne 0) { throw "Package failed: $($character.key)" }
        & $blender -b -t 4 -P 'Tools/TextureRefinement/render_model.py' -- $finalModel final '-' $qa 1.50 .48
    } else {
        & $blender -b -t 4 -P 'Tools/TextureRefinement/render_model.py' -- $source $Tag $texture $qa 1.50 .48
    }
    if ($LASTEXITCODE -ne 0) { throw "Render failed: $($character.key)" }
    Write-Output "BAKED $($character.key) $Tag"
}
