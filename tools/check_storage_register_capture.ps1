param([string]$Directory = (Join-Path $PSScriptRoot '../artifacts/stationary-final'))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
foreach($name in @('storage-cabinet-side-register.png','storage-cabinet-register-expanded.png')) {
    $bitmap=[System.Drawing.Bitmap]::new((Join-Path $Directory $name))
    try {
        $blue=0; $paper=0
        for($y=0;$y -lt $bitmap.Height;$y+=8) {
            for($x=0;$x -lt $bitmap.Width;$x+=8) {
                $pixel=$bitmap.GetPixel($x,$y)
                if($pixel.R -lt 100 -and $pixel.B -gt $pixel.R*1.25 -and $pixel.G -gt $pixel.R*1.15){$blue++}
                if($pixel.R -gt 210 -and $pixel.G -gt 210 -and $pixel.B -gt 210){$paper++}
            }
        }
        Write-Output "$name blue-control samples=$blue paper samples=$paper"
        if($blue -lt 100 -or $paper -lt 100){throw "Register or action bar occluded in actual fixed-camera capture: $name"}
        if($name -eq 'storage-cabinet-register-expanded.png') {
            # Inside the upper button fills, outside their labels/borders. The
            # exhausted-cache ellipse loses these pixels; a rounded rectangle does not.
            foreach($sampleX in @(425,761)) {
                $pixel=$bitmap.GetPixel([int]($sampleX*$bitmap.Width/1440),[int](939*$bitmap.Height/1080))
                if($pixel.R -ge 100 -or $pixel.B -le $pixel.R*1.25){throw 'Expanded register button fill has collapsed into an ellipse.'}
            }
            Write-Output 'Expanded register button corner-fill checks passed.'
        }
    } finally {$bitmap.Dispose()}
}
