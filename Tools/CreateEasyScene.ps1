$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$utf8 = [Text.UTF8Encoding]::new($false)
function Save($path,$text) { [IO.File]::WriteAllText((Join-Path $PWD $path),$text,$utf8) }
$scene=[IO.File]::ReadAllText((Join-Path $PWD 'Assets/Scenes/Normal.unity')).Replace("`r`n","`n")
$blocks=@{}
foreach($m in [regex]::Matches($scene,'(?ms)^--- !u!\d+ &(\d+).*?(?=^--- !u!|\z)')) { $blocks[$m.Groups[1].Value]=$m.Value }
foreach($b in @($blocks.Values)) {
    if($b -match '(?m)^  m_Name: (Map|Fights|Route Guide)$') {
        $scene=$scene.Replace($b,$b.Replace('m_IsActive: 1','m_IsActive: 0'))
    }
}
$scene=$scene.Replace('bossName: SUBJECT ZERO','bossName: TRAINING SENTINEL')
$scene=$scene.Replace('m_text: USC Campus','m_text: Training Academy').Replace('m_text: Wave 1 / 5','m_text: Wave 1 / 6')
$scene=[regex]::Replace($scene,'(?m)^  mapViewWaves:.*$','  mapViewWaves: ')
$scene=$scene.Replace('  labelTopMargin: 150','  labelTopMargin: 300').Replace('  bossLabelTopMargin: 235','  bossLabelTopMargin: 435')
$materials=@{
    TrainingFloor=@('6c0cb30228ee4677aa604e291be00b01','0.075, g: 0.10, b: 0.12');
    TrainingPath=@('6c0cb30228ee4677aa604e291be00b02','0.10, g: 0.23, b: 0.25');
    TrainingWall=@('6c0cb30228ee4677aa604e291be00b03','0.28, g: 0.34, b: 0.36');
    TrainingOrange=@('6c0cb30228ee4677aa604e291be00b04','0.30, g: 0.20, b: 0.12');
    TrainingBlue=@('6c0cb30228ee4677aa604e291be00b05','0.12, g: 0.20, b: 0.30');
    TrainingPurple=@('6c0cb30228ee4677aa604e291be00b06','0.23, g: 0.16, b: 0.28');
    TrainingGreen=@('6c0cb30228ee4677aa604e291be00b07','0.12, g: 0.24, b: 0.19')
}
$template=[IO.File]::ReadAllText((Join-Path $PWD 'Assets/Materials/Map/Building.mat'))
foreach($name in $materials.Keys) {
    $entry=$materials[$name]; $text=$template.Replace('m_Name: Building',"m_Name: $name")
    $text=[regex]::Replace($text,'(?m)(- _(?:BaseColor|Color): )\{[^}]+\}',"`${1}{r: $($entry[1]), a: 1}")
    $text=[regex]::Replace($text,'(?m)[ \t]+(?=\r?$)','')
    Save "Assets/Materials/Map/$name.mat" $text
    Save "Assets/Materials/Map/$name.mat.meta" "fileFormatVersion: 2`nguid: $($entry[0])`nNativeFormatImporter:`n  externalObjects: {}`n  mainObjectFileID: 2100000`n"
}
# Reuse only the YAML node/mesh writers; do not run the forest generator or touch Hard.
$forest=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'CreateForestScene.ps1'))
$writerStart=$forest.IndexOf('$records=')
$writerEnd=$forest.IndexOf('$root=Node')
Invoke-Expression $forest.Substring($writerStart,$writerEnd-$writerStart)
$script:next=3300000000L
$root=Node 'Easy - Training Academy'
Script $root '50d9298a3d3e4f07b6ac41d0aa839285' ''
$land=Node 'Academy - Open Training Courts' -parent $root.transform
$fights=Node 'Academy - Six Lessons' -parent $root.transform
[void](Shape 'Academy Foundation' 'x: 0, y: -0.3, z: 0' 'x: 240, y: 0.6, z: 210' 'TrainingFloor' $land.transform -collider $true)
$start=Node 'Academy Entrance' 'x: -100, y: 0, z: -85' -parent $root.transform
$plans=@(
    @{name='01 - Bomb Range'; stop=@(-80,-60); route=@(,@(-80,-85)); color='TrainingOrange'; boss=$false},
    @{name='02 - Freeze Lab'; stop=@(-10,-60); route=@(@(-80,-75),@(-10,-75)); color='TrainingBlue'; boss=$false},
    @{name='03 - Frenzy Hall'; stop=@(60,-60); route=@(@(-10,-75),@(60,-75)); color='TrainingPurple'; boss=$false},
    @{name='04 - Rocket Yard'; stop=@(60,30); route=@(@(95,-60),@(95,15),@(60,15)); color='TrainingOrange'; boss=$false},
    @{name='05 - Shield Court'; stop=@(-10,30); route=@(@(60,15),@(-10,15)); color='TrainingGreen'; boss=$false},
    @{name='06 - Graduation Arena'; stop=@(-80,30); route=@(@(-10,15),@(-80,15)); color='TrainingPurple'; boss=$true}
)
$encounters=@(); $from=@(-100,-85)
foreach($plan in $plans) {
    $fight=Node $plan.name -parent $fights.transform
    $route=Node 'Academy Route' -parent $fight.transform
    foreach($p in $plan.route) { [void](Node 'Route Bend' "x: $($p[0]), y: 0, z: $($p[1])" -parent $route.transform) }
    foreach($p in (@($plan.route)+@(,$plan.stop))) {
        $dx=$p[0]-$from[0]; $dz=$p[1]-$from[1]; $length=[Math]::Sqrt($dx*$dx+$dz*$dz)
        if($length -gt 0.1) {
            $yaw=[Math]::Atan2($dx,$dz)*180/[Math]::PI
            [void](Shape 'Cyan Academy Walkway' "x: $(($p[0]+$from[0])/2), y: 0.02, z: $(($p[1]+$from[1])/2)" "x: 5, y: 0.04, z: $($length+3)" 'TrainingPath' $land.transform -yaw $yaw)
        }
        $from=$p
    }
    $p=$plan.stop
    $stop=Node 'Academy Stop' "x: $($p[0]), y: 0, z: $($p[1])" -parent $fight.transform
    $bossStand=Node 'Sentinel Stand' "x: $($p[0]), y: 0, z: $($p[1]+27)" -parent $fight.transform
    foreach($offset in -8,0,8) {
        $spawn=Node 'Training Deployment' "x: $($p[0]+$offset), y: 0, z: $($p[1]+30)" -parent $fight.transform
        $exit=Node 'Clear Training Approach' "x: $($p[0]+$offset), y: 0, z: $($p[1]+27)" -parent $fight.transform
        Script $spawn '45c6fbc59f09d6d47a56890fea2d098f' "  exit: {fileID: $($exit.transform)}`n  door: {fileID: 0}`n  spread: 0.3"
    }
    Script $fight '2043d301c960d2844a39bdc259577d48' "  route: {fileID: $($route.transform)}`n  stop: {fileID: $($stop.transform)}`n  groupSize: 1`n  exitGate: {fileID: 0}`n  isBossFight: $([int]$plan.boss)`n  bossStand: {fileID: $($bossStand.transform)}"
    $encounters+="  - {fileID: $($fight.component)}"
    [void](Shape 'Colored Training Court' "x: $($p[0]), y: 0.04, z: $($p[1]+15)" 'x: 40, y: 0.04, z: 40' $plan.color $land.transform)
    # Tall color-coded pylons and backstops stay outside both spawn-to-stop lanes and routes.
    foreach($side in -1,1) {
        [void](Shape 'Academy Pylon' "x: $($p[0]+$side*22), y: 5, z: $($p[1]+22)" 'x: 2, y: 10, z: 2' 'TrainingWall' $land.transform -collider $true)
        [void](Shape 'Lesson Color Marker' "x: $($p[0]+$side*22), y: 10.5, z: $($p[1]+22)" 'x: 3, y: 1, z: 3' $plan.color $land.transform)
    }
    [void](Shape 'Academy Backstop' "x: $($p[0]), y: 3, z: $($p[1]+39)" 'x: 42, y: 6, z: 1' 'TrainingWall' $land.transform -collider $true)
    foreach($stripe in -12,-6,0,6,12) {
        [void](Shape 'Backstop Color Stripe' "x: $($p[0]+$stripe), y: 3, z: $($p[1]+38.4)" 'x: 0.7, y: 5, z: 0.1' $plan.color $land.transform)
    }
}
$level=$blocks['1968236269']
$newLevel=[regex]::Replace($level,'start: \{fileID: \d+\}',"start: {fileID: $($start.transform)}")
$newLevel=[regex]::Replace($newLevel,'(?ms)  encounters:\n(?:  - \{fileID: \d+\}\n)+',("  encounters:`n"+($encounters -join "`n")+"`n"))
$scene=$scene.Replace($level,$newLevel)
$extra=$records -join "`n"
$extra=[regex]::Replace($extra,'(COMPONENTS|CHILDREN)_(\d+)',[Text.RegularExpressions.MatchEvaluator]{
    param($match)
    $node=$nodes[([long]$match.Groups[2].Value+1).ToString()]
    if($match.Groups[1].Value -eq 'COMPONENTS') { return ($node.components -join "`n") }
    if($node.children.Count -gt 0) { return "  m_Children:`n"+($node.children -join "`n") }
    return '  m_Children: []'
})
$scene=$scene.Replace('--- !u!1660057539 &9223372036854775807',($extra+"`n--- !u!1660057539 &9223372036854775807"))
$scene+="  - {fileID: $($root.transform)}`n"
$ids=[Collections.Generic.HashSet[string]]::new()
foreach($m in [regex]::Matches($scene,'(?m)^--- !u!\d+ &(\d+)')) { if(!$ids.Add($m.Groups[1].Value)){throw "Duplicate scene ID $($m.Groups[1].Value)"} }
foreach($m in [regex]::Matches($scene,'\{fileID: (\d+)\}')) { if($m.Groups[1].Value -ne '0' -and !$ids.Contains($m.Groups[1].Value)){throw "Missing reference $($m.Value)"} }
$scene=[regex]::Replace($scene,'(?m)[ \t]+$','')
Save 'Assets/Scenes/Easy.unity' $scene
Write-Output 'Easy created: six independent academy stations, 18 spawn points, U-shaped return route. Normal and Hard unchanged.'
