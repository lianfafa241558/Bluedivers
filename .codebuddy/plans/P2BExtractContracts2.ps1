# Phase 2 step B (part 2): extract IVehicleUIController / TargetCfg / IDamageData into 01_GameContract.
# ASCII ONLY. Namespaces kept as-is (global / Unity.FPS.Game) => zero call-site churn.
$ErrorActionPreference = 'Stop'
$root = 'd:\Pro\Bluedivers\Assets\Scripts'
$utf8 = New-Object System.Text.UTF8Encoding($true)

function Cut-Range([string]$path, [int]$from, [int]$to) {
    $lines = [System.IO.File]::ReadAllLines($path)
    $keep = @()
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($i -ge $from -and $i -le $to) { continue }
        $keep += $lines[$i]
    }
    [System.IO.File]::WriteAllLines($path, $keep, $utf8)
    "$([System.IO.Path]::GetFileName($path)): $($lines.Count) -> $($keep.Count) lines"
    return ,$lines
}

# ---------- 1) IVehicleUIController  (04UI/UI/VehicleUI.cs lines 11..25) ----------
$vuPath = "$root\04UI\UI\VehicleUI.cs"
$vu = [System.IO.File]::ReadAllLines($vuPath)
if ($vu[10] -notmatch '^public interface IVehicleUIController') { throw "IVehicleUIController mismatch: '$($vu[10])'" }
if ($vu[24] -ne '}') { throw "IVehicleUIController end mismatch: '$($vu[24])'" }
$new = @('using UnityEngine;', 'using UnityEngine.Events;', '')
$new += $vu[10..24]
[System.IO.File]::WriteAllLines("$root\00GameContract\IVehicleUIController.cs", $new, $utf8)
Cut-Range $vuPath 10 24

# ---------- 2) TargetCfg  (02Game/Game/UnitQueryGrid.cs lines 269..278) ----------
$uqPath = "$root\02Game\Game\UnitQueryGrid.cs"
$uq = [System.IO.File]::ReadAllLines($uqPath)
if ($uq[268] -notmatch '^\[System.Serializable\]') { throw "TargetCfg attr mismatch: '$($uq[268])'" }
if ($uq[269] -notmatch '^public class TargetCfg') { throw "TargetCfg mismatch: '$($uq[269])'" }
if ($uq[277] -ne '}') { throw "TargetCfg end mismatch: '$($uq[277])'" }
$new2 = @('using Core;', '')
$new2 += $uq[268..277]
[System.IO.File]::WriteAllLines("$root\00GameContract\TargetCfg.cs", $new2, $utf8)
Cut-Range $uqPath 268 277

# ---------- 3) IDamageData  (02Game/Game/Shared/Weapon/DamageData.cs lines 12..46) ----------
$ddPath = "$root\02Game\Game\Shared\Weapon\DamageData.cs"
$dd = [System.IO.File]::ReadAllLines($ddPath)
if ($dd[11] -notmatch '^\s*/// <summary>') { throw "IDamageData doc mismatch: '$($dd[11])'" }
if ($dd[14] -notmatch '^\s*public interface IDamageData') { throw "IDamageData mismatch: '$($dd[14])'" }
if ($dd[45] -ne '    }') { throw "IDamageData end mismatch: '$($dd[45])'" }
$new3 = @('using System.Collections.Generic;', 'using Core;', 'using PEMaths;', 'using UnityEngine;', '')
$new3 += 'namespace Unity.FPS.Game'
$new3 += '{'
$new3 += $dd[11..45]
$new3 += '}'
[System.IO.File]::WriteAllLines("$root\00GameContract\IDamageData.cs", $new3, $utf8)
Cut-Range $ddPath 11 45
