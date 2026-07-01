$dir = "d:/Pro/Bluedivers/Assets/Scripts"
$files = Get-ChildItem -Recurse -Filter "*.cs" $dir | Select-String -Pattern 'InspectorName\(' | Where-Object { $_.Line -match '\?' }
$files | Format-Table Path, LineNumber, @{Name="Line"; Expression={$_.Line.Trim()}} -AutoSize -Wrap
Write-Host ("`nTotal: " + $files.Count)
