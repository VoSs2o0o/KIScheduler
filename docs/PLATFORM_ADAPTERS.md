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
