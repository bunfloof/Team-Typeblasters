$ErrorActionPreference='Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$fingerprints=@{}
foreach($name in 'Easy','Normal','Hard') {
    $scene=[IO.File]::ReadAllText((Join-Path $PWD "Assets/Scenes/$name.unity"))
    $blocks=@{}
    foreach($match in [regex]::Matches($scene,'(?ms)^--- !u!\d+ &(\d+).*?(?=^--- !u!|\z)')) {
        if($blocks.ContainsKey($match.Groups[1].Value)){throw "Duplicate ID in $name"}
        $blocks[$match.Groups[1].Value]=$match.Value
    }
    foreach($match in [regex]::Matches($scene,'\{fileID: (\d+)\}')) {
        if($match.Groups[1].Value -ne '0' -and !$blocks.ContainsKey($match.Groups[1].Value)){throw "Missing local reference in $name"}
    }
    $level=@($blocks.Values | Where-Object {$_ -match 'guid: 727960bfc2110504ea8684c06fcb8676'})
    if($level.Count -ne 1){throw "Expected one Level in $name"}
    $encounters=[regex]::Matches(($level[0] -split '  encounters:')[1],'fileID: (\d+)')
    $expected=if($name -eq 'Easy'){6}else{4}
    if($encounters.Count -ne $expected){throw "Unexpected encounter count in $name"}
    $stops=foreach($encounter in $encounters) {
        $block=$blocks[$encounter.Groups[1].Value]
        $stopId=[regex]::Match($block,'stop: \{fileID: (\d+)').Groups[1].Value
        [regex]::Match($blocks[$stopId],'m_LocalPosition: (.*)').Groups[1].Value
    }
    $fingerprints[$name]=$stops -join '|'
    if($name -eq 'Easy') {
        if($scene -notmatch 'm_Name: Easy - Training Academy' -or $scene -notmatch 'guid: 50d9298a3d3e4f07b6ac41d0aa839285'){throw 'Easy academy/tutorial missing'}
        if($scene -notmatch '(?m)^  mapViewWaves: *\r?$'){throw 'Easy still has overhead camera waves'}
        $spawnCount=@($blocks.Values | Where-Object {$_ -match 'guid: 45c6fbc59f09d6d47a56890fea2d098f' -and $_ -match 'm_GameObject: \{fileID: 33'}).Count
        if($spawnCount -ne 18){throw 'Expected 18 academy spawn points'}
    }
    if($name -eq 'Hard' -and ($scene -notmatch 'm_Name: Hard - The Hollow Forest' -or $scene -notmatch 'enemySpeedMultiplier: 1.3')){throw 'Hard forest or speed changed'}
    Write-Output "PASS $name`: $expected independent encounters; scene references valid."
}
if(@($fingerprints.Values | Select-Object -Unique).Count -ne 3){throw 'Level routes are identical'}
$build=[IO.File]::ReadAllText((Join-Path $PWD 'ProjectSettings/EditorBuildSettings.asset'))
foreach($name in 'Menu','Easy','Normal','Hard') {
    if($build -notmatch "path: Assets/Scenes/$name.unity"){throw "Missing $name in Build Settings"}
}
Write-Output 'PASS three distinct routes and all four menu/build scenes.'
