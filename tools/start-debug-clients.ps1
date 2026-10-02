param(
    [string]$GodotPath = 'C:\Users\zebas\Downloads\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe',
    [ValidateRange(1, 4)][int]$Instances = 2
)
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
if (-not (Test-Path -LiteralPath $GodotPath)) { throw 'Pass -GodotPath pointing to your Godot .NET executable.' }
for ($index = 0; $index -lt $Instances; $index++) {
    # These game windows are interactive: create on one, type its room code on the other.
    Start-Process -FilePath $GodotPath -ArgumentList @('--path', ('"' + $projectPath + '"')) -WindowStyle Normal
}
