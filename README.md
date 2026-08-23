# QuizScore Live

QuizScore Live is a fast, local-first Windows scoring desk for quizzes, school competitions, and live team events. It combines animated team cards, round and response timers, standings, undoable scoring, persistent sessions, and a spreadsheet-friendly audit export in one operator-focused screen.

![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-1877F2) ![License](https://img.shields.io/badge/license-MIT-62c48d)

## Install in under a minute

1. Open the [latest GitHub release](https://github.com/amitkumarupadhyay1/Score/releases/latest).
2. Download `ScoreSetup.exe` and the accompanying `ScoreSetup.exe.sha256`.
3. Optionally verify the download:

   ```powershell
   (Get-FileHash .\ScoreSetup.exe -Algorithm SHA256).Hash.ToLower()
   Get-Content .\ScoreSetup.exe.sha256
   ```

4. Run the installer, then open **QuizScore Live** from the Start menu or desktop.

The installer is per-user and does not require administrator access. Windows 10 and Windows 11 include a compatible .NET Framework in normal installations.

## Run a competition

1. Enter the event title, choose 2–12 teams and 1–10 rounds, then edit team and player names directly on the cards.
2. Choose optional event and round time limits and select **START LIVE**.
3. Award `+1`, `+2`, or `+5`, apply `−1` penalties, and use the team response timer when needed.
4. Select **FINISH ROUND**, review the recorded result, and continue to the next round.
5. Open **STANDINGS** at any time or **EXPORT LOG** to save the full competition record.

The board respects the available viewport: it changes card columns and height automatically as teams are added, while overflow remains inside the scrollable board. Results with many teams scroll inside their overlay instead of growing beyond the screen.

Useful shortcuts:

- `Space`: pause or resume a live session
- `Ctrl+Z`: undo the latest score change in the current unfinalized round
- `Ctrl+E`: export the audit log
- `F11`: toggle maximized presentation view
- `Esc`: close preview, standings, final-result, or About overlays when safe

## Reliability and data

QuizScore Live is local-only; it does not require an account, server, or internet connection after installation.

- Scores and sessions: `%LOCALAPPDATA%\QuizScore Live\Data\ScoreDB.sqlite`
- Daily rolling backups: `%LOCALAPPDATA%\QuizScore Live\Backups`
- Preferences and team names: `%LOCALAPPDATA%\QuizScore Live\Settings`
- Diagnostic log: `%LOCALAPPDATA%\QuizScore Live\Logs\QuizScoreLive.log`

Scores and their matching audit entries are written in one database transaction. The database uses full SQLite synchronization, an integrity check at startup, a busy timeout, single-instance protection, and up to 14 daily backups. If the app was interrupted during a live game, it restores the session in a paused state so the operator can verify it before continuing.

Upgrading or uninstalling the program does not intentionally delete the data folders above. Back up that folder before manually removing or transferring data.

## Build from source

Requirements:

- Windows 10 or Windows 11
- Visual Studio 2022 or Build Tools with **.NET desktop build tools**
- PowerShell 5.1 or later

Clone and run:

```powershell
git clone https://github.com/amitkumarupadhyay1/Score.git
cd Score
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Configuration Release -RunChecks -RunApp
```

The script locates MSBuild, restores NuGet packages, builds the solution, runs the production checks, and starts the Release app. The executable is written to `Score\bin\Release\Score.exe`.

Developers can set `QUIZSCORE_HOME` to an absolute temporary folder before launching the app to keep test sessions isolated from their normal local data.

## Contributing and security

Pull requests should keep the Release build and `Score.Tests` checks green. Please report security issues privately using the process in [SECURITY.md](SECURITY.md), not a public issue.

QuizScore Live is available under the [MIT License](LICENSE).
