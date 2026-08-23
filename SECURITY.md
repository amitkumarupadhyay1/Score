# Security policy

## Supported version

Security fixes are applied to the latest release of QuizScore Live. Upgrade to the newest GitHub release before reporting an issue that may already be fixed.

## Report a vulnerability

Please use the repository's **Security** tab and open a private security advisory. Include the affected version, Windows version, reproduction steps, impact, and any proof-of-concept files. Do not include real student or participant data, and do not publish a security issue before a fix is available.

## Data and trust boundaries

QuizScore Live is a local desktop application. It does not send scores, participant names, or telemetry to a remote service. Its database, settings, backups, and diagnostic logs are stored under the current user's `%LOCALAPPDATA%\QuizScore Live` folder and inherit that Windows account's file permissions.

The files are not encrypted by the application. Anyone who can access the Windows account or its files may be able to read participant names and competition records. Use Windows device encryption, a protected account, and the minimum participant information your event requires.

CSV exports neutralize common spreadsheet formulas, but exported files should still be opened only on a trusted computer. Official releases include a SHA-256 checksum. Until releases are code-signed, Windows may show a SmartScreen warning; verify the checksum and download only from this repository's Releases page.
