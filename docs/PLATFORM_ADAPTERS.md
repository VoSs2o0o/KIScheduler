# Einen weiteren Plattformadapter ergänzen

Ausführung und Usage-Ermittlung sind bewusst getrennte Erweiterungspunkte. Ein Adapterpaket implementiert daher in der Regel zwei voneinander unabhängige Klassen mit derselben stabilen `PlatformId`:

1. `IAiPlatform` prüft die lokale Verfügbarkeit, beschreibt seine Fähigkeiten und führt Aufträge aus. Es ordnet ein während der Ausführung erreichtes Nutzungslimit dem Ergebnis `PlatformExecutionOutcome.UsageExceeded` zu und setzt `MayHavePartialChanges`, wenn der Arbeitsbaum bereits verändert worden sein kann.
2. `IUsageProvider` liest ausschließlich Usage-Daten. Er muss `forceRefresh` respektieren. Push-fähige Quellen können zusätzlich `UsageChanged` auslösen; reine Polling-Provider lassen das Ereignis ungenutzt.
3. Beide Klassen werden separat registriert:

```csharp
services.AddKischedulerPlatformServices();
services.AddAiPlatform<ExamplePlatform>();
services.AddUsageProvider<ExampleUsageProvider>();
```

Die Plattformkonfiguration enthält Executable, Modelle und erlaubte Effort-Stufen. Beim Start wird `ValidateRegistrations` für alle aktivierten Definitionen aufgerufen. Vor jeder Ausführung prüft `ValidateExecution` Plattform-ID, Modell, Effort und Resume-Fähigkeit. `CheckHealthAsync` verbindet den Installationstest des Adapters mit der Modellkonfiguration und meldet fehlende Executables oder nicht unterstützte Modelle, bevor ein Prozess gestartet wird.

Ein Adapter darf weder WinForms referenzieren noch Shell-Befehle zusammensetzen. CLI-Aufrufe verwenden den zentralen `IProcessRunner`; Prompts werden nach Möglichkeit über stdin übergeben. Strukturierte Fehlerdaten haben bei der Erkennung von Usage-Limits Vorrang vor Textmustern.

Für Tests stehen `FakeAiPlatform` und `FakeUsageProvider` bereit. Der Plattform-Fake akzeptiert verzögerte oder vollständig benutzerdefinierte Ausführungen und misst Parallelität. Der Usage-Fake kann Leseergebnisse in eine Warteschlange stellen und Push-Ereignisse veröffentlichen. Damit lassen sich insbesondere ein Usage-Abbruch mit partiellen Änderungen und die anschließende Sperrlogik deterministisch prüfen.

Der Codex-Adapter dient als Referenz für eine strukturierte Integration. `CodexPlatform` verarbeitet die JSONL-Ereignisse von `codex exec` und gibt Ereignisse, Session-ID, Abschlussnachricht und Fehlerklasse getrennt zurück. Jede Versions-, Anmelde-, Ausführungs- und Resume-Anfrage setzt `CODEX_HOME` und `CODEX_SQLITE_HOME` ausdrücklich auf den gespeicherten Ordner des gewählten Profils; geerbte Providerwerte werden nicht verwendet.

`CodexUsageProvider` kennt keine Prozessdetails. Er bezieht seinen Transport aus der profilbezogenen `ICodexAppServerClientFactory`, die höchstens eine wiederverwendbare Verbindung pro aktiv verwendetem Profil hält. Profiländerungen und Deaktivierungen werden über `InvalidateAsync` freigegeben; beim Anwendungsende werden alle verbleibenden Prozesse kontrolliert beendet. Reads und Push-Ereignisse tragen die verursachende Profil-ID.

Die Codex-Profilprüfung verwendet ausschließlich `codex login status`. KIScheduler öffnet, protokolliert, kopiert, migriert oder löscht weder `auth.json` noch andere Credential-Dateien. Für zuverlässig getrennte Logins muss Codex in jedem `CODEX_HOME` den dateibasierten Credential-Store verwenden. Fehlt der Profilordner, ist er nicht lesbar oder meldet die CLI keine gültige Anmeldung, wird das Profil mit einer Diagnose abgelehnt.

Der Claude-Adapter zeigt die Trennung bei einer textbasierten Usage-Quelle. `ClaudePlatform` verarbeitet Stream-JSON aus dem nicht-interaktiven Print-Modus. `ClaudeUsageProvider` normalisiert dagegen ausschließlich das Ergebnis des generischen `CommandRegexReader`. Über `TestSample` kann die konfigurierte Beispielausgabe ohne Prozessstart gegen Match und normalisierten Snapshot geprüft werden. Ein fehlender Match ist unbekannte Usage und niemals ein Messwert von null Prozent.

Claude-Ausführung, Resume, Versionsprüfung, `claude auth status` und Usage-Abfrage setzen bei einem profilbezogenen Aufruf ausdrücklich `CLAUDE_CONFIG_DIR` auf den gespeicherten Profilordner. Die Umgebung des Elternprozesses wird nicht als Profil-Fallback verwendet. Usage-Ergebnisse enthalten die ID des abgefragten Profils; fehlende oder nicht lesbare Profilordner sowie eine ungültige Anmeldung werden mit einer klaren Diagnose gemeldet. KIScheduler öffnet oder protokolliert keine Credential-Dateien oder deren Inhalte. Getrennte Anmeldungen benötigen daher jeweils einen eigenen Claude-Konfigurationsordner.
