# Assembly/module level dependency scan: type-name based cross-unit reference counting.
# unit = nearest ancestor dir with .asmdef, otherwise top/second-level module dir
$ErrorActionPreference = 'Stop'
$root = 'd:\Pro\Bluedivers\Assets\Scripts'

$asmdefDirs = @{}
Get-ChildItem -Path $root -Recurse -Filter *.asmdef -File | ForEach-Object {
    $asmdefDirs[$_.DirectoryName] = $_.BaseName
}

function Get-Unit([string]$filePath) {
    $dirPath = Split-Path $filePath -Parent
    $d = $dirPath
    while ($d -and $d.Length -ge $root.Length) {
        if ($asmdefDirs.ContainsKey($d)) { return 'ASM:' + $asmdefDirs[$d] }
        $parent = Split-Path $d -Parent
        if (-not $parent -or $parent -eq $d) { break }
        $d = $parent
    }
    $rel = $dirPath.Substring($root.Length).TrimStart('\', '/')
    $p = $rel -split '[\\/]'
    if ($p.Count -ge 4 -and $p[0] -eq '02Game') {
        return "$($p[0])/$($p[1])/$($p[2])/$($p[3])"
    }
    if ($p.Count -ge 3 -and $p[0] -eq '02Game') {
        return "$($p[0])/$($p[1])/$($p[2])"
    }
    if ($p.Count -ge 2) { return "$($p[0])/$($p[1])" }
    return $p[0]
}

$files = @(Get-ChildItem -Path $root -Recurse -Filter *.cs -File)
$decl = @{}
$fileUnit = @{}
$declRx = [regex]'(?m)^\s*(?:\[[^\]]*\]\s*)*(?:public|internal|private|protected|sealed|abstract|static|partial|\s)*\b(?:class|struct|interface|enum)\s+([A-Za-z_]\w*)'

foreach ($f in $files) {
    $unit = Get-Unit $f.FullName
    $fileUnit[$f.FullName] = $unit
    $text = [System.IO.File]::ReadAllText($f.FullName)
    foreach ($m in $declRx.Matches($text)) {
        $n = $m.Groups[1].Value
        if (-not $decl.ContainsKey($n)) { $decl[$n] = @{} }
        $decl[$n][$unit] = $true
    }
}

$uniq = @{}
$dupCount = 0
foreach ($k in $decl.Keys) {
    if ($decl[$k].Count -eq 1) { $uniq[$k] = ([string[]]$decl[$k].Keys)[0] } else { $dupCount++ }
}

$names = @($uniq.Keys | Where-Object { $_.Length -ge 4 } | Sort-Object Length -Descending)
$pattern = '(?<![A-Za-z0-9_])(' + ($names -join '|') + ')(?![A-Za-z0-9_])'
$rx = [regex]::new($pattern, [System.Text.RegularExpressions.RegexOptions]::Compiled)

$edges = @{}
$hot = @{}
foreach ($f in $files) {
    $unit = $fileUnit[$f.FullName]
    $text = [System.IO.File]::ReadAllText($f.FullName)
    foreach ($m in $rx.Matches($text)) {
        $n = $m.Groups[1].Value
        $tu = $uniq[$n]
        if ($tu -ne $unit) {
            $k = "$unit -> $tu"
            if ($edges.ContainsKey($k)) { $edges[$k]++ } else { $edges[$k] = 1 }
            if ($hot.ContainsKey($n)) { $hot[$n]++ } else { $hot[$n] = 1 }
        }
    }
}

'### EDGES  (src -> dst : occurrences, includes comment/string noise)'
$edges.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object { '{0,-44} {1}' -f $_.Key, $_.Value }
''
'### INDEGREE  (unit : times referenced by other units)'
$in = @{}
$edges.GetEnumerator() | ForEach-Object {
    $t = $_.Key.Split(' -> ')[1]
    if ($in.ContainsKey($t)) { $in[$t] += $_.Value } else { $in[$t] = $_.Value }
}
$in.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object { '{0,-34} {1}' -f $_.Key, $_.Value }
''
'### HOT TYPES  (cross-unit referenced, type : count : owning unit)'
$hot.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 45 | ForEach-Object { '{0,-30} {1,6}  [{2}]' -f $_.Key, $_.Value, $uniq[$_.Key] }
''
'### STATS'
'cs files      : ' + $files.Count
'unique types  : ' + $uniq.Count
'ambiguous dup : ' + $dupCount
