Add-Type -AssemblyName System.Drawing
$data = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Report.json') -Raw | ConvertFrom-Json
$font = [System.Drawing.Font]::new('Malgun Gothic', 18)
$title = [System.Drawing.Font]::new('Malgun Gothic', 26, [System.Drawing.FontStyle]::Bold)
$small = [System.Drawing.Font]::new('Malgun Gothic', 13)
$white = [System.Drawing.Brushes]::White
$black = [System.Drawing.Brushes]::Black
$line = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(155, 225, 235, 240), 2)
$line.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
$summary = [System.Drawing.Bitmap]::new(2160, 1380)
$sg = [System.Drawing.Graphics]::FromImage($summary)
$sg.Clear([System.Drawing.Color]::FromArgb(23, 26, 29))
for ($i = 0; $i -lt $data.seeds.Count; $i++) {
    $seed = $data.seeds[$i]
    $map = [System.Drawing.Bitmap]::new(2000, 1500)
    $g = [System.Drawing.Graphics]::FromImage($map)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::FromArgb(23, 26, 29))
    $source = [System.Drawing.Image]::FromFile((Join-Path $PSScriptRoot "Seed$($seed.seed)-Map.png"))
    $g.DrawImage($source, [System.Drawing.Rectangle]::new(0, 80, 1600, 1400))
    $source.Dispose()
    $g.DrawString("WORLD SEED $($seed.seed)   |   15/15 POIs   |   FIXED CAMERA", $title, $white, 20, 18)
    foreach ($c in $seed.chunks) {
        $x = [single]($c.min.x * 1600)
        $y = [single](80 + (1 - $c.max.y) * 1400)
        $w = [single](($c.max.x - $c.min.x) * 1600)
        $h = [single](($c.max.y - $c.min.y) * 1400)
        $g.DrawRectangle($line, $x, $y, $w, $h)
        $g.DrawString("$($c.id): $($c.count)", $small, $white, $x + 7, $y + 7)
    }
    foreach ($p in $seed.pois) {
        $x = [single]($p.viewport.x * 1600)
        $y = [single](80 + (1 - $p.viewport.y) * 1400)
        $g.FillEllipse($white, $x - 15, $y - 15, 30, 30)
        $g.DrawString([string]$p.number, $small, $black, $x - 11, $y - 13)
        $ly = 108 + ($p.number - 1) * 78
        $g.DrawString("$($p.number). $($p.name)", $font, $white, 1630, $ly)
        $g.DrawString($p.chunk, $small, $white, 1654, $ly + 33)
    }
    $g.FillRectangle([System.Drawing.Brushes]::Cyan, 790, 770, 20, 20)
    $g.DrawString('BASE CAMP', $font, $white, 814, 756)
    $g.DrawString('Numbers / dashed lines are', $small, $white, 1630, 1330)
    $g.DrawString('comparison annotations.', $small, $white, 1630, 1360)
    $map.Save((Join-Path $PSScriptRoot "Seed$($seed.seed)-Annotated.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $sx = ($i % 3) * 720
    $sy = [math]::Floor($i / 3) * 690
    $sg.DrawString("SEED $($seed.seed)  /  15 POIs", $font, $white, $sx + 16, $sy + 10)
    $sg.DrawImage($map, [System.Drawing.Rectangle]::new($sx, $sy + 50, 720, 630), [System.Drawing.Rectangle]::new(0, 80, 1600, 1400), [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    $map.Dispose()
    $sheet = [System.Drawing.Bitmap]::new(1920, 2600)
    $fg = [System.Drawing.Graphics]::FromImage($sheet)
    $fg.Clear([System.Drawing.Color]::FromArgb(23, 26, 29))
    foreach ($p in $seed.pois) {
        $n = $p.number - 1
        $x = ($n % 3) * 640
        $y = [math]::Floor($n / 3) * 520
        $fg.DrawString("$($p.number). $($p.name) / Seed $($seed.seed)", $font, $white, $x + 12, $y + 4)
        $im = [System.Drawing.Image]::FromFile((Join-Path $PSScriptRoot $p.image))
        $fg.DrawImage($im, [System.Drawing.Rectangle]::new($x, $y + 40, 640, 480))
        $im.Dispose()
    }
    $sheet.Save((Join-Path $PSScriptRoot "Seed$($seed.seed)-Facades.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $fg.Dispose()
    $sheet.Dispose()
}
$sg.DrawString('Fixed POI key', $title, $white, 1460, 730)
foreach ($p in $data.seeds[0].pois) {
    $col = [math]::Floor(($p.number - 1) / 8)
    $row = ($p.number - 1) % 8
    $sg.DrawString("$($p.number). $($p.name)", $font, $white, 1460 + $col * 320, 800 + $row * 55)
}
$sg.DrawString('Cyan square = Base Camp', $small, $white, 1460, 1280)
$summary.Save((Join-Path $PSScriptRoot 'MapComparison.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$sg.Dispose()
$summary.Dispose()
$font.Dispose()
$title.Dispose()
$small.Dispose()
$line.Dispose()
