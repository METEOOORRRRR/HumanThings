param([string]$QaPath = 'D:/HumanThings/QA/NeonOutriderTexture')
Add-Type -AssemblyName System.Drawing
$font = New-Object Drawing.Font 'Malgun Gothic',26
$small = New-Object Drawing.Font 'Malgun Gothic',15
$white = [Drawing.Brushes]::White
$gray = [Drawing.Brushes]::LightGray
foreach ($view in @('front','face')) {
    $before = [Drawing.Image]::FromFile((Join-Path $QaPath "original/$view.png"))
    $after = [Drawing.Image]::FromFile((Join-Path $QaPath "final/$view.png"))
    $canvas = New-Object Drawing.Bitmap ($before.Width*2),($before.Height+108)
    $g = [Drawing.Graphics]::FromImage($canvas)
    $g.Clear([Drawing.Color]::FromArgb(30,33,38))
    $g.DrawString('원본', $font, $white, 24, 10)
    $g.DrawString('수정본 · 실제 3D 렌더', $font, $white, ($before.Width+24), 10)
    $g.DrawImage($before,0,64,$before.Width,$before.Height)
    $g.DrawImage($after,$before.Width,64,$after.Width,$after.Height)
    $g.DrawString('동일한 메시·UV·카메라·조명 / 색상 텍스처와 재질만 수정', $small, $gray, 24, ($before.Height+72))
    $canvas.Save((Join-Path $QaPath "final/$view-before-after.jpg"),[Drawing.Imaging.ImageFormat]::Jpeg)
    $g.Dispose(); $canvas.Dispose(); $before.Dispose(); $after.Dispose()
}
$font.Dispose(); $small.Dispose()
