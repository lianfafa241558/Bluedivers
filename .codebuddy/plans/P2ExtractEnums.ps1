# Phase 2 step A: exact line-range extraction of cross-layer enums into 01_GameContract.
# ASCII ONLY (PowerShell 5.1 reads .ps1 as ANSI; non-ASCII breaks parsing).
# Uses 1-based line numbers verified by reading the files; aborts on any mismatch.
$ErrorActionPreference = 'Stop'
$root = 'd:\Pro\Bluedivers\Assets\Scripts'
$utf8 = New-Object System.Text.UTF8Encoding($true)

# ---------- 1) MissionEnum (TaskManager.cs 512..667) + SizeType (695..708) ----------
$tmPath = "$root\01Manager\Global\TaskManager.cs"
$tm = [System.IO.File]::ReadAllLines($tmPath)
if ($tm[511] -notmatch '^public enum MissionEnum') { throw "MissionEnum start mismatch: '$($tm[511])'" }
if ($tm[666] -ne '}') { throw "MissionEnum end mismatch: '$($tm[666])'" }
if ($tm[697] -notmatch '^public enum SizeType') { throw "SizeType start mismatch: '$($tm[697])'" }
if ($tm[707] -ne '}') { throw "SizeType end mismatch: '$($tm[707])'" }
if ($tm[694] -notmatch '^/// <summary>') { throw "SizeType doc mismatch: '$($tm[694])'" }

$mission = @()
$mission += 'using UnityEngine;'
$mission += ''
$mission += $tm[511..666]
[System.IO.File]::WriteAllLines("$root\00GameContract\MissionEnum.cs", $mission, $utf8)

$size = @($tm[694..707])
[System.IO.File]::WriteAllLines("$root\00GameContract\SizeType.cs", $size, $utf8)

$keep = @()
for ($i = 0; $i -lt $tm.Count; $i++) {
    if (($i -ge 511 -and $i -le 666) -or ($i -ge 694 -and $i -le 707)) { continue }
    $keep += $tm[$i]
}
[System.IO.File]::WriteAllLines($tmPath, $keep, $utf8)
"TaskManager.cs: $($tm.Count) -> $($keep.Count) lines"

# ---------- 2) OOPartEnum (PropertyManager.cs 44..62) ----------
$pmPath = "$root\01Manager\Global\PropertyManager.cs"
$pm = [System.IO.File]::ReadAllLines($pmPath)
if ($pm[43] -notmatch '^public enum OOPartEnum') { throw "OOPartEnum start mismatch: '$($pm[43])'" }
if ($pm[61] -ne '}') { throw "OOPartEnum end mismatch: '$($pm[61])'" }

$oopart = @()
$oopart += 'using UnityEngine;'
$oopart += ''
$oopart += $pm[43..61]
[System.IO.File]::WriteAllLines("$root\00GameContract\OOPartEnum.cs", $oopart, $utf8)

$keep2 = @()
for ($i = 0; $i -lt $pm.Count; $i++) {
    if ($i -ge 43 -and $i -le 61) { continue }
    $keep2 += $pm[$i]
}
[System.IO.File]::WriteAllLines($pmPath, $keep2, $utf8)
"PropertyManager.cs: $($pm.Count) -> $($keep2.Count) lines"

# ---------- 3) TerrainType (GenerateNoiseTerrain.cs 11..34) ----------
$gnPath = "$root\08Map\GenerateNoiseTerrain.cs"
$gn = [System.IO.File]::ReadAllLines($gnPath)
if ($gn[10] -notmatch '^\s+public enum TerrainType') { throw "TerrainType start mismatch: '$($gn[10])'" }
if ($gn[33] -ne '    }') { throw "TerrainType end mismatch: '$($gn[33])'" }

$terrain = @()
$terrain += 'using UnityEngine;'
$terrain += ''
$terrain += $gn[10..33]
[System.IO.File]::WriteAllLines("$root\00GameContract\TerrainType.cs", $terrain, $utf8)

$keep3 = @()
for ($i = 0; $i -lt $gn.Count; $i++) {
    if ($i -ge 10 -and $i -le 33) { continue }
    $keep3 += $gn[$i]
}
[System.IO.File]::WriteAllLines($gnPath, $keep3, $utf8)
"GenerateNoiseTerrain.cs: $($gn.Count) -> $($keep3.Count) lines"
