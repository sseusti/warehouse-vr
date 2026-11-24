# Windows counterpart of BuildDay1.sh.
# Usage: powershell -ExecutionPolicy Bypass -File UnityProject\Build.ps1 [tracking|interaction|both|warehouse|weight]
# Keep this file ASCII-only: Windows PowerShell 5.1 reads BOM-less scripts in the ANSI code page.
param(
    [Parameter(Position = 0)]
    [ValidateSet('tracking', 'interaction', 'both', 'warehouse', 'weight')]
    [string]$Target = 'both'
)

$ErrorActionPreference = 'Stop'

$projectDir = $PSScriptRoot
$editorPath = $env:UNITY_EDITOR_PATH
if (-not $editorPath) {
    $editorPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.85f2\Editor\Unity.exe'
}
if (-not (Test-Path -LiteralPath $editorPath -PathType Leaf)) {
    [Console]::Error.WriteLine("Unity Editor not found at $editorPath")
    exit 1
}

$buildTargets = switch ($Target) {
    'tracking'    { @('TrackingCheck') }
    'interaction' { @('InteractionCheck') }
    'both'        { @('TrackingCheck', 'InteractionCheck') }
    'warehouse'   { @('Warehouse') }
    'weight'      { @('WeightCheck') }
}

$apkNames = @{
    TrackingCheck    = 'Warehouse-Tracking.apk'
    InteractionCheck = 'Warehouse-Day1.apk'
    Warehouse        = 'Warehouse-Day2.apk'
    WeightCheck      = 'Warehouse-Weight.apk'
}

# Unity's Android tools require an ASCII-only project path.
$tempRoot = [System.IO.Path]::GetTempPath()
if ($tempRoot -match '[^\x20-\x7E]') {
    $tempRoot = Join-Path $env:SystemDrive 'Temp'
}
$stagingDir = Join-Path $tempRoot ('warehouse-vr.' + [System.IO.Path]::GetRandomFileName().Replace('.', ''))

$buildsDir = Join-Path $projectDir 'Builds'
$logsDir = Join-Path $projectDir 'Logs'

try {
    New-Item -ItemType Directory -Force -Path $stagingDir, $buildsDir, $logsDir | Out-Null
    foreach ($folder in 'Assets', 'Packages', 'ProjectSettings') {
        Copy-Item -LiteralPath (Join-Path $projectDir $folder) -Destination $stagingDir -Recurse
    }

    foreach ($buildTarget in $buildTargets) {
        $logPath = Join-Path $logsDir "Build-$buildTarget.log"
        Write-Host "Building $buildTarget. Log at $logPath"

        $unityArgs = @(
            '-batchmode', '-nographics', '-quit', '-buildTarget', 'Android',
            '-projectPath', "`"$stagingDir`"",
            '-executeMethod', "Warehouse.Editor.PicoAndroidBuild.Build$buildTarget",
            '-logFile', "`"$logPath`""
        ) -join ' '
        # Unity.exe is a GUI app: Start-Process -Wait is the reliable way to wait and get the exit code.
        $process = Start-Process -FilePath $editorPath -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow
        if ($process.ExitCode -ne 0) {
            if (Test-Path -LiteralPath $logPath) {
                Get-Content -LiteralPath $logPath -Tail 40 | ForEach-Object { [Console]::Error.WriteLine($_) }
            }
            [Console]::Error.WriteLine("Unity exited with code $($process.ExitCode) while building $buildTarget")
            exit 1
        }

        $apk = $apkNames[$buildTarget]
        $destination = Join-Path $buildsDir $apk
        Move-Item -LiteralPath (Join-Path $stagingDir "Builds\$apk") -Destination $destination -Force
        Write-Host "APK built at $destination"
    }
}
finally {
    if (Test-Path -LiteralPath $stagingDir) {
        Remove-Item -LiteralPath $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
