param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$dataPath = Join-Path $ProjectRoot 'app/Assets/EndoscopyTheme/Resources/ClinicalCourse/office-training-records.json'
$outputPath = Join-Path $ProjectRoot 'app/Assets/EndoscopyTheme/Resources/ClinicalCourse/FullScriptVisuals'
$data = Get-Content -LiteralPath $dataPath -Raw -Encoding UTF8 | ConvertFrom-Json
$fontFamily = 'Microsoft YaHei UI'

function Draw-Text {
    param(
        [System.Drawing.Graphics]$Graphics,
        [string]$Text,
        [single]$X,
        [single]$Y,
        [single]$Width,
        [single]$Height,
        [single]$Size,
        [System.Drawing.Color]$Color = [System.Drawing.Color]::FromArgb(34, 48, 63),
        [System.Drawing.FontStyle]$Style = [System.Drawing.FontStyle]::Regular,
        [System.Drawing.StringAlignment]$Alignment = [System.Drawing.StringAlignment]::Near
    )
    $font = [System.Drawing.Font]::new($fontFamily, $Size, $Style, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = [System.Drawing.SolidBrush]::new($Color)
    $format = [System.Drawing.StringFormat]::new()
    $format.Alignment = $Alignment
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $format.Trimming = [System.Drawing.StringTrimming]::EllipsisWord
    $format.FormatFlags = [System.Drawing.StringFormatFlags]::LineLimit
    $bounds = [System.Drawing.RectangleF]::new($X, $Y, $Width, $Height)
    $Graphics.DrawString($Text, $font, $brush, $bounds, $format)
    $format.Dispose()
    $brush.Dispose()
    $font.Dispose()
}

function Fill-Rectangle {
    param([System.Drawing.Graphics]$Graphics, [System.Drawing.Color]$Color,
        [single]$X, [single]$Y, [single]$Width, [single]$Height)
    $brush = [System.Drawing.SolidBrush]::new($Color)
    $Graphics.FillRectangle($brush, $X, $Y, $Width, $Height)
    $brush.Dispose()
}

function Draw-GridLine {
    param([System.Drawing.Graphics]$Graphics, [single]$X1, [single]$Y1, [single]$X2, [single]$Y2)
    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(177, 190, 202), 2)
    $Graphics.DrawLine($pen, $X1, $Y1, $X2, $Y2)
    $pen.Dispose()
}

