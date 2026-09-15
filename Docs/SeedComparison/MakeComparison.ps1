Add-Type -AssemblyName System.Drawing
$folder = Join-Path $PSScriptRoot 'MarketBranches'
$rows = Import-Csv -LiteralPath (Join-Path $folder 'Summary.csv')
$font = New-Object System.Drawing.Font('Segoe UI', 20)
$small = New-Object System.Drawing.Font('Segoe UI', 13)
$brush = [System.Drawing.Brushes]::White
foreach ($view in @('Perspective', 'Top')) {
    $bitmap = New-Object System.Drawing.Bitmap(2160, 1260)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::FromArgb(23, 26, 29))
        for ($i = 0; $i -lt $rows.Count; $i++) {
            $row = $rows[$i]
            $x = ($i % 3) * 720
            $y = [math]::Floor($i / 3) * 630
            $graphics.DrawString("SEED $($row.seed)", $font, $brush, $x + 18, $y + 12)
            $graphics.DrawString("Roads $($row.roads) | Buildings $($row.buildings) | Vehicles $($row.vehicles)", $small, $brush, $x + 18, $y + 49)
            $source = [System.Drawing.Image]::FromFile((Join-Path $folder "Seed$($row.seed)-$view.png"))
            try { $graphics.DrawImage($source, [System.Drawing.Rectangle]::new($x, $y + 82, 720, 540)) }
            finally { $source.Dispose() }
        }
        $graphics.DrawString('MarketBranches', $font, $brush, 1470, 730)
        $graphics.DrawString("SAME TEMPLATE / FIVE SEEDS`n`nView: $view`nCamera, scale and lighting fixed.`nNo layouts selected or discarded.`n`nUnity render / Synty POLYGON City Pack", $small, $brush, 1470, 790)
        $bitmap.Save((Join-Path $folder "Comparison-$view.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $graphics.Dispose(); $bitmap.Dispose() }
}
$font.Dispose()
$small.Dispose()
