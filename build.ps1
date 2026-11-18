[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$RunChecks,
    [switch]$RunApp,
    [switch]$BuildInstaller,
    [string]$Version = "1.0.0"
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

if ($BuildInstaller) {
    Write-Host "Locating Inno Setup compiler..."
    $innoCandidates = @(
        (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source,
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

    $isccPath = $innoCandidates | Select-Object -First 1
    if (-not $isccPath) {
        throw "Inno Setup compiler (ISCC.exe) was not found. Install Inno Setup 6 from https://jrsoftware.org/isdl.php"
    }

    Write-Host "Building installer with Inno Setup (version $Version)..."
    $issPath = Join-Path $repositoryRoot "installer\Score.iss"
    & $isccPath "/DMyAppVersion=$Version" $issPath
    if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed with exit code $LASTEXITCODE." }

    $installerExe = Join-Path $repositoryRoot "installer-output\ScoreSetup.exe"
    if (Test-Path -LiteralPath $installerExe) {
        $hash = (Get-FileHash -Path $installerExe -Algorithm SHA256).Hash.ToLowerInvariant()
        $checksumFile = "$installerExe.sha256"
        "$hash *ScoreSetup.exe" | Set-Content -Path $checksumFile -Encoding ascii
        $fileInfo = Get-Item -LiteralPath $installerExe
        Write-Host "Installer created successfully:"
        Write-Host "  Path:     $($fileInfo.FullName)"
        Write-Host "  Size:     $([math]::Round($fileInfo.Length / 1MB, 2)) MB"
        Write-Host "  SHA256:   $hash"
    } else {
        throw "Installer output not found at $installerExe."
    }
}

if ($RunApp) {
    $appPath = Join-Path $repositoryRoot "Score\bin\$Configuration\Score.exe"
    Start-Process -FilePath $appPath
}

Write-Host "QuizScore Live is ready."
