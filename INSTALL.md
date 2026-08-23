# QuizScore Live installation and release guide

End users should download the latest `ScoreSetup.exe` and `ScoreSetup.exe.sha256` from the [GitHub Releases page](https://github.com/amitkumarupadhyay1/Score/releases/latest). They do not need Visual Studio, NuGet, or administrator rights.

## Publish a downloadable installer

1. Commit and push the repository changes.
2. Create and push a version tag:

```powershell
git add .
git commit -m "Add QuizScore Live installer"
git push origin master
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions will restore the NuGet packages, build and run the production checks, create a versioned `ScoreSetup.exe`, calculate its SHA-256 checksum, and attach both files to the GitHub Release for that tag. It also keeps a downloadable workflow artifact for manual runs.

## What users do

Users download `ScoreSetup.exe` from the GitHub Release and run it. The installer creates a Start Menu shortcut and desktop shortcut, installs the complete Release output including SQLite x86/x64 libraries, and can launch QuizScore Live when installation finishes.

The app is installed per user under `%LOCALAPPDATA%\Programs\QuizScore Live`. Mutable data is stored separately under `%LOCALAPPDATA%\QuizScore Live`, so normal upgrades and uninstalls do not overwrite competition records.

The target computer must have .NET Framework 4.7.2 or later. Windows 10 and Windows 11 normally include a later compatible .NET Framework version; the installer checks this before installing.

## Build locally

From a Developer PowerShell prompt, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Configuration Release -RunChecks
```

Compile `installer\Score.iss` with Inno Setup 6 after the Release build succeeds.
