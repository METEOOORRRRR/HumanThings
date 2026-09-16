param([string]$Root = 'D:/HumanThings')
$ErrorActionPreference = 'Stop'
$blender = 'C:/Program Files/Blender Foundation/Blender 3.3/blender.exe'
$sources = Get-Content "$Root/QA/CharacterBatch/sources.json" | ConvertFrom-Json
Add-Type -AssemblyName System.Drawing
foreach ($source in $sources) {
    $qa = "$Root/QA/CharacterBatch/$($source.name)"
    foreach ($tag in @('projection_source','original')) {
        & $blender -b -t 4 -P "$Root/Tools/TextureRefinement/render_model.py" -- $source.path $tag - $qa 1.50 0.48 *> "$qa/render-$tag.log"
        if ($LASTEXITCODE -ne 0) { throw "Render failed: $($source.name) $tag" }
    }
    & $blender -b -t 4 -P "$Root/Tools/TextureRefinement/export_surface.py" -- $source.path "$qa/surface.npz" *> "$qa/surface.log"
    if ($LASTEXITCODE -ne 0) { throw "Surface extraction failed: $($source.name)" }
    $sheet = New-Object Drawing.Bitmap 3072,1024
    $g = [Drawing.Graphics]::FromImage($sheet)
    $g.Clear([Drawing.Color]::FromArgb(128,133,141))
    $views = @('front','right','back','left')
    for ($i=0; $i -lt 4; $i++) {
        $im = [Drawing.Image]::FromFile("$qa/projection_source/$($views[$i]).png")
        $g.DrawImage($im,($i*768),0,768,1024); $im.Dispose()
    }
    $sheet.Save("$qa/turnaround-source.png",[Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $sheet.Dispose()
    foreach ($view in @('front','face')) {
        $im = [Drawing.Image]::FromFile("$qa/projection_source/$view.png")
        $canvas = New-Object Drawing.Bitmap $im.Width,$im.Height
        $g = [Drawing.Graphics]::FromImage($canvas)
        $g.Clear([Drawing.Color]::FromArgb(128,133,141))
        $g.DrawImage($im,0,0,$im.Width,$im.Height)
        $canvas.Save("$qa/$view-source.png",[Drawing.Imaging.ImageFormat]::Png)
        $g.Dispose(); $im.Dispose(); $canvas.Dispose()
    }
    Write-Output "PREPARED $($source.name)"
}
