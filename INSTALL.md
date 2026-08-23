# QuizScore Live installer

## Publish a downloadable installer

1. Commit and push the repository changes.
2. Create and push a version tag:

```powershell
git add .
git commit -m "Add QuizScore Live installer"
git push origin main
git tag v1.0.0
git push origin v1.0.0
```

GitHub Actions will restore the NuGet packages, build the Release application, create `ScoreSetup.exe`, and attach it to the GitHub Release for that tag. It also keeps a downloadable workflow artifact for manual runs.

## What users do

Users download `ScoreSetup.exe` from the GitHub Release and run it. The installer creates a Start Menu shortcut and desktop shortcut, installs the complete Release output including SQLite x86/x64 libraries, and can launch QuizScore Live when installation finishes.

The app is installed per user under `%LOCALAPPDATA%\Programs\QuizScore Live`, so it can create its SQLite database and audit logs without administrator permissions. Existing quiz data is preserved when the app is upgraded in place.

The target computer must have .NET Framework 4.7.2 or later. Windows 10 and Windows 11 normally include a later compatible .NET Framework version; the installer checks this before installing.
