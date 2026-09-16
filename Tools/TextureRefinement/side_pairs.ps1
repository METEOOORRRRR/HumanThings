$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root='D:/HumanThings/QA/CharacterBatch'
foreach($character in (Get-Content "$root/characters.json" -Raw | ConvertFrom-Json)) {
    $canvas=New-Object System.Drawing.Bitmap(1536,1024)
    $g=[System.Drawing.Graphics]::FromImage($canvas)
    $g.Clear([System.Drawing.Color]::FromArgb(128,133,141))
    $views=@('left','right')
    for($i=0;$i -lt 2;$i++) {
        $im=[System.Drawing.Image]::FromFile("$root/$($character.key)/projection_source/$($views[$i]).png")
        $g.DrawImage($im,[int]($i*768),0,768,1024);$im.Dispose()
    }
    $canvas.Save("$root/$($character.key)/sides-source.png",[System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose();$canvas.Dispose()
}
