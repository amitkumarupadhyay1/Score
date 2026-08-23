[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$RunChecks,
    [switch]$RunApp
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionPath = Join-Path $repositoryRoot "Score.sln"
$vswherePath = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"

if (Test-Path -LiteralPath $vswherePath) {
    $msbuildPath = & $vswherePath -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
} else {
    $msbuildPath = (Get-Command msbuild.exe -ErrorAction SilentlyContinue).Source
}

if (-not $msbuildPath) {
    throw "MSBuild was not found. Install Visual Studio 2022 Build Tools with the '.NET desktop build tools' workload."
}

Write-Host "Restoring NuGet packages..."
& $msbuildPath $solutionPath /t:Restore /p:RestorePackagesConfig=true /v:minimal
if ($LASTEXITCODE -ne 0) { throw "NuGet restore failed with exit code $LASTEXITCODE." }

Write-Host "Building QuizScore Live ($Configuration)..."
& $msbuildPath $solutionPath /t:Rebuild "/p:Configuration=$Configuration" "/p:Platform=Any CPU" /warnaserror /v:minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE." }

if ($RunChecks) {
    $checksPath = Join-Path $repositoryRoot "Score.Tests\bin\$Configuration\Score.Tests.exe"
    & $checksPath
    if ($LASTEXITCODE -ne 0) { throw "Production checks failed with exit code $LASTEXITCODE." }
}

if ($RunApp) {
    $appPath = Join-Path $repositoryRoot "Score\bin\$Configuration\Score.exe"
    Start-Process -FilePath $appPath
}

Write-Host "QuizScore Live is ready."
