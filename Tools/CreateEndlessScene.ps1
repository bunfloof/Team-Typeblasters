# CreateEndlessScene.ps1
# ---------------------------------------------------------------------------
# Builds the ENDLESS arena: "The Coliseum", a greybox stadium modelled on the
# LA Memorial Coliseum (compressed so the whole fight fits in the view), in the
# same plain-box style and with the same five materials as the campus map
# (Building, Ground, Lawn, Neighbour, Pavement).
#
#   powershell -ExecutionPolicy Bypass -File Tools/CreateEndlessScene.ps1
#
# It edits Assets/Scenes/Endless.unity in place and can be run again: every
# object it made last time (IDs 3200000000+) is removed first. Endless's own
# settings (endlessMode, caps, the Atmosphere object) are kept. The campus map,
# its fights and route arrows are switched off (not deleted).
#
# Layout (x = east, z = north, around ArenaCentre):
#   - the player stands on the west half of the field, facing EAST;
#   - an oval bowl of six stepped stand tiers, the peristyle (colonnade with
#     the torch) behind the east end, four light towers;
#   - six gates (a door in a small gatehouse in front of the stands), the zombie spawn points, all
#     within 33 degrees of the facing so they stay on screen:
#       Stage 1  front  (+-6 deg, far lanes)
#       Stage 2  front-left / front-right  (+-20 deg)
#       Stage 3  left / right  (+-32 deg, near lanes)
#     (named by stage so later waves can open them one stage at a time);
#   - three barrels and three supply crates scattered near the lanes (seeded),
#     and a boss stand in mid-field (for a boss every 10 waves).
# ---------------------------------------------------------------------------
$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$utf8 = New-Object Text.UTF8Encoding($false)
function Save($path,$text) { [IO.File]::WriteAllText((Join-Path $PWD $path),$text,$utf8) }
function Clamp([double]$v,[double]$lo,[double]$hi) { [Math]::Max($lo,[Math]::Min($hi,$v)) }
function F($v) { ([double]$v).ToString('0.###',[Globalization.CultureInfo]::InvariantCulture) }

$scenePath = 'Assets/Scenes/Endless.unity'
$scene = [IO.File]::ReadAllText((Join-Path $PWD $scenePath)).Replace("`r`n","`n")

# ---- 1. Remove what an earlier run made (IDs 3200000000 .. 3299999999) ----
$GenMin = 3200000000L; $GenMax = 3299999999L
function IsGen($id) { $n = [long]$id; return ($n -ge $GenMin -and $n -le $GenMax) }
$scene = [regex]::Replace($scene, '(?ms)^--- !u!\d+ &(\d+)( stripped)?\n.*?(?=^--- !u!|\z)', [Text.RegularExpressions.MatchEvaluator]{
    param($m) if (IsGen $m.Groups[1].Value) { return '' } return $m.Value })
$scene = [regex]::Replace($scene, '(?m)^  - \{fileID: (\d+)\}\n', [Text.RegularExpressions.MatchEvaluator]{
    param($m) if (IsGen $m.Groups[1].Value) { return '' } return $m.Value })

$blocks = @{}
foreach ($m in [regex]::Matches($scene,'(?ms)^--- !u!\d+ &(\d+).*?(?=^--- !u!|\z)')) { $blocks[$m.Groups[1].Value] = $m.Value }

# ---- 2. Switch off the campus map, its fights and its route arrows ----
foreach ($id in @($blocks.Keys)) {
    $b = $blocks[$id]
    if ($b -match '(?m)^  m_Name: (Map|Fights|Route Guide)$') {
        $scene = $scene.Replace($b, $b.Replace('m_IsActive: 1','m_IsActive: 0'))
    }
}
$scene = $scene.Replace('m_text: Endless Arena','m_text: The Coliseum')

# The view: THIRD PERSON. The camera's local position is the player's eye
# (what energy orbs aim at, as on the campus); CameraDirector's Third Person
# Offset puts the camera behind and above it, tilted down.
$EyeHeightValue = '1.7'                          # metres, the campus value
$ThirdPersonOffset = '{x: 0, y: 3.5, z: -6.5}'   # behind and above the eye: the camera is about 5.2 m up
$ThirdPersonPitchDown = '12'                     # degrees
$directorBlock = [regex]::Match($scene,'(?ms)^--- !u!114 &\d+\n(?:(?!^--- ).)*?m_Script: \{fileID: 11500000, guid: 4eea2cf469e94b143b7514bcd8700de2, type: 3\}.*?(?=^--- )').Value
if (-not $directorBlock) { throw 'No CameraDirector in the scene' }
$newDirector = [regex]::Replace($directorBlock,'(?m)^  thirdPerson(Offset|PitchDown): .*\n','')
$newDirector = [regex]::Replace($newDirector,'(?m)^(  m_EditorClassIdentifier: .*\n)',"`${1}  thirdPersonOffset: $ThirdPersonOffset`n  thirdPersonPitchDown: $ThirdPersonPitchDown`n")
$scene = $scene.Replace($directorBlock,$newDirector)
# ENDLESS PACE: short, light waves that grow slowly (a run should still be
# fun at wave 10, not tiring at wave 3). Wave n has 3 + n zombies (at most 14),
# each special kind comes in its own wave (one new thing per wave), then one
# more of it every 5 waves (WaveSpawner.SpecialCount); wave 10, 20... is a boss.
#   2 runner   3 armored   4 splitter   5 rear rush   6 sniper
#   7 red (self-destructing: not in the easy first waves)   8 ghost
#   9 spitter   10 BOSS   11 brute   12 word chain
$pace = [ordered]@{
    zombiesBase = '3'; zombiesPerWave = '1'; spawnInterval = '1.8'; spawnIntervalPerWave = '0.05';
    zombieSpeed = '1.5'; zombieSpeedPerWave = '0.06'
    runnerFirstWave = '2'; armoredFirstWave = '3'; splitterFirstWave = '4'; explosiveFirstWave = '7'
    ghostFirstWave = '8'; spitterFirstWave = '9'; bruteFirstWave = '11'; chainFirstWave = '12'
    endlessDifficultyCapWave = '20'; endlessMaxBaseZombiesPerWave = '14'; endlessMaxChainPairs = '2'
    maxOnScreen = '4'
}
$spawnerBlock = [regex]::Match($scene,'(?ms)^--- !u!114 &\d+\n(?:(?!^--- ).)*?m_EditorClassIdentifier: Assembly-CSharp::WaveSpawner\n.*?(?=^--- )').Value
if (-not $spawnerBlock) { throw 'No WaveSpawner in the scene' }
$newSpawner = $spawnerBlock
foreach ($key in $pace.Keys) {
    if ($newSpawner -notmatch "(?m)^  $($key): ") { throw "WaveSpawner has no $key" }
    $newSpawner = [regex]::Replace($newSpawner,"(?m)^  $($key): .*$","  $($key): $($pace[$key])")
}
$scene = $scene.Replace($spawnerBlock,$newSpawner)
$cameraGo = [regex]::Match($scene,'(?ms)^--- !u!1 &(\d+)\n(?:(?!^--- ).)*?\n  m_Name: Main Camera\n').Groups[1].Value
if (-not $cameraGo) { throw 'No Main Camera in the scene' }
$cameraTf = [regex]::Match($scene,"(?ms)^--- !u!4 &(\d+)\n(?:(?!^--- ).)*?m_GameObject: \{fileID: $cameraGo\}").Groups[1].Value
$cameraBlock = [regex]::Match($scene,"(?ms)^--- !u!4 &$cameraTf\n.*?(?=^--- )").Value
$scene = $scene.Replace($cameraBlock,[regex]::Replace($cameraBlock,'(?m)^  m_LocalPosition: \{x: 0, y: [^,]+, z: 0\}$',"  m_LocalPosition: {x: 0, y: $EyeHeightValue, z: 0}"))

