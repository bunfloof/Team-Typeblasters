$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$utf8 = [Text.UTF8Encoding]::new($false)
function Save($path,$text) { [IO.File]::WriteAllText((Join-Path $PWD $path),$text,$utf8) }
$scene=[IO.File]::ReadAllText((Join-Path $PWD 'Assets/Scenes/Prototype.unity')).Replace("`r`n","`n")
$blocks=@{}
foreach($m in [regex]::Matches($scene,'(?ms)^--- !u!\d+ &(\d+).*?(?=^--- !u!|\z)')) { $blocks[$m.Groups[1].Value]=$m.Value }
# Keep the normal game's managers/UI, but deactivate its entire physical map,
# fights and route arrows. Forest gameplay uses new independent encounters.
foreach($id in @($blocks.Keys)) {
    $b=$blocks[$id]
    if($b -match '(?m)^  m_Name: (Map|Fights|Route Guide)$') {
        $scene=$scene.Replace($b,$b.Replace('m_IsActive: 1','m_IsActive: 0'))
    }
    if($b.StartsWith('--- !u!108 ')) {
        $new=[regex]::Replace($b,'(?m)^  m_Intensity: .+$','  m_Intensity: 0.7')
        $new=[regex]::Replace($new,'(?m)^  m_Color: .+$','  m_Color: {r: 0.58, g: 0.68, b: 0.8, a: 1}')
        $scene=$scene.Replace($b,$new)
    }
}
$scene=$scene.Replace('  m_Fog: 0','  m_Fog: 1').Replace('  m_FogMode: 1','  m_FogMode: 2').Replace('  m_FogDensity: 0.01','  m_FogDensity: 0.008')
$scene=[regex]::Replace($scene,'(?m)^  m_FogColor: .+$','  m_FogColor: {r: 0.055, g: 0.085, b: 0.08, a: 1}')
$scene=$scene.Replace('  m_ClearFlags: 1','  m_ClearFlags: 2')
$scene=[regex]::Replace($scene,'(?m)^  m_BackGroundColor: .+$','  m_BackGroundColor: {r: 0.055, g: 0.085, b: 0.08, a: 1}')
$scene=[regex]::Replace($scene,'(?m)^  m_AmbientSkyColor: .+$','  m_AmbientSkyColor: {r: 0.25, g: 0.32, b: 0.34, a: 1}')
$scene=$scene.Replace('m_text: The Typeblaster of the Dead','m_text: HARD - The Hollow Forest')
$scene=$scene.Replace('Type the word above a zombie to shoot it. Every letter is a bullet.','HARD FOREST: type EXACT case and punctuation. Follow the forest trail. Purple enemies change words while idle. Enemies move 30% faster.')
$scene=$scene.Replace('  spawnInterval: 2','  spawnInterval: 1.6').Replace('  runnerFirstWave: 3','  runnerFirstWave: 2').Replace('  armoredFirstWave: 3','  armoredFirstWave: 2').Replace('  bossName: SUBJECT ZERO','  bossName: THE HOLLOW WARDEN')
$materials=@{
    ForestEarth=@('0711525c90ef4cd7aac565201974b102','0.14, g: 0.13, b: 0.105');
    ForestTrail=@('0711525c90ef4cd7aac565201974b103','0.24, g: 0.215, b: 0.165');
    ForestBark=@('0711525c90ef4cd7aac565201974b104','0.16, g: 0.12, b: 0.09');
    ForestLeaves=@('0711525c90ef4cd7aac565201974b105','0.07, g: 0.16, b: 0.115');
    ForestMoss=@('0711525c90ef4cd7aac565201974b106','0.13, g: 0.22, b: 0.145');
    ForestStone=@('0711525c90ef4cd7aac565201974b107','0.22, g: 0.255, b: 0.245');
    ForestWater=@('0711525c90ef4cd7aac565201974b108','0.06, g: 0.14, b: 0.155');
    ForestLantern=@('0711525c90ef4cd7aac565201974b109','0.9, g: 0.55, b: 0.18')
}
$template=[IO.File]::ReadAllText((Join-Path $PWD 'Assets/Materials/Map/Building.mat'))
foreach($name in $materials.Keys) {
    $entry=$materials[$name]; $text=$template.Replace('m_Name: Building',"m_Name: $name")
    $text=[regex]::Replace($text,'(?m)(- _(?:BaseColor|Color): )\{[^}]+\}',"`${1}{r: $($entry[1]), a: 1}")
    if($name -eq 'ForestLantern') {
        $text=$text.Replace('m_ValidKeywords: []',"m_ValidKeywords:`n  - _EMISSION")
        $text=[regex]::Replace($text,'(?m)(- _EmissionColor: )\{[^}]+\}','${1}{r: 1.5, g: 0.65, b: 0.12, a: 1}')
    }
    Save "Assets/Materials/Map/$name.mat" $text
    Save "Assets/Materials/Map/$name.mat.meta" "fileFormatVersion: 2`nguid: $($entry[0])`nNativeFormatImporter:`n  externalObjects: {}`n  mainObjectFileID: 2100000`n"
}
$records=[Collections.Generic.List[string]]::new(); $nodes=@{}; $next=3100000000L
function Node($name,$position='x: 0, y: 0, z: 0',$scale='x: 1, y: 1, z: 1',$parent=0,$yaw=0) {
    $base=$script:next; $script:next+=10
    $qY=[Math]::Sin($yaw*[Math]::PI/360); $qW=[Math]::Cos($yaw*[Math]::PI/360)
    $node=@{ go=$base; transform=$base+1; component=$base+2; children=[Collections.Generic.List[string]]::new(); components=[Collections.Generic.List[string]]::new() }
    $node.components.Add("  - component: {fileID: $($base+1)}")
    $script:nodes[($base+1).ToString()]=$node
    if($parent -ne 0) { $script:nodes[$parent.ToString()].children.Add("  - {fileID: $($base+1)}") }
    $script:records.Add(@"
--- !u!1 &$base
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
COMPONENTS_$base
  m_Layer: 0
  m_Name: $name
  m_TagString: Untagged
  m_IsActive: 1
--- !u!4 &$($base+1)
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: $base}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: $qY, z: 0, w: $qW}
  m_LocalPosition: {$position}
  m_LocalScale: {$scale}
