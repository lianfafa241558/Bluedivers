$path = "d:\Pro\Bluedivers\Assets\Scripts\01Manager\Bridge\TaskManager.cs"

# Read raw bytes
$bytes = [System.IO.File]::ReadAllBytes($path)

# The file contains garbled Unicode text (UTF-8 bytes interpreted as GBK)
# To fix: encode the garbled Unicode string as GBK, then decode as UTF-8
$garbled = [System.Text.Encoding]::UTF8.GetString($bytes)
$gbkBytes = [System.Text.Encoding]::GetEncoding(936).GetBytes($garbled)
$fixed = [System.Text.Encoding]::UTF8.GetString($gbkBytes)

# Write back with UTF-8 BOM
[System.IO.File]::WriteAllText($path, $fixed, [System.Text.UTF8Encoding]::new($true))

Write-Host "Encoding fix complete"
