param(
    [Parameter(Mandatory)][string]$NativeReferenceDirectory,
    [string]$SheetDirectory = (Join-Path $PSScriptRoot '../assets/characters/sheets'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../assets/characters/male'),
    [string]$Magick = 'C:\Program Files\ImageMagick-7.1.2-Q16-HDRI\magick.exe'
)
$ErrorActionPreference = 'Stop'
# Native reference textures are read only for frame dimensions/baselines; no native pixels enter the result.
$frames = Get-Content -LiteralPath (Join-Path $NativeReferenceDirectory 'sprites.json') -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$scratch = Join-Path $PSScriptRoot '../work/character-alpha'
New-Item -ItemType Directory -Force $scratch | Out-Null
$alpha = Join-Path $scratch 'alpha.png'
$inner = Join-Path $scratch 'inner.png'
foreach ($group in ($frames | Group-Object CostumeId)) {
    $sheet = Join-Path $SheetDirectory ($group.Name + '.png')
    if (!(Test-Path -LiteralPath $sheet)) { continue }
    $size = (& $Magick identify -format '%w %h' $sheet) -split ' '
    $cellWidth = [int]$size[0] / 3
    $cellHeight = [int]$size[1] / 3
    for ($i = 0; $i -lt $group.Group.Count; $i++) {
        $frame = $group.Group[$i]
        $native = Join-Path $NativeReferenceDirectory $frame.Texture
        $bounds = (& $Magick $native -trim -format '%h %Y' info:) -split ' '
        $height = [int]$bounds[0]
        $bottom = [int]$frame.Rect.height - [int]$bounds[1] - $height
        $x0 = [int][Math]::Round(($i % 3) * $cellWidth)
        $y0 = [int][Math]::Round([Math]::Floor($i / 3) * $cellHeight)
        $x1 = [int][Math]::Round((($i % 3) + 1) * $cellWidth)
        $y1 = [int][Math]::Round(([Math]::Floor($i / 3) + 1) * $cellHeight)
        $crop = '{0}x{1}+{2}+{3}' -f ($x1-$x0),($y1-$y0),$x0,$y0
        $canvas = '{0}x{1}' -f $frame.Rect.width,$frame.Rect.height
        $destination = Join-Path $OutputDirectory ($frame.CostumeId.ToString()+'.'+$frame.Label+'.png')
        # Flood from outside, preserving white areas enclosed by the costume outline.
        & $Magick $sheet -crop $crop +repage -alpha set -bordercolor white -border 1 -fuzz '6%' -fill none -draw 'alpha 0,0 floodfill' -shave 1x1 -trim +repage -resize "x$height" -background none -gravity South -splice "0x$bottom" -extent $canvas $destination
        if ($LASTEXITCODE -ne 0) { throw "Sprite conversion failed: $destination" }
        # Generated edges carry white matte color even where alpha is partial.
        # Preserve coverage and interior colors; give only the outer edge the dark native-style outline color.
        & $Magick $destination -alpha extract $alpha
        if ($LASTEXITCODE -ne 0) { throw "Alpha extraction failed: $destination" }
        & $Magick $alpha -threshold '99%' -morphology Erode Diamond:1 $inner
        if ($LASTEXITCODE -ne 0) { throw "Edge mask failed: $destination" }
        & $Magick -size $canvas xc:'#400908' '(' $destination -alpha off ')' $inner -compose Over -composite $alpha -alpha off -compose CopyOpacity -composite $destination
        if ($LASTEXITCODE -ne 0) { throw "Edge cleanup failed: $destination" }
    }
}
