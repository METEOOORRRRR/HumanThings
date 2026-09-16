$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root='D:/HumanThings/QA/CharacterBatch'
$characters=Get-Content "$root/characters.json" -Raw | ConvertFrom-Json
$canvas=New-Object System.Drawing.Bitmap(1500,660)
$g=[System.Drawing.Graphics]::FromImage($canvas)
$g.Clear([System.Drawing.Color]::FromArgb(28,31,37))
$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$font=New-Object System.Drawing.Font('Arial',14)
for($i=0;$i -lt $characters.Count;$i++) {
    $c=$characters[$i]
    $g.DrawString("$($c.alias)  /  BEFORE",$font,[System.Drawing.Brushes]::White,[single]($i*300+10),[single]5)
    $g.DrawString("$($c.alias)  /  AFTER",$font,[System.Drawing.Brushes]::White,[single]($i*300+10),[single]335)
    foreach($stage in @('original','final')) {
        $im=[System.Drawing.Image]::FromFile("$root/$($c.key)/$stage/face.png")
        $y=30;if($stage -eq 'final'){$y=360}
        $g.DrawImage($im,[int]($i*300),$y,300,300);$im.Dispose()
    }
}
$canvas.Save("$root/faces-before-after.jpg",[System.Drawing.Imaging.ImageFormat]::Jpeg)
$g.Dispose();$canvas.Dispose();$font.Dispose()
