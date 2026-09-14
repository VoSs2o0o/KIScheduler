# AP18 – Testmatrix für Plattformprofile

Stand: 2026-09-14

Diese Matrix ordnet die Release-Anforderungen ihrem automatischen oder manuellen Nachweis zu. Die
automatischen Tests verwenden ausschließlich temporäre Verzeichnisse und Fake-CLIs. Dateien namens
`auth.json` und `.credentials.json` dienen nur als Wächterdateien mit Testwerten; echte Anmeldedaten
werden weder benötigt noch in Artefakte übernommen.

| Anforderung | Automatischer Nachweis | Ergänzender manueller Nachweis |
|---|---|---|
| Migration vor AP13 | `MigrationAssignsExistingRowsToDefaultProfilesWithoutDataLoss` | Restore einer Sicherung in Schritt 12 |
| zwei Codex-Profile, Umgebung und Usage | `TwoCodexProfilesKeepExecutionUsageAndInheritedProviderEnvironmentSeparated` | Schritte 18–21 |
| zwei Claude-Profile, Umgebung und Usage | `TwoClaudeProfilesKeepExecutionAndUsageResponsesSeparated` | Schritt 22 |
| geerbte Provider-Variablen | `DefaultDirectoriesComeOnlyFromUserProfileAndAreNormalized` und Codex-E2E-Test | Profilprüfung in frischen Shells |
| profilbezogene Sperre, Zweitprofil, Resume | `UsageBlockOnlyStopsItsProfileAndResumeKeepsOriginalProfileAndSession` | Schritte 20–21 |
| Retry, UsageExceeded und Projekt-Hold | Scheduler-Tests `UsageExceededCreatesBothBlocksAndOnlyFreshUsageUnlocksResume` und `FailedExecutionUsesBackoffAndStopsAtConfiguredMaximumAttempts` | sichtbaren Hold in Schritt 21 prüfen |
| deaktivierte/fehlende Profile | `DisabledProfileIsDiagnosedWithoutUsageReadOrDispatch` und `MissingAndCrossPlatformProfilesAreSafelyDiagnosed` | Schritte 16–17 |
| Umbenennung, Standardwechsel, Entfernen ohne Dateioperation | `RenameDefaultChangeAndRemovalNeverTouchProfileOrCredentialFiles` | Schritte 17 und 23 |
| Codex-App-Server-Lifecycle | `AppServerFactoryReusesPerProfileAndStopsClientsOnChangeAndDisable` | Anwendung nach Profiländerung kontrolliert beenden |
| Statusleiste | `ProfileStatusBarFormatsNoOneAndMultipleSelectedProfilesExactly` | Schritt 14 |
| Diagnose- und Credential-Sicherheit | Logging-, Resolver- und beide Profil-E2E-Tests | Schritt 24 |

Das verbindliche reale Provider-Protokoll steht in `RELEASE_CHECKLIST.md`. Ein automatischer grüner
Testlauf ersetzt die dort verlangte Prüfung mit den konkret auszuliefernden CLI-Versionen nicht.
