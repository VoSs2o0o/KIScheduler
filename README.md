# KIScheduler

KIScheduler wird eine lokale .NET-8-Windows-Forms-Anwendung zum Verwalten und Ausführen KI-generierter Arbeitspakete. Der aktuelle Stand enthält das technische Grundgerüst, das UI-unabhängige Domänenmodell, die lokale SQLite-Persistenz, einen sicheren lokalen Prozess-Runner, Projektroot-Auflösung und kontrollierte Projekterzeugung, getrennte Verträge und Registries für KI-Plattformen und Usage-Provider sowie den prioritäts-, projekt- und usage-gesteuerten Scheduler. Codex- und Claude-Aufträge werden nicht-interaktiv als JSONL ausgeführt; ihre Usage-Quellen bleiben unabhängig von der Ausführung austauschbar.

## Voraussetzungen

- Windows 10 oder neuer
- Visual Studio 2022 mit der Workload **.NET-Desktopentwicklung** oder ein .NET SDK, das .NET 8 als Ziel unterstützt

## Bauen und testen

```powershell
dotnet restore KIScheduler.sln
dotnet build KIScheduler.sln --no-restore
dotnet test KIScheduler.sln --no-build
```

Die Anwendung lässt sich anschließend mit folgendem Befehl starten:

```powershell
dotnet run --project src/KIScheduler.WinForms/KIScheduler.WinForms.csproj
```

Nicht geheime Einstellungen liegen in `src/KIScheduler.WinForms/appsettings.json`. Laufzeitprotokolle werden standardmäßig täglich unter `logs/` neben der Anwendung abgelegt und nach der konfigurierten Anzahl von Tagen bereinigt.

## Wiederanlauf und menschliche Prüfung

Beim Start werden verwaiste Reservierungen und Ausführungen atomar als `Unterbrochen` protokolliert. Für möglicherweise veränderte Projektarbeitsbäume bleibt ein Projekt-Hold aktiv, bis der Auftrag erfolgreich abgeschlossen, ausdrücklich abgebrochen oder nach externer Prüfung bewusst freigegeben wird. Die Historie zeigt Plattform, Modell, Projekt, Sitzung, Diagnose und Ereignisprotokolle; ein verfügbarer Fortsetzungsbefehl kann kopiert, wird aber nie automatisch aus der Oberfläche ausgeführt.

Pro Datenbank darf nur eine Instanz als Worker aktiv sein. Normale technische Fehlschläge werden mit dem unter `Scheduler` konfigurierten exponentiellen Backoff wiederholt und enden spätestens nach `MaximumAttempts`. `UsageExceeded` zählt dabei nicht als normaler Fehlversuch.

## Projektstruktur

- `KIScheduler.Core`: Domänenmodell, Scheduler und Verträge
- `KIScheduler.Infrastructure`: Persistenz, Prozesse, Git, Dateisystem und technische Dienste
- `KIScheduler.Platforms`: Plattformadapter
- `KIScheduler.WinForms`: Oberfläche und Composition Root
- `KIScheduler.Tests`: automatisierte Tests

Der Leitfaden [docs/PLATFORM_ADAPTERS.md](docs/PLATFORM_ADAPTERS.md) beschreibt, wie ein weiterer Plattform- und Usage-Adapter ergänzt wird.

## Codex-Integration

Die installierte Codex CLI wird über `codex exec - --json` verwendet. Prompt, Modell und Reasoning-Effort werden pro Auftrag fest übergeben. Für Usage hält `CodexUsageProvider` eine wiederverwendbare stdio-Verbindung zu `codex app-server`, liest `account/rateLimits/read` und verarbeitet `account/rateLimits/updated`. `account/usage/read` wird nicht zur Startfreigabe verwendet.

Executable, Sandbox, Timeouts, Cache-Dauer und App-Server-Argumente sind im Abschnitt `Codex` der `appsettings.json` konfigurierbar. Authentifizierungs-, Verbindungs- und Protokollfehler führen zu explizit unbekannter Usage; es gibt keinen Rückfall auf private HTTP-Endpunkte.

## Claude-Integration

`ClaudePlatform` verwendet den Print-Modus der Claude CLI mit Stream-JSON, reicht Prompt, Modell und Effort separat weiter und unterstützt die Fortsetzung anhand einer Session-ID. Ein während der Ausführung erkanntes Usage-Limit wird als `UsageExceeded` klassifiziert.

`ClaudeUsageProvider` liest einen frei konfigurierbaren Kommando-Output mit einem Regex. Standardmäßig wird `Current session: <n>% used` erkannt. Kommando, Argumente, Regex, Kultur, Einheit sowie Kommando- und Regex-Timeout sind im Abschnitt `Claude:Usage` konfigurierbar. Da Claude Code keinen stabilen maschinenlesbaren Usage-Endpunkt dokumentiert, muss der konfigurierte Usage-Befehl gegen die lokal installierte CLI-Version geprüft und bei Bedarf durch ein eigenes Probe-Kommando ersetzt werden. Eine Resetzeit kann über die benannte Regex-Gruppe `reset` oder `ConfiguredResetAtUtc` geliefert werden; ohne Resetzeit bleibt die Endspurtregel inaktiv.
