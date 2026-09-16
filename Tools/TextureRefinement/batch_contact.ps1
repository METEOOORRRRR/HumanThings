param([string]$Stage='final')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root='D:/HumanThings/QA/CharacterBatch'
$characters=Get-Content "$root/characters.json" -Raw | ConvertFrom-Json
$canvas=New-Object System.Drawing.Bitmap(2000,1800)
$g=[System.Drawing.Graphics]::FromImage($canvas)
$g.Clear([System.Drawing.Color]::FromArgb(34,38,43))
$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$font=New-Object System.Drawing.Font('Arial',15)
for($row=0;$row -lt $characters.Count;$row++) {
    $character=$characters[$row]
    $views=@('front','face','threequarter','back','right')
    for($column=0;$column -lt $views.Count;$column++) {
        $path="$root/$($character.key)/$Stage/$($views[$column]).png"
        $im=[System.Drawing.Image]::FromFile($path)
        $height=324; $width=[int]($im.Width*$height/$im.Height)
        $g.DrawImage($im,[int]($column*400+(400-$width)/2),[int]($row*360+32),$width,$height)
        $g.DrawString("$($character.alias) / $($views[$column])",$font,[System.Drawing.Brushes]::White,[single]($column*400+10),[single]($row*360+5))
        $im.Dispose()
    }
}
$canvas.Save("$root/$Stage-contact.jpg",[System.Drawing.Imaging.ImageFormat]::Jpeg)
$g.Dispose();$canvas.Dispose();$font.Dispose()