# ---- 3. Materials: the campus map's own ----
$materials = @{
    Building  = '242005caf19da824eb83b12873987113';
    Ground    = 'c55d2c42704122d439a7362d72d7e748';
    Lawn      = '1442f7182eaf996489f56962730a8b15';
    Neighbour = '48eacf4a017432e4fb0d9417de011db1';
    Pavement  = '5d1d071aa055b824ca435c0f15086401'
}
foreach ($k in $materials.Keys) { if (-not (Test-Path "Assets/Materials/Map/$k.mat")) { throw "Missing material $k" } }

# ---- 4. Builders (same record format as Tools/CreateForestScene.ps1) ----
$records = New-Object 'Collections.Generic.List[string]'; $nodes = @{}; $script:next = $GenMin
function Node($name,$position='x: 0, y: 0, z: 0',$scale='x: 1, y: 1, z: 1',$parent=0,$yaw=0) {
    $base = $script:next; $script:next += 10
    $qY = F([Math]::Sin($yaw*[Math]::PI/360)); $qW = F([Math]::Cos($yaw*[Math]::PI/360))
    $node = @{ go=$base; transform=$base+1; component=$base+2; children=(New-Object 'Collections.Generic.List[string]'); components=(New-Object 'Collections.Generic.List[string]') }
    $node.components.Add("  - component: {fileID: $($base+1)}")
    $script:nodes[($base+1).ToString()] = $node
    if ($parent -ne 0) { $script:nodes[$parent.ToString()].children.Add("  - {fileID: $($base+1)}") }
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
# A box (mesh 10202), cylinder (10206) or sphere (10207) with a map material.
function Shape($name,[double]$x,[double]$y,[double]$z,[double]$sx,[double]$sy,[double]$sz,$surface,$parent,$mesh=10202,[double]$yaw=0,$collider=$true) {
    $node = Node $name "x: $(F $x), y: $(F $y), z: $(F $z)" "x: $(F $sx), y: $(F $sy), z: $(F $sz)" $parent $yaw
    $render = $blocks['342607414'].Replace('342607414',($node.go+3).ToString()).Replace('342607412',$node.go.ToString()).Replace('711cdfd5dba1a774da5183b41e276c68',$materials[$surface])
    $filter = $blocks['342607416'].Replace('342607416',($node.go+4).ToString()).Replace('342607412',$node.go.ToString()).Replace('m_Mesh: {fileID: 10202,',"m_Mesh: {fileID: $mesh,")
    $node.components.Add("  - component: {fileID: $($node.go+3)}"); $node.components.Add("  - component: {fileID: $($node.go+4)}")
    $script:records.Add($render); $script:records.Add($filter)
    if ($collider) {
        $node.components.Add("  - component: {fileID: $($node.go+5)}")
        $script:records.Add($blocks['342607415'].Replace('342607415',($node.go+5).ToString()).Replace('342607412',$node.go.ToString()))
    }
    return $node
}
# A copy of a prefab instance already in the scene (a barrel, a crate).
function PrefabCopy($sourceName,$newName,[double]$x,[double]$z,$parent) {
    $source = $null
    foreach ($b in $blocks.Values) {
        if ($b.StartsWith('--- !u!1001 ') -and $b -match "(?m)^      propertyPath: m_Name\n      value: $([regex]::Escape($sourceName))\n") { $source = $b; break }
    }
    if ($source -eq $null) { throw "No prefab instance named $sourceName" }
    $oldPi = [regex]::Match($source,'^--- !u!1001 &(\d+)').Groups[1].Value
    $stripped = $null
    foreach ($b in $blocks.Values) { if ($b -match '^--- !u!4 &\d+ stripped' -and $b.Contains("m_PrefabInstance: {fileID: $oldPi}")) { $stripped = $b; break } }
    if ($stripped -eq $null) { throw "No stripped transform for $sourceName" }
    $pi = $script:next; $st = $pi + 1; $script:next += 10
    $copy = $source.Replace("--- !u!1001 &$oldPi","--- !u!1001 &$pi")
    $copy = [regex]::Replace($copy,'m_TransformParent: \{fileID: \d+\}',"m_TransformParent: {fileID: $parent}")
    $copy = [regex]::Replace($copy,'(propertyPath: m_LocalPosition\.x\n      value: )[^\n]+',"`${1}$(F $x)")
    $copy = [regex]::Replace($copy,'(propertyPath: m_LocalPosition\.z\n      value: )[^\n]+',"`${1}$(F $z)")
    $copy = [regex]::Replace($copy,'(propertyPath: m_Name\n      value: )[^\n]+',"`${1}$newName")
    $oldSt = [regex]::Match($stripped,'^--- !u!4 &(\d+)').Groups[1].Value
    $strip = $stripped.Replace("--- !u!4 &$oldSt","--- !u!4 &$st").Replace("m_PrefabInstance: {fileID: $oldPi}","m_PrefabInstance: {fileID: $pi}")
    $script:records.Add($copy.TrimEnd("`n")); $script:records.Add($strip.TrimEnd("`n"))
    $script:nodes[$parent.ToString()].children.Add("  - {fileID: $st}")
}

# ---- 5. The arena ----
$Cx = 0.0; $Cz = -250.0                     # ArenaCentre: away from the campus map
$FieldA = 33.0; $FieldB = 22.0               # the field's oval (half length east-west, half width)
$Px = 5.0; $Pz = 0.0                         # the player's stop (local), facing east: close enough that zombies reach them in good time
$TierDepth = 4.0; $Tiers = 6

$root    = Node 'Endless - The Coliseum'
$ground  = Node 'Coliseum - Field and Ground' -parent $root.transform
$stands  = Node 'Coliseum - Stands' -parent $root.transform
$tunnels = Node 'Coliseum - Gates' -parent $root.transform
$land    = Node 'Coliseum - Peristyle and Towers' -parent $root.transform
$fight   = Node 'Endless Arena Fight' -parent $root.transform

function WX([double]$x) { $Cx + $x }
function WZ([double]$z) { $Cz + $z }

# Ground, apron (track) and the field with its yard lines.
[void](Shape 'Ground' (WX 0) -0.1 (WZ 0) 300 0.2 240 'Ground' $ground.transform)
[void](Shape 'Track and Apron' (WX 0) 0.01 (WZ 0) (2*$FieldA+14) 0.02 (2*$FieldB+14) 'Pavement' $ground.transform -collider $false)
[void](Shape 'Field' (WX 0) 0.025 (WZ 0) (2*$FieldA-4) 0.02 (2*$FieldB-4) 'Lawn' $ground.transform -collider $false)
for ($x = -25; $x -le 25; $x += 10) {
    [void](Shape 'Yard Line' (WX $x) 0.04 (WZ 0) 0.3 0.01 (2*$FieldB-6) 'Pavement' $ground.transform -collider $false)
}

# The six gates: a small gatehouse on the apron in front of the stands, its
# door facing the field. Zombies appear just behind the closed door, burst it
# open (Door.BurstOpen, as on the campus) and count as "out" (their word shows)
# at the Exit, ExitOut metres in front of the door, in the open.
$plans = @(
    @{ name='Stage 1 - East Gate North'; deg=-6 },  @{ name='Stage 1 - East Gate South'; deg=6 },
    @{ name='Stage 2 - Northeast Gate'; deg=-20 },  @{ name='Stage 2 - Southeast Gate'; deg=20 },
    @{ name='Stage 3 - North Gate'; deg=-32 },      @{ name='Stage 3 - South Gate'; deg=32 }
)
# Facing east = +x; a positive angle turns right (toward -z, south).
$A = $FieldA - 2; $B = $FieldB - 2
$lanes = New-Object 'Collections.Generic.List[object]'
foreach ($p in $plans) {
    $r = $p.deg * [Math]::PI / 180; $c = [Math]::Cos($r); $s = -[Math]::Sin($r)
    $qa = $c*$c/($A*$A) + $s*$s/($B*$B); $qb = 2*($Px*$c/($A*$A) + $Pz*$s/($B*$B)); $qc = $Px*$Px/($A*$A) + $Pz*$Pz/($B*$B) - 1
    $t = (-$qb + [Math]::Sqrt($qb*$qb - 4*$qa*$qc)) / (2*$qa)
    $ex = $Px + $c*$t; $ez = $Pz + $s*$t
    $nx = $ex/($A*$A); $nz = $ez/($B*$B); $nl = [Math]::Sqrt($nx*$nx+$nz*$nz); $nx /= $nl; $nz /= $nl
    $p.ex = $ex; $p.ez = $ez; $p.nx = $nx; $p.nz = $nz; $p.distance = $t
    $lanes.Add(@($ex,$ez))
}

# The WEST TUNNEL (the real Coliseum's players' tunnel, at the west end):
# behind the player, a little south of the middle line (the west goal posts
# stand on it). It runs through the three lowest stand tiers, which have a
# gap for it. WaveSpawner's REAR RUSH sends a few zombies out of it now and then.
$WtHalf = 3.0                # half the inner width
$WtLength = 13.0             # from the mouth (on the field's edge) back to its end wall
$WtHeight = 4.5              # inner height
$wtZ = -7.0
$A2 = $FieldA - 2; $B2 = $FieldB - 2
$wt = @{ ex = -$A2 * [Math]::Sqrt(1 - ($wtZ/$B2)*($wtZ/$B2)); ez = $wtZ }
$wtNx = $wt.ex/($A2*$A2); $wtNz = $wt.ez/($B2*$B2); $wtL = [Math]::Sqrt($wtNx*$wtNx + $wtNz*$wtNz)
$wt.nx = $wtNx/$wtL; $wt.nz = $wtNz/$wtL
# Is a point in the tunnel's footprint (plus margin)? along: metres out from the mouth.
function InFootprint($p,[double]$x,[double]$z,[double]$alongMin,[double]$alongMax,[double]$half) {
    $dx = $x - $p.ex; $dz = $z - $p.ez
    $along = $dx*$p.nx + $dz*$p.nz; $side = [Math]::Abs(-$dx*$p.nz + $dz*$p.nx)
    return ($along -gt $alongMin -and $along -lt $alongMax -and $side -lt $half)
}
# Does a stand segment (sampled along its length) reach into the tunnel?
function InWestTunnel($x0,$z0,$x1,$z1) {
    for ($t = 0.0; $t -le 1.0; $t += 0.125) {
        if (InFootprint $wt ($x0 + ($x1-$x0)*$t) ($z0 + ($z1-$z0)*$t) -2 ($WtLength + 1.5) ($WtHalf + 0.6 + 0.4)) { return $true }
    }
    return $false
}

# Stands: six stepped oval tiers of boxes (the gates stand in front of them;
# the three lowest have a gap for the west tunnel).
$segmentCount = 0
for ($k = 0; $k -lt $Tiers; $k++) {
    $a = $FieldA + 2 + $TierDepth*$k; $b = $FieldB + 2 + $TierDepth*$k
    $height = 1.2 + 1.4*$k
    $perimeter = 2*[Math]::PI*[Math]::Sqrt(($a*$a+$b*$b)/2)
    $n = [int][Math]::Ceiling($perimeter / 3.5)
    $surface = if ($k % 2 -eq 0) { 'Building' } else { 'Neighbour' }
    for ($i = 0; $i -lt $n; $i++) {
        $f0 = 2*[Math]::PI*$i/$n; $f1 = 2*[Math]::PI*($i+1)/$n; $fm = ($f0+$f1)/2
        $x0 = $a*[Math]::Cos($f0); $z0 = $b*[Math]::Sin($f0); $x1 = $a*[Math]::Cos($f1); $z1 = $b*[Math]::Sin($f1)
        $mx = $a*[Math]::Cos($fm); $mz = $b*[Math]::Sin($fm)
        if ($k -le 2 -and (InWestTunnel $x0 $z0 $x1 $z1)) { continue }
        $len = [Math]::Sqrt(($x1-$x0)*($x1-$x0)+($z1-$z0)*($z1-$z0)) + 0.4
        $yaw = [Math]::Atan2($x1-$x0, $z1-$z0) * 180 / [Math]::PI
        [void](Shape "Stand Tier $($k+1)" (WX $mx) ($height/2) (WZ $mz) $TierDepth $height $len $surface $stands.transform -yaw $yaw)
        $segmentCount++
    }
}
# Parapet along the top of the bowl.
$a = $FieldA + 2 + $TierDepth*$Tiers - 1; $b = $FieldB + 2 + $TierDepth*$Tiers - 1
$n = [int][Math]::Ceiling(2*[Math]::PI*[Math]::Sqrt(($a*$a+$b*$b)/2) / 4)
for ($i = 0; $i -lt $n; $i++) {
    $f0 = 2*[Math]::PI*$i/$n; $f1 = 2*[Math]::PI*($i+1)/$n; $fm = ($f0+$f1)/2
    $x0 = $a*[Math]::Cos($f0); $z0 = $b*[Math]::Sin($f0); $x1 = $a*[Math]::Cos($f1); $z1 = $b*[Math]::Sin($f1)
    $len = [Math]::Sqrt(($x1-$x0)*($x1-$x0)+($z1-$z0)*($z1-$z0)) + 0.3
    $yaw = [Math]::Atan2($x1-$x0, $z1-$z0) * 180 / [Math]::PI
    $top = 1.2 + 1.4*($Tiers-1)
    [void](Shape 'Parapet' (WX ($a*[Math]::Cos($fm))) ($top+0.75) (WZ ($b*[Math]::Sin($fm))) 0.6 1.5 $len 'Neighbour' $stands.transform -yaw $yaw -collider $false)
}

# Gates: a gatehouse (two side blocks, a roof over the doorway, a back wall)
# around a Door prefab, the spawn point just behind the door and the Exit in
# front of it. The door face stands on the field's edge ($p.ex, the point the
# lane from the player meets it); the stands start about 2 m behind it, so the
# gatehouse backs onto them.
$DoorGuid = '535c098995fe77645be1eabe8a18e121'
$DoorScale = 1.4             # the campus door is 1.6 x 2.6 m: here 2.24 x 3.64 m
$GateHalf = 1.25             # half the doorway's inner width
$GateSide = 1.0              # width of each side block
$GateInner = 1.8             # depth of the room behind the door
$GateBack = 0.6              # back wall thickness
$GateHeight = 4.2
$DoorTop = 2.6 * $DoorScale
$ExitOut = 1.5               # the Exit: this far in front of the door
$BehindDoor = 0.9            # the spawn point: this far behind the door
function AddDoor($name,[double]$x,[double]$z,[double]$yaw,$parent) {
    $pi = $script:next; $st = $pi + 1; $dc = $pi + 2; $script:next += 10
    $qY = F([Math]::Sin($yaw*[Math]::PI/360)); $qW = F([Math]::Cos($yaw*[Math]::PI/360))
    $doorRoot = "{fileID: 2628932372315101373, guid: $DoorGuid, type: 3}"
    $mods = ''
    foreach ($pv in @(@('m_LocalPosition.x',(F $x)),@('m_LocalPosition.y','0'),@('m_LocalPosition.z',(F $z)),
                      @('m_LocalRotation.w',$qW),@('m_LocalRotation.x','0'),@('m_LocalRotation.y',$qY),@('m_LocalRotation.z','0'),
                      @('m_LocalScale.x',(F $DoorScale)),@('m_LocalScale.y',(F $DoorScale)),@('m_LocalScale.z',(F $DoorScale)))) {
        $mods += "    - target: $doorRoot`n      propertyPath: $($pv[0])`n      value: $($pv[1])`n      objectReference: {fileID: 0}`n"
    }
    $mods += "    - target: {fileID: 6463601933640435491, guid: $DoorGuid, type: 3}`n      propertyPath: m_Name`n      value: $name`n      objectReference: {fileID: 0}`n"
    $script:records.Add(@"
--- !u!1001 &$pi
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {fileID: $parent}
    m_Modifications:
$($mods.TrimEnd("`n"))
    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents: []
  m_SourcePrefab: {fileID: 100100000, guid: $DoorGuid, type: 3}
--- !u!4 &$st stripped
Transform:
  m_CorrespondingSourceObject: $doorRoot
  m_PrefabInstance: {fileID: $pi}
  m_PrefabAsset: {fileID: 0}
--- !u!114 &$dc stripped
MonoBehaviour:
  m_CorrespondingSourceObject: {fileID: 2139242959772549417, guid: $DoorGuid, type: 3}
  m_PrefabInstance: {fileID: $pi}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 36c104718febb1141b4bd7045ac0ad64, type: 3}
  m_Name:
  m_EditorClassIdentifier: Assembly-CSharp::Door
"@)
    $script:nodes[$parent.ToString()].children.Add("  - {fileID: $st}")
    return $dc
}
$spawnCount = 0
foreach ($p in $plans) {
    $yaw = [Math]::Atan2($p.nx, $p.nz) * 180 / [Math]::PI     # pointing away from the field
    $rx = $p.nz; $rz = -$p.nx                                  # across the doorway
    function At([double]$along,[double]$across) { return @(($p.ex + $p.nx*$along + $rx*$across), ($p.ez + $p.nz*$along + $rz*$across)) }
    $gate = Node $p.name -parent $tunnels.transform
    $depth = $GateInner + $GateBack
    foreach ($side in -1,1) {
        $c = At ($depth/2) (($GateHalf + $GateSide/2)*$side)
        [void](Shape 'Gate Side' (WX $c[0]) ($GateHeight/2) (WZ $c[1]) $GateSide $GateHeight $depth 'Neighbour' $gate.transform -yaw $yaw)
    }
    $c = At ($GateInner/2) 0
    [void](Shape 'Gate Lintel' (WX $c[0]) (($DoorTop+$GateHeight)/2) (WZ $c[1]) (2*$GateHalf) ($GateHeight-$DoorTop) $GateInner 'Building' $gate.transform -yaw $yaw)
    $c = At ($GateInner + $GateBack/2) 0
    [void](Shape 'Gate Back' (WX $c[0]) ($GateHeight/2) (WZ $c[1]) (2*$GateHalf) $GateHeight $GateBack 'Neighbour' $gate.transform -yaw $yaw)
    $c = At ($depth/2) 0
    [void](Shape 'Gate Cap' (WX $c[0]) ($GateHeight+0.15) (WZ $c[1]) (2*($GateHalf+$GateSide)+0.3) 0.3 ($depth+0.3) 'Building' $gate.transform -yaw $yaw)
    # The door: its forward (blue arrow) points out to the field, the way it swings.
    $door = AddDoor 'Gate Door' (WX $p.ex) (WZ $p.ez) ($yaw+180) $gate.transform
    $s = At $BehindDoor 0
    $spawn = Node $p.name "x: $(F (WX $s[0])), y: 0, z: $(F (WZ $s[1]))" -parent $fight.transform -yaw ($yaw+180)
    # The Exit is NOT under the spawn point (that one is turned, and its
    # children's positions are local); under the fight (at the origin) its
    # position is the world position.
    $o = At (-$ExitOut) 0
    $exit = Node "$($p.name) - Exit" "x: $(F (WX $o[0])), y: 0, z: $(F (WZ $o[1]))" -parent $fight.transform
    Script $spawn '45c6fbc59f09d6d47a56890fea2d098f' "  exit: {fileID: $($exit.transform)}`n  door: {fileID: $door}`n  spread: 0.5"
    $p.exitX = $o[0]; $p.exitZ = $o[1]
    $spawnCount++
}

# ---- The west tunnel's walls, roof and its REAR spawn point ----
$wtYaw = [Math]::Atan2($wt.nx, $wt.nz) * 180 / [Math]::PI
$wtRx = $wt.nz; $wtRz = -$wt.nx
function WtAt([double]$along,[double]$across) { return @(($wt.ex + $wt.nx*$along + $wtRx*$across), ($wt.ez + $wt.nz*$along + $wtRz*$across)) }
$westTunnel = Node 'West Tunnel' -parent $tunnels.transform
foreach ($side in -1,1) {
    $c = WtAt ($WtLength/2) (($WtHalf + 0.3)*$side)
    [void](Shape 'Tunnel Wall' (WX $c[0]) ($WtHeight/2) (WZ $c[1]) 0.6 $WtHeight $WtLength 'Neighbour' $westTunnel.transform -yaw $wtYaw)
}
$c = WtAt ($WtLength/2) 0
[void](Shape 'Tunnel Roof' (WX $c[0]) ($WtHeight+0.3) (WZ $c[1]) (2*$WtHalf+1.2) 0.6 $WtLength 'Building' $westTunnel.transform -yaw $wtYaw)
$c = WtAt ($WtLength+0.3) 0
[void](Shape 'Tunnel End Wall' (WX $c[0]) ($WtHeight/2) (WZ $c[1]) (2*$WtHalf+1.2) $WtHeight 0.6 'Neighbour' $westTunnel.transform -yaw $wtYaw)
$c = WtAt 0.3 0
[void](Shape 'Tunnel Portal' (WX $c[0]) ($WtHeight+0.9) (WZ $c[1]) (2*$WtHalf+2.4) 0.6 0.6 'Pavement' $westTunnel.transform -yaw $wtYaw)
# The spawn point deep inside (named "Rear ...", NOT under the fight: only the
# REAR RUSH uses it), its Exit just outside the mouth, on the field.
$s = WtAt 8.0 0
$rear = Node 'Rear - West Tunnel' "x: $(F (WX $s[0])), y: 0, z: $(F (WZ $s[1]))" -parent $tunnels.transform -yaw ($wtYaw+180)
$o = WtAt -1.5 0
$rearExit = Node 'Rear - West Tunnel - Exit' "x: $(F (WX $o[0])), y: 0, z: $(F (WZ $o[1]))" -parent $tunnels.transform
Script $rear '45c6fbc59f09d6d47a56890fea2d098f' "  exit: {fileID: $($rearExit.transform)}`n  door: {fileID: 0}`n  spread: 1.2"

# ---- The field wall: a low wall around the field (the real field is sunk
# below the stands, ringed by a wall), with gaps at the gates and the tunnel ----
$WallA = $FieldA - 1.2; $WallB = $FieldB - 1.2
$fieldWall = Node 'Coliseum - Field Wall' -parent $root.transform
$n = [int][Math]::Ceiling(2*[Math]::PI*[Math]::Sqrt(($WallA*$WallA+$WallB*$WallB)/2) / 2.2)
$wallCount = 0
for ($i = 0; $i -lt $n; $i++) {
    $f0 = 2*[Math]::PI*$i/$n; $f1 = 2*[Math]::PI*($i+1)/$n; $fm = ($f0+$f1)/2
    $x0 = $WallA*[Math]::Cos($f0); $z0 = $WallB*[Math]::Sin($f0); $x1 = $WallA*[Math]::Cos($f1); $z1 = $WallB*[Math]::Sin($f1)
    $gap = $false
    for ($t = 0.0; $t -le 1.0 -and -not $gap; $t += 0.25) {
        $x = $x0 + ($x1-$x0)*$t; $z = $z0 + ($z1-$z0)*$t
        foreach ($p in $plans) { if (InFootprint $p $x $z -1.5 4 ($GateHalf + $GateSide + 0.6)) { $gap = $true } }
        if (InFootprint $wt $x $z -1.5 4 ($WtHalf + 1.2)) { $gap = $true }
    }
    if ($gap) { continue }
    $len = [Math]::Sqrt(($x1-$x0)*($x1-$x0)+($z1-$z0)*($z1-$z0)) + 0.15
    $yaw = [Math]::Atan2($x1-$x0, $z1-$z0) * 180 / [Math]::PI
    $mx = $WallA*[Math]::Cos($fm); $mz = $WallB*[Math]::Sin($fm)
    [void](Shape 'Field Wall' (WX $mx) 0.6 (WZ $mz) 0.35 1.2 $len 'Neighbour' $fieldWall.transform -yaw $yaw)
    [void](Shape 'Field Wall Cap' (WX $mx) 1.25 (WZ $mz) 0.5 0.1 $len 'Pavement' $fieldWall.transform -yaw $yaw -collider $false)
    $wallCount++
}

# ---- Aisles: stairways from the field wall to the top of the bowl, cutting
# the stands into sections (lighter steps over each tier); none at a gate or
# at the tunnel ----
$aisles = Node 'Coliseum - Aisles' -parent $stands.transform
$aisleCount = 0
for ($i = 0; $i -lt 16; $i++) {
    $f = 2*[Math]::PI*($i + 0.5)/16
    $blocked = $false
    for ($k = 0; $k -lt 3 -and -not $blocked; $k++) {
        $x = ($FieldA + 2 + $TierDepth*$k)*[Math]::Cos($f); $z = ($FieldB + 2 + $TierDepth*$k)*[Math]::Sin($f)
        foreach ($p in $plans) { if (InFootprint $p $x $z -3 18 ($GateHalf + $GateSide + 2)) { $blocked = $true } }
        if (InFootprint $wt $x $z -3 ($WtLength + 3) ($WtHalf + 2.5)) { $blocked = $true }
    }
    if ($blocked) { continue }
    for ($k = 0; $k -lt $Tiers; $k++) {
        $a = $FieldA + 2 + $TierDepth*$k; $b = $FieldB + 2 + $TierDepth*$k
        $height = 1.2 + 1.4*$k
        $tx = -$a*[Math]::Sin($f); $tz = $b*[Math]::Cos($f)          # along the tier
        $yaw = [Math]::Atan2($tx, $tz) * 180 / [Math]::PI
        [void](Shape "Aisle Step $($k+1)" (WX ($a*[Math]::Cos($f))) (($height+0.08)/2) (WZ ($b*[Math]::Sin($f))) ($TierDepth+0.04) ($height+0.08) 1.6 'Pavement' $aisles.transform -yaw $yaw)
        # a half step on the front half of each tier, so it reads as stairs
        [void](Shape 'Aisle Half Step' (WX (($a-$TierDepth/4)*[Math]::Cos($f))) ($height+0.3) (WZ (($b-$TierDepth/4)*[Math]::Sin($f))) ($TierDepth/2) 0.6 1.6 'Pavement' $aisles.transform -yaw $yaw)
    }
    $aisleCount++
}

# ---- Sidelines: team benches, coolers and equipment cases (low: zombies walk
# round them), different on the two sides ----
$sidelines = Node 'Coliseum - Sidelines' -parent $root.transform
foreach ($side in @(@{ z = 17.4; x0 = -9.0; benches = 3 }, @{ z = -17.6; x0 = -4.0; benches = 2 })) {
    $sz = $side.z; $sgn = [Math]::Sign($sz)
    for ($j = 0; $j -lt $side.benches; $j++) {
        $bx = $side.x0 + $j*6.5
        [void](Shape 'Team Bench' (WX $bx) 0.25 (WZ $sz) 5.0 0.5 0.5 'Building' $sidelines.transform)
        [void](Shape 'Team Bench Back' (WX $bx) 0.6 (WZ ($sz + 0.3*$sgn)) 5.0 0.6 0.12 'Building' $sidelines.transform)
    }
    $end = $side.x0 + ($side.benches - 1)*6.5 + 4.0
    [void](Shape 'Cooler' (WX $end) 0.45 (WZ ($sz - 0.6*$sgn)) 0.8 0.45 0.8 'Pavement' $sidelines.transform 10206)
    [void](Shape 'Cooler' (WX ($end + 1.1)) 0.35 (WZ ($sz - 0.2*$sgn)) 0.7 0.35 0.7 'Pavement' $sidelines.transform 10206)
    [void](Shape 'Equipment Case' (WX ($side.x0 - 3.2)) 0.4 (WZ ($sz - 0.4*$sgn)) 1.4 0.8 0.9 'Neighbour' $sidelines.transform -yaw (12*$sgn))
}
[void](Shape 'Medical Table' (WX 9.5) 0.45 (WZ 16.6) 2.0 0.9 0.9 'Pavement' $sidelines.transform)
[void](Shape 'Equipment Cart' (WX 7.0) 0.5 (WZ -16.4) 2.4 1.0 1.2 'Neighbour' $sidelines.transform -yaw -8)

# ---- End zones (darker strips beyond the goal lines) and goal posts ----
foreach ($sgn in -1,1) {
    [void](Shape 'End Zone' (WX (28*$sgn)) 0.035 (WZ 0) 6 0.01 (2*$FieldB-4) 'Ground' $ground.transform -collider $false)
}
# West posts: a normal goal post on the end line (behind the player).
$gp = Node 'Goal Posts West' -parent $root.transform
[void](Shape 'Goal Post Base' (WX -31.4) 1.5 (WZ 0) 0.3 3.0 0.3 'Pavement' $gp.transform)
[void](Shape 'Goal Crossbar' (WX -31.4) 3.0 (WZ 0) 0.2 0.2 5.8 'Pavement' $gp.transform)
foreach ($s in -1,1) { [void](Shape 'Goal Upright' (WX -31.4) 6.0 (WZ (2.8*$s)) 0.15 6.0 0.15 'Pavement' $gp.transform) }
# East posts: between the two front gates, raised above their roofs.
$gp = Node 'Goal Posts East' -parent $root.transform
[void](Shape 'Goal Post Base' (WX 32.0) 2.75 (WZ 0) 0.25 5.5 0.25 'Pavement' $gp.transform)
[void](Shape 'Goal Crossbar' (WX 32.0) 5.5 (WZ 0) 0.2 0.2 5.8 'Pavement' $gp.transform)
foreach ($s in -1,1) { [void](Shape 'Goal Upright' (WX 32.0) 8.5 (WZ (2.8*$s)) 0.15 6.0 0.15 'Pavement' $gp.transform) }

# The peristyle behind the east end: piers, a lintel, a taller central arch and the torch.
$px0 = $FieldA + 2 + $TierDepth*$Tiers + 2.5
for ($i = -4; $i -le 4; $i++) {
    $z = $i * 4.6
    $h = if ([Math]::Abs($i) -eq 0) { 0 } else { 16 }
    if ($h -gt 0) { [void](Shape 'Peristyle Pier' (WX $px0) ($h/2) (WZ $z) 1.8 $h 1.8 'Pavement' $land.transform) }
}
[void](Shape 'Peristyle Lintel' (WX $px0) 17 (WZ 0) 2.2 2 39 'Pavement' $land.transform)
foreach ($side in -1,1) { [void](Shape 'Peristyle Arch Pier' (WX $px0) 9.5 (WZ (2.4*$side)) 1.6 19 1.6 'Pavement' $land.transform) }
[void](Shape 'Peristyle Arch Top' (WX $px0) 19.8 (WZ 0) 2.4 1.6 6.4 'Pavement' $land.transform)
[void](Shape 'Torch Pedestal' (WX $px0) 22.1 (WZ 0) 2 3 2 'Neighbour' $land.transform)
[void](Shape 'Torch Bowl' (WX $px0) 24 (WZ 0) 3.2 0.8 3.2 'Neighbour' $land.transform 10206)

# Four light towers at the corners of the bowl, panels turned to the field.
foreach ($corner in @(@(1,1),@(1,-1),@(-1,1),@(-1,-1))) {
    $tx = $corner[0]*($FieldA + 24); $tz = $corner[1]*($FieldB + 22)
    [void](Shape 'Light Tower' (WX $tx) 15 (WZ $tz) 1.2 30 1.2 'Neighbour' $land.transform)
    $yaw = [Math]::Atan2(-$tx, -$tz) * 180 / [Math]::PI
    [void](Shape 'Light Panel' (WX $tx) 30.5 (WZ $tz) 5 3 0.6 'Pavement' $land.transform -yaw $yaw -collider $false)
}

# ---- 6. The fight: stop, route (none: the player never rides), boss stand, props ----
$stop = Node 'Last Stand' "x: $(F (WX $Px)), y: 0, z: $(F (WZ $Pz))" -parent $fight.transform -yaw 90
$route = Node 'Route (none)' -parent $fight.transform
$bossStand = Node 'Boss Stand - Midfield' "x: $(F (WX ($Px+17))), y: 0, z: $(F (WZ ($Pz-0.5)))" -parent $fight.transform   # clear of the props
Script $fight '2043d301c960d2844a39bdc259577d48' "  route: {fileID: $($route.transform)}`n  stop: {fileID: $($stop.transform)}`n  groupSize: 2`n  exitGate: {fileID: 0}`n  openGateFromStart: 0`n  isBossFight: 0`n  bossStand: {fileID: $($bossStand.transform)}"

# Sniper perches: empty spots on top of stand tiers 2-4 (WaveSpawner puts a
# SNIPER on a free one at random, from Endless wave 6). Bearings are picked
# off the gates' lines (a gatehouse would hide a sniper on a low tier behind it).
function RayToOval([double]$deg,[double]$a,[double]$b) {
    $r = $deg * [Math]::PI / 180; $c = [Math]::Cos($r); $s = -[Math]::Sin($r)
    $qa = $c*$c/($a*$a) + $s*$s/($b*$b); $qb = 2*($Px*$c/($a*$a) + $Pz*$s/($b*$b)); $qc = $Px*$Px/($a*$a) + $Pz*$Pz/($b*$b) - 1
    $t = (-$qb + [Math]::Sqrt($qb*$qb - 4*$qa*$qc)) / (2*$qa)
    return @(($Px + $c*$t), ($Pz + $s*$t))
}
# One tier higher than at first (tiers 3-5): up there they stand out against
# the top rows and the sky instead of hiding among the lower stands.
$perchPlan = @(@(-37,3), @(-26,2), @(-13,2), @(-1,4), @(9,3), @(14,2), @(25,4), @(36,3))   # (bearing, tier index 0-5)
$perchCount = 0
foreach ($pp in $perchPlan) {
    $k = $pp[1]
    $q = RayToOval $pp[0] ($FieldA + 2 + $TierDepth*$k) ($FieldB + 2 + $TierDepth*$k)
    $perchCount++
    [void](Node "Sniper Perch $perchCount" "x: $(F (WX $q[0])), y: $(F (1.2 + 1.4*$k)), z: $(F (WZ $q[1]))" -parent $fight.transform)
}

# Points at a bearing (degrees, + = right) and distance from the player.
function Bearing([double]$deg,[double]$dist) { $r = $deg*[Math]::PI/180; return @(($Px + [Math]::Cos($r)*$dist), ($Pz - [Math]::Sin($r)*$dist)) }
function Dist($a,$b) { [Math]::Sqrt(($a[0]-$b[0])*($a[0]-$b[0]) + ($a[1]-$b[1])*($a[1]-$b[1])) }
# How far a point is from the nearest zombie lane (its gate's Exit -> the player).
function LaneDistance($q) {
    $best = 1e9
    foreach ($p in $plans) {
        $ax = $p.exitX; $az = $p.exitZ; $dx = $Px - $ax; $dz = $Pz - $az
        $t = Clamp ((($q[0]-$ax)*$dx + ($q[1]-$az)*$dz) / ($dx*$dx + $dz*$dz)) 0 1
        $d = Dist $q @(($ax + $dx*$t), ($az + $dz*$t))
        if ($d -lt $best) { $best = $d }
    }
    return $best
}
# Props are scattered, not laid out in a pattern (field coordinates, x east,
# z north, the player at ($Px, $Pz)). The spots were picked at random under
# these rules, which the script checks again below:
#   Barrels: blast rings (Barrel.BlastRadius = 4 m) never overlap, so one
#   barrel never sets off the next; they are spread across the view (left,
#   straight ahead, right) as far apart as the rules allow; each stands 1.2-5 m from a zombie lane
#   (zombies walk past it, inside its ring) but not on it.
#   Crates: outside every blast ring, 6 m apart, at least 1.5 m off the lanes.
#   All: well inside the field and at least 4.5 m from every gate's Exit.
$BlastRadius = 4.0
function OnOpenField($q) {
    if (($q[0]/$A)*($q[0]/$A) + ($q[1]/$B)*($q[1]/$B) -gt 0.85) { return $false }
    foreach ($p in $plans) { if ((Dist $q @($p.exitX, $p.exitZ)) -lt 4.5) { return $false } }
    return $true
}
$barrelSpots = @(@(16.58, 8.79), @(24.95, -0.67), @(12.96, -6.22))   # left, ahead (far), right: at least 12.6 m apart
$crateSpots  = @(@(17.9, 2.83), @(19.37, -3.18), @(23.81, 4.1))   # health, energy, energy
foreach ($q in $barrelSpots) {
    $lane = LaneDistance $q
    if (-not (OnOpenField $q) -or $lane -lt 1.2 -or $lane -gt 5.0) { throw "Barrel at $($q -join ',') breaks the layout rules" }
    foreach ($o in $barrelSpots) { if (-not [object]::ReferenceEquals($o,$q) -and (Dist $q $o) -lt (2*$BlastRadius + 0.8)) { throw "Barrels at $($q -join ',') and $($o -join ',') share a blast" } }
}
foreach ($q in $crateSpots) {
    if (-not (OnOpenField $q) -or (LaneDistance $q) -lt 1.5) { throw "Crate at $($q -join ',') breaks the layout rules" }
    foreach ($o in $barrelSpots) { if ((Dist $q $o) -lt ($BlastRadius + 0.8)) { throw "Crate at $($q -join ',') is inside a blast" } }
    foreach ($o in $crateSpots) { if (-not [object]::ReferenceEquals($o,$q) -and (Dist $q $o) -lt 6.0) { throw "Crates at $($q -join ',') too close" } }
}
$i = 0
foreach ($b in $barrelSpots) { $i++; PrefabCopy 'Barrel 1' "Barrel $i" (WX $b[0]) (WZ $b[1]) $fight.transform }
PrefabCopy 'Health crate' 'Health crate' (WX $crateSpots[0][0]) (WZ $crateSpots[0][1]) $fight.transform
PrefabCopy 'Lure crate' 'Energy crate 1' (WX $crateSpots[1][0]) (WZ $crateSpots[1][1]) $fight.transform
PrefabCopy 'Lure crate' 'Energy crate 2' (WX $crateSpots[2][0]) (WZ $crateSpots[2][1]) $fight.transform
$propReport = (($barrelSpots | ForEach-Object { "barrel ($(F $_[0]), $(F $_[1]))" }) + ($crateSpots | ForEach-Object { "crate ($(F $_[0]), $(F $_[1]))" })) -join '; '

# ---- 7. Level: start at the stop, one encounter (Endless uses Encounters[0]) ----
$levelBlock = $null
foreach ($b in $blocks.Values) { if ($b -match 'm_Script: \{fileID: 11500000, guid: 727960bfc2110504ea8684c06fcb8676') { $levelBlock = $b; break } }
if ($levelBlock -eq $null) { throw 'No Level component in the scene' }
$newLevel = [regex]::Replace($levelBlock,'start: \{fileID: \d+\}',"start: {fileID: $($stop.transform)}")
# (The list may be empty here: step 1 removed the entry an earlier run made.)
$newLevel = [regex]::Replace($newLevel,'(?m)^  encounters:.*\n(?:  - \{fileID: \d+\}\n)*',"  encounters:`n  - {fileID: $($fight.component)}`n")
if (-not $newLevel.Contains("  - {fileID: $($fight.component)}")) { throw 'Could not set the Level encounters' }
$scene = $scene.Replace($levelBlock,$newLevel)

# ---- 8. Write the records in, validate, save ----
$extra = $records -join "`n"
$extra = [regex]::Replace($extra,'(COMPONENTS|CHILDREN)_(\d+)',[Text.RegularExpressions.MatchEvaluator]{
    param($match)
    $node = $nodes[([long]$match.Groups[2].Value+1).ToString()]
    if ($match.Groups[1].Value -eq 'COMPONENTS') { return ($node.components -join "`n") }
    if ($node.children.Count -gt 0) { return "  m_Children:`n"+($node.children -join "`n") }
    return '  m_Children: []'
})
$rootsHeader = '--- !u!1660057539 &9223372036854775807'
if (-not $scene.Contains($rootsHeader)) { throw 'No SceneRoots block' }
$scene = $scene.Replace($rootsHeader,($extra.TrimEnd("`n")+"`n"+$rootsHeader))
$scene = $scene.TrimEnd("`n") + "`n  - {fileID: $($root.transform)}`n"
$ids = New-Object 'Collections.Generic.HashSet[string]'
foreach ($m in [regex]::Matches($scene,'(?m)^--- !u!\d+ &(\d+)')) { if (-not $ids.Add($m.Groups[1].Value)) { throw "Duplicate scene ID $($m.Groups[1].Value)" } }
foreach ($m in [regex]::Matches($scene,'\{fileID: (\d+)\}')) { if ($m.Groups[1].Value -ne '0' -and -not $ids.Contains($m.Groups[1].Value)) { throw "Missing reference $($m.Value)" } }
Save $scenePath $scene
$distances = ($plans | ForEach-Object { "$($_.name) $([Math]::Round($_.distance,1)) m" }) -join '; '
Write-Output "Coliseum built: $segmentCount stand segments, $spawnCount gates, 3 barrels, 3 crates. Gates from the player: $distances"
Write-Output "Props: $propReport; $perchCount sniper perches; $wallCount field wall segments; $aisleCount aisles; west tunnel mouth ($(F $wt.ex), $(F $wt.ez))"
