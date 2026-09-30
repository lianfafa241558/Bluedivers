# Phase 2 step B (part 1): move/extract the 4 low-risk contracts into 01_GameContract.
# ASCII ONLY (PowerShell 5.1 reads .ps1 as ANSI).
# Namespaces are kept as-is so no call site needs editing.
$ErrorActionPreference = 'Stop'
$root = 'd:\Pro\Bluedivers\Assets\Scripts'
$utf8 = New-Object System.Text.UTF8Encoding($true)

# ---------- 1) whole-file moves (already standalone tiny files) ----------
$move = @(
    @{ src = "$root\02Game\Interface\IEquippable.cs";            dst = "$root\00GameContract\IEquippable.cs" },
    @{ src = "$root\02Game\Interface\ISubmittableHandItem.cs";   dst = "$root\00GameContract\ISubmittableHandItem.cs" },
    @{ src = "$root\02Game\05Interactable\IStepPress.cs";        dst = "$root\00GameContract\IStepPress.cs" }
)
foreach ($m in $move) {
    if (-not (Test-Path $m.src)) { throw "missing source: $($m.src)" }
    Move-Item $m.src $m.dst
    Move-Item "$($m.src).meta" "$($m.dst).meta"
    "moved $($m.src)"
}

# ---------- 2) extract FurnitureFlag + IFurniture (Furniture_Attached.cs lines 13..61) ----------
$faPath = "$root\02Game\05Interactable\Furniture_Attached.cs"
$fa = [System.IO.File]::ReadAllLines($faPath)
if ($fa[12] -notmatch '^\s+\[Flags\]') { throw "FurnitureFlag attr mismatch: '$($fa[12])'" }
if ($fa[13] -notmatch '^\s+public enum FurnitureFlag') { throw "FurnitureFlag mismatch: '$($fa[13])'" }
if ($fa[60] -ne '    }') { throw "IFurniture end mismatch: '$($fa[60])'" }

$new = @()
$new += 'using System;'
$new += 'using Core.Interface;'
$new += 'using UnityEngine;'
$new += ''
$new += 'namespace FPSGame.Furn'
$new += '{'
$new += $fa[12..60]
$new += '}'
[System.IO.File]::WriteAllLines("$root\00GameContract\FurnitureContract.cs", $new, $utf8)

$keep = @()
for ($i = 0; $i -lt $fa.Count; $i++) {
    if ($i -ge 12 -and $i -le 60) { continue }
    $keep += $fa[$i]
}
[System.IO.File]::WriteAllLines($faPath, $keep, $utf8)
"Furniture_Attached.cs: $($fa.Count) -> $($keep.Count) lines"
