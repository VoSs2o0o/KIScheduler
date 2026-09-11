# KIScheduler

KIScheduler wird eine lokale .NET-8-Windows-Forms-Anwendung zum Verwalten und Ausführen KI-generierter Arbeitspakete. Der aktuelle Stand enthält das technische Grundgerüst, das UI-unabhängige Domänenmodell, die lokale SQLite-Persistenz, einen sicheren lokalen Prozess-Runner, Projektroot-Auflösung und kontrollierte Projekterzeugung, getrennte Verträge und Registries für KI-Plattformen und Usage-Provider sowie den prioritäts-, projekt- und usage-gesteuerten Scheduler. Codex-Aufträge werden nicht-interaktiv als JSONL ausgeführt; Rate-Limits stammen getrennt davon aus dem dokumentierten Codex App Server.

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