function New-DocumentPage {
    $bitmap = [System.Drawing.Bitmap]::new(1536, 1024, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $graphics.Clear([System.Drawing.Color]::FromArgb(250, 250, 247))
    Fill-Rectangle $graphics ([System.Drawing.Color]::White) 34 34 1468 956
    Draw-GridLine $graphics 34 34 1502 34
    Draw-GridLine $graphics 34 990 1502 990
    return @{ Bitmap = $bitmap; Graphics = $graphics }
}

function Draw-Header {
    param([System.Drawing.Graphics]$Graphics, [string]$Title)
    Fill-Rectangle $Graphics ([System.Drawing.Color]::FromArgb(27, 110, 168)) 62 58 1412 142
    Draw-Text $Graphics $Title 92 76 960 58 38 ([System.Drawing.Color]::White) ([System.Drawing.FontStyle]::Bold)
    Draw-Text $Graphics '模拟训练资料 · 非医院真实记录' 94 138 1120 44 24 ([System.Drawing.Color]::FromArgb(226, 241, 250))
    Fill-Rectangle $Graphics ([System.Drawing.Color]::FromArgb(233, 243, 250)) 1218 88 220 52
    Draw-Text $Graphics '训练样例' 1218 88 220 52 22 ([System.Drawing.Color]::FromArgb(27, 93, 139)) ([System.Drawing.FontStyle]::Bold) ([System.Drawing.StringAlignment]::Center)
}

function Save-DocumentPage {
    param([hashtable]$Page, [string]$Name)
    $path = Join-Path $outputPath $Name
    $Page.Graphics.Dispose()
    $Page.Bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $Page.Bitmap.Dispose()
    Write-Output $path
}

function New-DisinfectionRecordImage {
    $page = New-DocumentPage
    $graphics = $page.Graphics
    $document = $data.documents | Where-Object id -eq 'disinfection' | Select-Object -First 1
    Draw-Header $graphics '清洗消毒记录'
    Draw-Text $graphics '模拟训练数据 · 字段与记录同源' 80 220 1360 42 24 ([System.Drawing.Color]::FromArgb(54, 73, 90))

    $columnWidths = @(190, 210, 250, 235, 235, 280)
    $headers = @($data.fields | ForEach-Object title)
    $tableX = 68
    $tableY = 292
    $headerHeight = 96
    $rowHeight = 115
    $tableWidth = ($columnWidths | Measure-Object -Sum).Sum
    Fill-Rectangle $graphics ([System.Drawing.Color]::FromArgb(33, 118, 185)) $tableX $tableY $tableWidth $headerHeight
    $x = $tableX
    for ($column = 0; $column -lt $columnWidths.Count; $column++) {
        Draw-Text $graphics $headers[$column] ($x + 8) ($tableY + 8) ($columnWidths[$column] - 16) ($headerHeight - 16) 22 ([System.Drawing.Color]::White) ([System.Drawing.FontStyle]::Bold) ([System.Drawing.StringAlignment]::Center)
        if ($column -gt 0) { Draw-GridLine $graphics $x $tableY $x ($tableY + $headerHeight + $rowHeight * $data.rows.Count) }
        $x += $columnWidths[$column]
    }
    for ($row = 0; $row -lt $data.rows.Count; $row++) {
        $y = $tableY + $headerHeight + $row * $rowHeight
        $rowColor = if (($row % 2) -eq 0) { [System.Drawing.Color]::White } else { [System.Drawing.Color]::FromArgb(239, 246, 251) }
        Fill-Rectangle $graphics $rowColor $tableX $y $tableWidth $rowHeight
        $x = $tableX
        for ($column = 0; $column -lt $columnWidths.Count; $column++) {
            $value = [string]$data.rows[$row].values[$column]
            if (-not [string]::IsNullOrWhiteSpace($value)) {
                Draw-Text $graphics $value ($x + 8) ($y + 8) ($columnWidths[$column] - 16) ($rowHeight - 16) 23 ([System.Drawing.Color]::FromArgb(31, 47, 62)) ([System.Drawing.FontStyle]::Regular) ([System.Drawing.StringAlignment]::Center)
            }
            $x += $columnWidths[$column]
        }
        Draw-GridLine $graphics $tableX $y ($tableX + $tableWidth) $y
    }
    $bottom = $tableY + $headerHeight + $rowHeight * $data.rows.Count
    Draw-GridLine $graphics $tableX $bottom ($tableX + $tableWidth) $bottom
    $x = $tableX
    foreach ($width in $columnWidths) { Draw-GridLine $graphics $x $tableY $x $bottom; $x += $width }
    Draw-GridLine $graphics ($tableX + $tableWidth) $tableY ($tableX + $tableWidth) $bottom

    Draw-Text $graphics $document.body 78 790 1380 162 22 ([System.Drawing.Color]::FromArgb(54, 73, 90))
    Save-DocumentPage $page 'office-disinfection-training-record-v1.png'
}

function New-StaffTrainingImage {
    $page = New-DocumentPage
    $graphics = $page.Graphics
    $document = $data.documents | Where-Object id -eq 'training' | Select-Object -First 1
    $lines = @($document.body -split "`n")
    $dateAndScope = [regex]::Match($lines[1], '^日期：(.+?)；范围：(.+?)。$')
    $content = [regex]::Match($lines[2], '^内容：(.+)$')
    $methodAndOwner = [regex]::Match($lines[3], '^方式：(.+?)；负责人：(.+)$')
    if (-not $dateAndScope.Success -or -not $content.Success -or -not $methodAndOwner.Success) {
        throw 'Training record text no longer matches the reviewed layout; update the document renderer with the source data.'
    }

    Draw-Header $graphics '人员培训记录'
    $fields = @(
        @('日期', $dateAndScope.Groups[1].Value),
        @('培训对象', $dateAndScope.Groups[2].Value),
        @('培训内容', $content.Groups[1].Value),
        @('培训方式', $methodAndOwner.Groups[1].Value),
        @('负责人', $methodAndOwner.Groups[2].Value)
    )
    $tableX = 92
    $tableY = 254
    $labelWidth = 252
    $valueWidth = 1100
    $rowHeights = @(88, 92, 170, 110, 90)
    $y = $tableY
    for ($row = 0; $row -lt $fields.Count; $row++) {
        $height = $rowHeights[$row]
        Fill-Rectangle $graphics ([System.Drawing.Color]::FromArgb(237, 244, 249)) $tableX $y $labelWidth $height
        Fill-Rectangle $graphics ([System.Drawing.Color]::White) ($tableX + $labelWidth) $y $valueWidth $height
        Draw-Text $graphics $fields[$row][0] ($tableX + 16) ($y + 6) ($labelWidth - 32) ($height - 12) 25 ([System.Drawing.Color]::FromArgb(36, 78, 112)) ([System.Drawing.FontStyle]::Bold)
        Draw-Text $graphics $fields[$row][1] ($tableX + $labelWidth + 20) ($y + 8) ($valueWidth - 40) ($height - 16) 25 ([System.Drawing.Color]::FromArgb(31, 47, 62))
        Draw-GridLine $graphics $tableX $y ($tableX + $labelWidth + $valueWidth) $y
        Draw-GridLine $graphics $tableX $y $tableX ($y + $height)
        Draw-GridLine $graphics ($tableX + $labelWidth) $y ($tableX + $labelWidth) ($y + $height)
        Draw-GridLine $graphics ($tableX + $labelWidth + $valueWidth) $y ($tableX + $labelWidth + $valueWidth) ($y + $height)
        $y += $height
    }
    Draw-GridLine $graphics $tableX $y ($tableX + $labelWidth + $valueWidth) $y
    Draw-Text $graphics $lines[4] 92 ($y + 25) 1350 78 21 ([System.Drawing.Color]::FromArgb(75, 85, 94))
    Save-DocumentPage $page 'office-staff-training-record-v1.png'
}

New-DisinfectionRecordImage
New-StaffTrainingImage
