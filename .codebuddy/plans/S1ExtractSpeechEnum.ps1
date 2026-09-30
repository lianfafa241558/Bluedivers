# S1: move SpeechTypeEnum out of RoleData_SO.cs into 01_GameContract (keep namespace Unity.FPS.Game).
# ASCII ONLY. Range verified: lines 82..200 of a 201-line file; the enum is indented 4 spaces (inside namespace).
$ErrorActionPreference = 'Stop'
$root = 'd:\Pro\Bluedivers\Assets\Scripts'
$utf8 = New-Object System.Text.UTF8Encoding($true)

$src = "$root\02Game\Game\Data\RoleData_SO.cs"
$lines = [System.IO.File]::ReadAllLines($src)
if ($lines[81] -notmatch '^\s*public enum SpeechTypeEnum') { throw "start mismatch: '$($lines[81])'" }
if ($lines[199] -ne '    }') { throw "enum end mismatch: '$($lines[199])'" }
if ($lines[200] -ne '}') { throw "namespace end mismatch: '$($lines[200])'" }

$new = @()
$new += 'using UnityEngine;'
$new += ''
$new += 'namespace Unity.FPS.Game'
$new += '{'
$new += $lines[81..199]
$new += '}'
[System.IO.File]::WriteAllLines("$root\00GameContract\SpeechTypeEnum.cs", $new, $utf8)

$keep = @()
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($i -ge 81 -and $i -le 199) { continue }
    $keep += $lines[$i]
}
[System.IO.File]::WriteAllLines($src, $keep, $utf8)
"RoleData_SO.cs: $($lines.Count) -> $($keep.Count) lines"
