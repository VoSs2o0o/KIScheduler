# KIScheduler

KIScheduler wird eine lokale .NET-8-Windows-Forms-Anwendung zum Verwalten und Ausführen KI-generierter Arbeitspakete. Der aktuelle Stand enthält das technische Grundgerüst, das UI-unabhängige Domänenmodell, die lokale SQLite-Persistenz und einen sicheren lokalen Prozess-Runner mit Timeout, Abbruch, Prozessbaumbehandlung und maskierter Protokollierung; Scheduler- und plattformspezifische CLI-Adapter folgen in späteren Arbeitspaketen.

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