CHILDREN_$base
  m_Father: {fileID: $parent}
"@)
    return $node
}
function Script($node,$guid,$fields) {
    $node.components.Add("  - component: {fileID: $($node.component)}")
    $script:records.Add(@"
--- !u!114 &$($node.component)
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: $($node.go)}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: $guid, type: 3}
  m_Name:
  m_EditorClassIdentifier:
$fields
"@)
}
function Shape($name,$position,$scale,$surface,$parent,$mesh=10202,$yaw=0,$collider=$false) {
    $node=Node $name $position $scale $parent $yaw
    $render=$blocks['342607414'].Replace('342607414',($node.go+3).ToString()).Replace('342607412',$node.go.ToString()).Replace('711cdfd5dba1a774da5183b41e276c68',$materials[$surface][0])
    $filter=$blocks['342607416'].Replace('342607416',($node.go+4).ToString()).Replace('342607412',$node.go.ToString()).Replace('m_Mesh: {fileID: 10202,',"m_Mesh: {fileID: $mesh,")
    $node.components.Add("  - component: {fileID: $($node.go+3)}"); $node.components.Add("  - component: {fileID: $($node.go+4)}")
    $script:records.Add($render); $script:records.Add($filter)
    if($collider) {
        $node.components.Add("  - component: {fileID: $($node.go+5)}")
        $script:records.Add($blocks['342607415'].Replace('342607415',($node.go+5).ToString()).Replace('342607412',$node.go.ToString()))
    }
    return $node
}
$root=Node 'Hard - The Hollow Forest'
Script $root 'ac41dbe137fe41969c6c9d139da4d261' "  enemySpeedMultiplier: 1.3`n  wordChangeSeconds: 4.5"
$land=Node 'Forest - Terrain and Trails' -parent $root.transform
$trees=Node 'Forest - Trees and Underbrush' -parent $root.transform
$fights=Node 'Forest - New Encounters' -parent $root.transform
$props=Node 'Forest - Landmarks and Lanterns' -parent $root.transform
[void](Shape 'Forest Floor' 'x: 0, y: -0.3, z: 15' 'x: 240, y: 0.6, z: 270' 'ForestEarth' $land.transform -collider $true)
$start=Node 'Forest Entry' 'x: -75, y: 0, z: -75' -parent $root.transform
$plans=@(
    @{name='Fight 1 - Lantern Clearing'; stop=@(-60,-40); yaw=0; route=@(@(-70,-68),@(-65,-58),@(-60,-48)); boss=$false},
    @{name='Fight 2 - Crooked Grove'; stop=@(-15,10); yaw=90; route=@(@(-50,-40),@(-40,-34),@(-34,-18),@(-25,0)); boss=$false},
    @{name='Fight 3 - Deadwood Hollow'; stop=@(45,-15); yaw=0; route=@(@(-5,10),@(15,7),@(28,-2),@(40,-15)); boss=$false},
    @{name='Fight 4 - Warden Altar'; stop=@(25,65); yaw=0; route=@(@(62,-5),@(70,15),@(62,38),@(42,53)); boss=$true}
)
$segments=[Collections.Generic.List[object]]::new(); $encounters=@(); $from=@(-75,-75)
foreach($plan in $plans) {
    $fight=Node $plan.name -parent $fights.transform
    $route=Node 'Winding Trail' -parent $fight.transform
    foreach($p in $plan.route) {
        [void](Node 'Trail Bend' "x: $($p[0]), y: 0, z: $($p[1])" -parent $route.transform)
        $segments.Add(@($from,$p)); $from=$p
    }
    $p=$plan.stop; $segments.Add(@($from,$p)); $from=$p
    $stop=Node 'Forest Battle Stop' "x: $($p[0]), y: 0, z: $($p[1])" -parent $fight.transform -yaw $plan.yaw
    $angle=$plan.yaw*[Math]::PI/180; $fx=[Math]::Sin($angle); $fz=[Math]::Cos($angle); $rx=$fz; $rz=-$fx
    $plan.fx=$fx; $plan.fz=$fz; $plan.rx=$rx; $plan.rz=$rz
    $bossStand=Node 'Warden Stand' "x: $($p[0]+$fx*27), y: 0, z: $($p[1]+$fz*27)" -parent $fight.transform
    foreach($offset in -12,0,12) {
        $distance=32-[Math]::Abs($offset)/3
        $sx=$p[0]+$fx*$distance+$rx*$offset; $sz=$p[1]+$fz*$distance+$rz*$offset
        $spawn=Node 'From the Tree Line' "x: $sx, y: 0, z: $sz" -parent $fight.transform
        $exit=Node 'Forest Approach' "x: $($sx-$fx*3), y: 0, z: $($sz-$fz*3)" -parent $fight.transform
        Script $spawn '45c6fbc59f09d6d47a56890fea2d098f' "  exit: {fileID: $($exit.transform)}`n  door: {fileID: 0}`n  spread: 0.5"
        $segments.Add(@(@($sx,$sz),@($p[0],$p[1])))
    }
    Script $fight '2043d301c960d2844a39bdc259577d48' "  route: {fileID: $($route.transform)}`n  stop: {fileID: $($stop.transform)}`n  groupSize: 3`n  exitGate: {fileID: 0}`n  isBossFight: $([int]$plan.boss)`n  bossStand: {fileID: $($bossStand.transform)}"
    $encounters+="  - {fileID: $($fight.component)}"
    # Supply rewards stay in the player's field of view, off the attack lanes.
    foreach($kind in 0,1,2) {
        $cx=$p[0]+$fx*16+$rx*($kind*5-5); $cz=$p[1]+$fz*16+$rz*($kind*5-5)
        $crate=Node "Forest Supply $kind" "x: $cx, y: 0, z: $cz" -parent $fight.transform
        $surface=@('ForestStone','ForestLantern','ForestWater')[$kind]
        $cube=Shape 'Supply Box' 'x: 0, y: 0.4, z: 0' 'x: 0.8, y: 0.8, z: 0.8' $surface $crate.transform -collider $true
        Script $crate '26ae6f75199a80343ac32b57df8601c5' "  kind: $kind`n  cube: {fileID: $($cube.go)}"
    }
}
# Trail paving follows the new path; flat patches overlap at turns.
$walking=[Collections.Generic.List[object]]::new(); $from=@(-75,-75)
foreach($plan in $plans) { foreach($p in (@($plan.route)+@(,$plan.stop))) { $walking.Add(@($from,$p)); $from=$p } }
foreach($segment in $walking) {
    $a=$segment[0]; $b=$segment[1]; $dx=$b[0]-$a[0]; $dz=$b[1]-$a[1]; $length=[Math]::Sqrt($dx*$dx+$dz*$dz)
    $yaw=[Math]::Atan2($dx,$dz)*180/[Math]::PI
    [void](Shape 'Forest Dirt Trail' "x: $(($a[0]+$b[0])/2), y: 0.015, z: $(($a[1]+$b[1])/2)" "x: 6, y: 0.025, z: $($length+3)" 'ForestTrail' $land.transform -yaw $yaw)
    [void](Shape 'Trail Lantern Post' "x: $($b[0]+4), y: 1.1, z: $($b[1])" 'x: 0.15, y: 2.2, z: 0.15' 'ForestBark' $props.transform)
    [void](Shape 'Trail Lantern Glow' "x: $($b[0]+4), y: 2.1, z: $($b[1])" 'x: 0.4, y: 0.5, z: 0.4' 'ForestLantern' $props.transform)
}
function Clearance($x,$z) {
    $distance=[double]::PositiveInfinity
    foreach($segment in $segments) {
        $a=$segment[0]; $b=$segment[1]; $dx=$b[0]-$a[0]; $dz=$b[1]-$a[1]; $den=$dx*$dx+$dz*$dz
        $t=if($den -gt 0){[Math]::Clamp((($x-$a[0])*$dx+($z-$a[1])*$dz)/$den,0,1)}else{0}
        $ex=$x-$a[0]-$dx*$t; $ez=$z-$a[1]-$dz*$t
        $distance=[Math]::Min($distance,[Math]::Sqrt($ex*$ex+$ez*$ez))
    }
    foreach($plan in $plans) {
        $dx=$x-$plan.stop[0]; $dz=$z-$plan.stop[1]; $along=$dx*$plan.fx+$dz*$plan.fz; $side=$dx*$plan.rx+$dz*$plan.rz
        if([Math]::Abs($side) -lt 23 -and $along -gt -10 -and $along -lt 45) { return 0 }
    }
    return $distance
}
$rng=[Random]::new(526); $treeCount=0; $minimumClearance=1000
for($x=-110; $x -le 110; $x+=7) { for($z=-110; $z -le 135; $z+=7) {
    $tx=$x+$rng.NextDouble()*4-2; $tz=$z+$rng.NextDouble()*4-2
    if([Math]::Pow(($tx+72)/15,2)+[Math]::Pow(($tz-50)/11,2) -lt 1) { continue }
    $distance=Clearance $tx $tz
    # Reserve room for rounded route corners as well as foliage and trunks.
    if($distance -lt 10) { continue }
    $minimumClearance=[Math]::Min($minimumClearance,$distance)
    $treeCount++; $height=9+$rng.NextDouble()*7
    $tree=Node "Forest Tree $treeCount" "x: $tx, y: 0, z: $tz" -parent $trees.transform
    [void](Shape 'Rough Trunk' "x: 0, y: $($height/2), z: 0" "x: 0.8, y: $($height/2), z: 0.8" 'ForestBark' $tree.transform 10206 -collider $true)
    if($tx -gt 24 -and $tx -lt 80 -and $tz -lt 30 -and $treeCount%3 -eq 0) {
        [void](Shape 'Deadwood Branch' "x: 0, y: $($height*0.6), z: 0" 'x: 5, y: 0.3, z: 0.35' 'ForestBark' $tree.transform -yaw 35)
    } else {
        foreach($layer in 0,1,2) {
            $width=6-$layer*1.4
            $surface=if($treeCount%3 -eq 0){'ForestMoss'}else{'ForestLeaves'}
            [void](Shape 'Dark Canopy' "x: 0, y: $($height-3+$layer*2), z: 0" "x: $width, y: 4.5, z: $width" $surface $tree.transform 10207)
        }
    }
    if($treeCount%6 -eq 0) { [void](Shape 'Mossy Boulder' "x: $($tx+1.5), y: 0.8, z: $($tz+1)" 'x: 3, y: 2, z: 2.5' 'ForestStone' $trees.transform 10207) }
} }
[void](Shape 'Still Forest Pond' 'x: -72, y: 0.02, z: 50' 'x: 26, y: 0.03, z: 18' 'ForestWater' $land.transform 10206)
[void](Shape 'Ancient Altar Floor' 'x: 25, y: 0.03, z: 91' 'x: 19, y: 0.06, z: 16' 'ForestStone' $props.transform 10206)
foreach($p in @(@(6,95),@(44,95),@(9,109),@(41,109))) {
    [void](Shape 'Warden Standing Stone' "x: $($p[0]), y: 3.5, z: $($p[1])" 'x: 2.5, y: 7, z: 2' 'ForestStone' $props.transform -yaw 15 -collider $true)
}
$level=$blocks['1968236269']; $newLevel=$level.Replace('start: {fileID: 170364582}',"start: {fileID: $($start.transform)}")
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
# Validate references before replacing the actual scene.
$ids=[Collections.Generic.HashSet[string]]::new()
foreach($m in [regex]::Matches($scene,'(?m)^--- !u!\d+ &(\d+)')) { if(!$ids.Add($m.Groups[1].Value)){throw 'Duplicate scene ID'} }
foreach($m in [regex]::Matches($scene,'\{fileID: (\d+)\}')) { if($m.Groups[1].Value -ne '0' -and !$ids.Contains($m.Groups[1].Value)){throw "Missing reference $($m.Value)"} }
Save 'Assets/Scenes/Hard.unity' $scene
Save 'Assets/Scenes/Hard.unity.meta' "fileFormatVersion: 2`nguid: fe2b67c2cc324157a631583bdc66f193`n"
Write-Output "Forest created: $treeCount trees, $($walking.Count) new trail segments, 4 encounters, 12 spawn points, 12 supplies. Minimum tree distance from route/approach lines: $([Math]::Round($minimumClearance,2)) m."
