$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$output=Join-Path (Split-Path -Parent $PSScriptRoot) 'app/Assets/Endoscopy/Resources/StationMedia'
function Draw-Atlas([bool]$wrong) {
    $bitmap=[Drawing.Bitmap]::new(1600,900)
    $g=[Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint=[Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([Drawing.ColorTranslator]::FromHtml('#102A27'))
    $gold=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#D5BA7B'))
    $white=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#F0F2E8'))
    $muted=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#A7BFB5'))
    $tile=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#24453F'))
    $alert=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#F49B85'))
    $title=[Drawing.Font]::new('Microsoft YaHei',42,[Drawing.FontStyle]::Bold)
    $body=[Drawing.Font]::new('Microsoft YaHei',25)
    $small=[Drawing.Font]::new('Microsoft YaHei',20)
    $format=[Drawing.StringFormat]::new();$format.Alignment=[Drawing.StringAlignment]::Center;$format.LineAlignment=[Drawing.StringAlignment]::Center
    $heading=if($wrong){'02 / 找出错乱 · 选择修复方案'}else{'01 / 正确示范 · 布局与流程'}
    $g.DrawString($heading,$title,$white,60,45)
    $g.DrawString('清洗消毒室  ·  教学示意图（非现场照片）',$small,$gold,66,130)
    $tiles=if($wrong){@('门敞开','消化 / 呼吸共用设备','终末漂洗位缺失')}else{@('实体隔断完整 · 门关闭','消化 / 呼吸设备分别配置','五环节齐全 · 单向衔接')}
    for($i=0;$i -lt 3;$i++){
        $rect=[Drawing.RectangleF]::new(60+$i*500,220,480,160);$g.FillRectangle($tile,$rect)
        $g.DrawString($tiles[$i],$body, $(if($wrong){$alert}else{$white}),$rect,$format)
    }
    $g.DrawString($(if($wrong){'观察：缺位与回流，不能只靠改标牌解决。'}else{'顺着工位理解交接顺序，而不是只记住箭头。'}),$body,$white,66,435)
    $steps=@('清洗','漂洗','消毒',$(if($wrong){'缺位'}else{'终末漂洗'}),'干燥')
    for($i=0;$i -lt 5;$i++){
        $rect=[Drawing.RectangleF]::new(60+$i*304,535,265,110);$g.FillRectangle($tile,$rect)
        $g.DrawString($steps[$i],$body,$(if($wrong -and $i -eq 3){$alert}else{$white}),$rect,$format)
        if($i -lt 4){$g.DrawString('→',$body,$gold,326+$i*304,567)}
    }
    if($wrong){$g.DrawString('错误路线：消毒后返回前序槽位  ↶',$body,$alert,66,695)}
    else{$g.DrawString('分设核对：两类清洗槽 + 两类清洗消毒机，均分别配置。',$body,$muted,66,695)}
    $g.DrawString('依据：用户提供的场景 5 镜头一脚本。用于课程对照，不代替现场核验。',$small,$muted,66,810)
    $name=if($wrong){'layout_wrong.png'}else{'layout_correct.png'};$bitmap.Save((Join-Path $output $name),[Drawing.Imaging.ImageFormat]::Png)
    foreach($item in @($format,$title,$body,$small,$gold,$white,$muted,$tile,$alert,$g,$bitmap)){$item.Dispose()}
}
Draw-Atlas $false
Draw-Atlas $true
