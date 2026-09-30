# List every identifier in 02Data/**/*.cs that resolves to a type declared OUTSIDE 02Data.
# Purpose: definitive leak list for making 02Data an independent assembly (Phase 2).
$root = 'd:\Pro\Bluedivers\Assets\Scripts'
$asmdefDirs = @{}
Get-ChildItem -Path $root -Recurse -Filter *.asmdef -File | ForEach-Object { $asmdefDirs[$_.DirectoryName] = $_.BaseName }

function Get-Unit([string]$filePath) {
    $dirPath = Split-Path $filePath -Parent
    $d = $dirPath
    while ($d -and $d.Length -ge $root.Length) {
        if ($asmdefDirs.ContainsKey($d)) { return 'ASM:' + $asmdefDirs[$d] }
        $p = Split-Path $d -Parent
        if (-not $p -or $p -eq $d) { break }
        $d = $p
    }
    $rel = $dirPath.Substring($root.Length).TrimStart('\', '/')
    $a = $rel -split '[\\/]'
    if ($a.Count -ge 4 -and $a[0] -eq '02Game') { return "$($a[0])/$($a[1])/$($a[2])/$($a[3])" }
    if ($a.Count -ge 3 -and $a[0] -eq '02Game') { return "$($a[0])/$($a[1])/$($a[2])" }
    if ($a.Count -ge 2) { return "$($a[0])/$($a[1])" }
    return $a[0]
}

$all = @(Get-ChildItem -Path $root -Recurse -Filter *.cs -File)
$decl = @{}
$fileUnit = @{}
$declRx = [regex]'(?m)^\s*(?:\[[^\]]*\]\s*)*(?:public|internal|private|protected|sealed|abstract|static|partial|\s)*\b(?:class|struct|interface|enum)\s+([A-Za-z_]\w*)'
foreach ($f in $all) {
    $fileUnit[$f.FullName] = Get-Unit $f.FullName
    $t = [System.IO.File]::ReadAllText($f.FullName)
    foreach ($m in $declRx.Matches($t)) {
        $n = $m.Groups[1].Value
        if (-not $decl.ContainsKey($n)) { $decl[$n] = @{} }
        $decl[$n][$fileUnit[$f.FullName]] = $true
    }
}
$uniq = @{}
foreach ($k in $decl.Keys) { if ($decl[$k].Count -eq 1) { $uniq[$k] = ([string[]]$decl[$k].Keys)[0] } }
$names = @($uniq.Keys | Where-Object { $_.Length -ge 4 } | Sort-Object Length -Descending)
$rx = [regex]::new('(?<![A-Za-z0-9_])(' + ($names -join '|') + ')(?![A-Za-z0-9_])', [System.Text.RegularExpressions.RegexOptions]::Compiled)

'### 02Data leaks (file:line: identifier [owning unit])'
foreach ($f in ($all | Where-Object { $_.FullName -like "$root\02Data\*" })) {
    $u = $fileUnit[$f.FullName]
    $lines = [System.IO.File]::ReadAllLines($f.FullName)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^\s*(//|///|\*|/\*)') { continue }
        foreach ($m in $rx.Matches($lines[$i])) {
            $n = $m.Groups[1].Value
            $tu = $uniq[$n]
            if ($tu -and $tu -ne $u) { '{0}:{1}: {2}   [{3}]' -f $f.Name, ($i + 1), $n, $tu }
        }
    }
}
''
'### summary by target unit'
$summary = @{}
foreach ($f in ($all | Where-Object { $_.FullName -like "$root\02Data\*" })) {
    $u = $fileUnit[$f.FullName]
    $lines = [System.IO.File]::ReadAllLines($f.FullName)
    foreach ($ln in $lines) {
        if ($ln -match '^\s*(//|///|\*|/\*)') { continue }
        foreach ($m in $rx.Matches($ln)) {
            $n = $m.Groups[1].Value; $tu = $uniq[$n]
            if ($tu -and $tu -ne $u) { if ($summary.ContainsKey($tu)) { $summary[$tu]++ } else { $summary[$tu] = 1 } }
        }
    }
}
$summary.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object { '{0,-40} {1}' -f $_.Key, $_.Value }
