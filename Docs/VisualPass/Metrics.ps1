Add-Type -AssemblyName System.Drawing
$rows = foreach ($view in @('Street','Boulevard','Detail','Overview')) {
    foreach ($pass in @('Original','Pass5')) {
        $path = Join-Path $PSScriptRoot "$pass-$view.png"
        $bitmap = [System.Drawing.Bitmap]::FromFile($path)
        try {
            $luma=0.0; $sat=0.0; $dark=0; $count=0; $magenta=0
            for ($y=[int]($bitmap.Height*.4); $y -lt $bitmap.Height; $y+=5) {
                for ($x=0; $x -lt $bitmap.Width; $x+=5) {
                    $c=$bitmap.GetPixel($x,$y)
                    $v=(.2126*$c.R+.7152*$c.G+.0722*$c.B)/255
                    $luma+=$v; $sat+=$c.GetSaturation(); $count++
                    if ($v -lt .06) {$dark++}
                    if ($c.R -gt 220 -and $c.B -gt 220 -and $c.G -lt 35) {$magenta++}
                }
            }
            [pscustomobject]@{View=$view;Pass=$pass;MeanLuma=[math]::Round($luma/$count,4);MeanSaturation=[math]::Round($sat/$count,4);DarkPercent=[math]::Round(100*$dark/$count,2);MagentaPixels=$magenta}
        } finally {$bitmap.Dispose()}
    }
}
$rows | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'Metrics.json') -Encoding utf8
$rows | Format-Table
