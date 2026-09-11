# KIScheduler – Umsetzungsplan

Stand: 2026-09-11

## 1. Ziel

KIScheduler ist eine lokale .NET-8-Windows-Forms-Anwendung für Visual Studio 2022. Die Anwendung verwaltet KI-generierte Arbeitspakete als Prompt-Dateien, wählt ausführbare Aufträge anhand von Priorität und verfügbarer KI-Nutzung aus und startet sie nicht-interaktiv über Codex CLI oder Claude CLI.

Die Anwendung arbeitet ohne Administratorrechte. In Version 1 läuft der Worker innerhalb der WinForms-Anwendung. Das Fenster kann in den Infobereich (Tray) minimiert werden; beim vollständigen Beenden stoppt auch die Verarbeitung.

## 2. Festgelegte Produktentscheidungen

- Persistenz: lokale SQLite-Datenbank über Entity Framework Core.
- Oberfläche: .NET 8 Windows Forms, kompatibel mit Visual Studio 2022.
- Plattformen in Version 1: Codex CLI und Claude CLI.
- Plattform und Modell sind pro Auftrag fest vorgegeben. Es gibt keinen automatischen Wechsel auf eine andere Plattform oder ein anderes Modell.
- Pro Plattform darf höchstens ein Auftrag gleichzeitig laufen.
- Codex und Claude dürfen parallel laufen, sofern sie nicht dasselbe Projekt sperren.
- In demselben Projekt darf höchstens ein schreibender Auftrag gleichzeitig laufen, auch wenn unterschiedliche Plattformen verwendet werden.
- Die Priorität entscheidet nur unter den derzeit ausführbaren Aufträgen. Ein wegen Usage blockierter Codex-Auftrag blockiert keinen ausführbaren Claude-Auftrag.
- Ein Auftrag gilt technisch als erfolgreich, wenn der CLI-Prozess mit Exitcode 0 endet und eine gegebenenfalls aktivierte Abschlussaktion, etwa der Git-Commit, erfolgreich war.
- Die fachliche Qualität wird in Version 1 nicht automatisch bewertet.
- Git-Ausführung erfolgt auf dem konfigurierten Zielbranch, standardmäßig `master`. Optional kann pro Auftrag nach erfolgreicher Ausführung ein eigener Commit erzeugt werden. Es werden keine Arbeitsbranches angelegt.
- Rückfragen oder unklare CLI-Fehler werden nicht in einer eigenen Chat-Oberfläche beantwortet. Der Auftrag wechselt zu `MenschlichePruefung`; die Anwendung zeigt Plattform, Sitzung, Logs und einen Fortsetzungsbefehl an. Nach externer Klärung kann der Auftrag erneut eingereiht werden.

## 3. Prompt- und Projektkonvention

Typischer Prompt-Pfad:

```text
<Projektroot>\docs\000_AP0.md
<Projektroot>\docprompts\010_AP10.md
```

Projektauflösung:

1. Liegt die Prompt-Datei direkt oder in einem Unterordner eines Verzeichnisses namens `docs` oder `docprompts`, ist dessen übergeordnetes Verzeichnis das Projektroot.
2. Die Suche läuft vom Prompt-Verzeichnis nach oben und verwendet den nächstgelegenen passenden Ordner.
3. Groß-/Kleinschreibung wird unter Windows ignoriert.
4. Gibt es keinen Treffer, wechselt der Auftrag zu `ProjektFehlt` und das Projektroot wird separat abgefragt.
5. Die bestätigte Auflösung wird am Auftrag gespeichert. Ein späteres Verschieben der Prompt-Datei löst eine erneute Prüfung aus.
6. Existiert das gewählte Projektroot noch nicht, bietet ein Assistent die Erzeugung mittels einer konfigurierten `dotnet new`-Vorlage an.

Der Dateiname wird nicht zum Speichern eines vollständigen Windows-Pfads verwendet.

## 4. Lösungsstruktur

```text
KIScheduler.sln
src/
  KIScheduler.WinForms/       Oberfläche, Tray, Dialoge
  KIScheduler.Core/           Domänenmodell, Scheduler, Verträge
  KIScheduler.Infrastructure/ SQLite, Prozesse, Git, Dateisystem
  KIScheduler.Platforms/      Codex-, Claude- und Testadapter
tests/
  KIScheduler.Tests/
docs/
  PLAN.md
  000_AP0.md ... 012_AP12.md
```

Für Hosting, Dependency Injection, Konfiguration und Logging werden die üblichen `Microsoft.Extensions.*`-Bausteine verwendet. Der Worker ist ein innerhalb der WinForms-Anwendung gehosteter Hintergrunddienst.

## 5. Zentrale Komponenten

### 5.1 Domäne

- `WorkItem`: Auftrag und aktuelle Planung
- `ExecutionAttempt`: unveränderliche Historie eines Ausführungsversuchs
- `ProjectDefinition`: Projektroot, Zielbranch und optionale Prüfkommandos
- `PlatformDefinition`: CLI, Modelle, Effort-Stufen und Kapazität
- `UsagePolicy`: zeitabhängige Verbrauchsgrenze
- `UsageSnapshot`: gelesene Usage-Fenster und Resetzeitpunkte
- `PlatformUsageBlock`: temporäre Plattformsperre nach einem serverseitig gemeldeten Usage-Limit
- `ProjectExecutionHold`: projektweite Sperre nach einer unterbrochenen, möglicherweise nur teilweise ausgeführten Aufgabe
- `SchedulerLease`: atomare Reservierung eines Auftrags
- `ExecutionEvent`: strukturierte Ereignisse und Diagnose

### 5.2 Plattformen und Usage-Provider

Ausführung und Usage-Ermittlung sind zwei unabhängige Erweiterungspunkte:

- `IAiPlatform` kapselt Installationstest, Aufruf, Ausgabeauswertung, Abbruch und gegebenenfalls Sitzungsfortsetzung.
- `IUsageProvider` liest ausschließlich Kontingentfenster, Resetzeitpunkte und erreichte Limits. Er startet keine KI-Aufträge.

Beide Implementierungen werden über dieselbe stabile `PlatformId` verbunden und separat über Dependency Injection registriert. Dadurch kann sich beispielsweise die Claude-Usage-Ausgabe ändern, ohne die Claude-Ausführungsklasse anzupassen.

Vorgesehene Verträge:

```csharp
public interface IAiPlatform
{
    string PlatformId { get; }
    PlatformCapabilities Capabilities { get; }
    Task<PlatformHealth> CheckAvailabilityAsync(CancellationToken cancellationToken);
    Task<ExecutionResult> ExecuteAsync(ExecutionRequest request, CancellationToken cancellationToken);
}

public interface IUsageProvider
{
    string PlatformId { get; }
    UsageProviderCapabilities Capabilities { get; }
    Task<UsageReadResult> ReadAsync(bool forceRefresh, CancellationToken cancellationToken);
    event EventHandler<UsageChangedEventArgs>? UsageChanged;
}
```

Konkrete Implementierungen in Version 1:

| Plattform | Ausführung | Usage |
|---|---|---|
| Codex | `CodexPlatform : IAiPlatform` | `CodexUsageProvider : IUsageProvider` |
| Claude | `ClaudePlatform : IAiPlatform` | `ClaudeUsageProvider : IUsageProvider` |

Das `UsageChanged`-Ereignis ist optional in der Verwendung: `CodexUsageProvider` kann App-Server-Benachrichtigungen weiterreichen, während `ClaudeUsageProvider` in Version 1 primär abgefragt wird. Unabhängig davon kann der Scheduler vor einem Start mit `forceRefresh: true` eine aktuelle Messung verlangen.

`ProcessStartInfo.ArgumentList` wird anstelle zusammengesetzter Shell-Befehle verwendet. Prompts werden bevorzugt über stdin übergeben. stdout, stderr und strukturierte Ereignisse werden vollständig und asynchron gelesen.

## 6. Statusmodell

```text
Entwurf
InWarteschlange
Reserviert
InBearbeitung
WartetAufUsage
ProjektFehlt
Pausiert
MenschlichePruefung
TechnischErfolgreich
ErfolgreichMitWarnung
Fehlgeschlagen
Abgebrochen
Unterbrochen
```

`ProjektAngehalten` ist ein abgeleiteter Anzeigezustand für wartende Aufträge, deren Projekt durch einen `ProjectExecutionHold` gesperrt ist. Die ursprünglichen Auftragsstatus werden dabei nicht massenhaft verändert und müssen später nicht rekonstruiert werden.

Pragmatische Erfolgsregel:

- `TechnischErfolgreich`: CLI-Exitcode 0; bei aktiviertem Auto-Commit wurde der Commit erfolgreich erzeugt.
- `ErfolgreichMitWarnung`: CLI-Exitcode 0, aber eine nicht zwingende Plausibilitätsprüfung meldet etwas Auffälliges, zum Beispiel keine Dateiänderung oder ein optionaler Test schlägt fehl.
- `MenschlichePruefung`: Startfehler, Timeout, nicht interpretierbare Ausgabe, Authentifizierungsproblem, interaktive Rückfrage oder Git-Konflikt.
- `Fehlgeschlagen`: eindeutiger nicht erfolgreicher CLI-Exitcode oder ausgeschöpfte Wiederholungen.
- `UsageExceeded` ist ein eigenes Ergebnis eines Ausführungsversuchs. Der Auftrag wird nicht als fachlich fehlgeschlagen gewertet, sondern wechselt zu `WartetAufUsage` und hält sein Projekt bis zur Fortsetzung oder manuellen Auflösung gesperrt.

Diese Trennung bleibt fast so einfach wie „Exitcode 0 = erfolgreich“, macht aber stille technische Auffälligkeiten sichtbar, ohne eine zweite KI-Bewertung einzuführen.

## 7. Usage-Regeln

Zeitregeln verwenden halb-offene Intervalle, beispielsweise:

```text
[00:00, 08:00) -> MaxUsedPercent 100
[08:00, 20:00) -> MaxUsedPercent 75
[20:00, 24:00) -> MaxUsedPercent 100
```

Jede Regel kann auf Plattform und optional Modell eingeschränkt werden und besitzt:

- Wochentage, lokale Start- und Endzeit sowie Zeitzone
- regulär maximal erlaubten Verbrauch in Prozent
- optional `EndspurtDauer`, beispielsweise 30 Minuten
- optional `EndspurtMaxUsedPercent`, beispielsweise 100 Prozent
- Verhalten bei unbekannter oder veralteter Usage
- Wiederholungsintervall für die nächste Abfrage

### 7.1 Endspurtlogik

Für jedes gemeldete Usage-Fenster wird die verbleibende Zeit bis `ResetAt` berechnet.

```text
effektive Grenze =
    EndspurtMaxUsedPercent, wenn ZeitBisReset <= EndspurtDauer
    sonst MaxUsedPercent
```

Beispiel: Tagsüber gilt 75 Prozent. Meldet das relevante Fenster einen Reset in höchstens 30 Minuten, steigt die Grenze für dieses Fenster auf 100 Prozent.

Bei mehreren relevanten Usage-Fenstern müssen alle Fenster ihren jeweils wirksamen Grenzwert einhalten. Fehlt `ResetAt`, kann keine Endspurtregel angewandt werden. Die Zeitberechnung erfolgt intern mit UTC; Anzeige und Zeitregeln verwenden die konfigurierte lokale Zeitzone.

Die Grenze ist immer exklusiv:

```text
Auftrag darf starten <=> UsedPercent < EffektiveGrenze
```

Damit ist bei einer Grenze von 75 bereits `75 %` blockiert. Auch eine Endspurtgrenze von `100 %` erlaubt nur Werte unter 100; bei exakt `100 %` darf kein neuer Auftrag starten. Ein serverseitig gesetztes `RateLimitReachedType` blockiert unabhängig vom gerundeten Prozentwert.

### 7.2 Codex

`CodexPlatform` verwendet die aktuelle nicht-interaktive CLI und bevorzugt strukturierte JSONL-Ausgabe. Für Usage wird dieselbe dokumentierte Semantik wie in der Codex-Oberfläche verwendet: Ein eigener `CodexUsageProvider` spricht über einen internen `ICodexAppServerClient` mit dem Codex App Server und liest zunächst `account/rateLimits/read`. Danach verarbeitet er `account/rateLimits/updated`-Benachrichtigungen und führt in angemessenen Abständen beziehungsweise vor einem Start eine erneute Abfrage aus.

Auswertung:

- `rateLimitsByLimitId` ist die bevorzugte Mehrfach-Bucket-Ansicht.
- `rateLimits` dient nur als rückwärtskompatibler Fallback.
- `primary` und `secondary` werden als getrennte Usage-Fenster normalisiert.
- `usedPercent`, `windowDurationMins`, `resetsAt` und `rateLimitReachedType` bleiben erhalten.
- `account/usage/read` wird nicht für die Startentscheidung benutzt; es enthält Token-Aktivitätsstatistiken, nicht die Rate-Limit-Freigabe.
- Der Provider läuft als separate Klasse hinter `IUsageProvider`; App-Server-Transport und Usage-Normalisierung werden ebenfalls getrennt, damit Protokolländerungen lokal begrenzt bleiben.
- Wenn der App Server oder die passende Codex-Authentifizierung nicht verfügbar ist, lautet das Ergebnis `UsageUnbekannt`. Es wird nicht auf inoffizielle private HTTP-Endpunkte ausgewichen.

### 7.3 Claude

`ClaudePlatform` kapselt ausschließlich die Auftragsausführung. `ClaudeUsageProvider` ist eine eigene `IUsageProvider`-Implementierung und verwendet intern einen wiederverwendbaren, konfigurierbaren Kommando-/Regex-Reader. Der vorgeschlagene Ausdruck wird robust normalisiert zu:

```regex
Current session:\s*(?<used>\d+)%\s*used
```

Der reguläre Ausdruck besitzt einen kurzen Timeout. Beispielausgabe und Regex können in den Einstellungen getestet werden. Für die Endspurtlogik muss zusätzlich ein Resetzeitpunkt gelesen oder anderweitig konfiguriert werden.

## 8. Scheduling und Parallelität

Der Scheduler arbeitet in kurzen, konfigurierbaren Intervallen:

1. Plattformzustand und hinreichend alte Usage-Snapshots aktualisieren.
2. fällige Aufträge aus `InWarteschlange` und `WartetAufUsage` laden.
3. feste Plattform, Modell, Projekt, Abhängigkeiten und Zeitregeln prüfen.
4. Aufträge für eine bereits belegte Plattform ausschließen.
5. Aufträge für ein bereits belegtes Projekt ausschließen.
6. pro freier Plattform den Auftrag mit höchster effektiver Priorität reservieren.
7. Codex- und Claude-Auftrag gegebenenfalls parallel starten.

Um dauerhaftes Verhungern zu verhindern:

```text
EffektivePriorität = Basispriorität + konfigurierbarer Wartezeitbonus
```

Die Reservierung und Statusänderung werden in einer SQLite-Transaktion gespeichert. Nach einem Absturz werden verwaiste Zustände `Reserviert` oder `InBearbeitung` beim nächsten Start zu `Unterbrochen`.

### 8.1 Usage-Limit während einer Ausführung

Zwischen letzter Usage-Abfrage und tatsächlicher Ausführung kann das Limit erreicht werden. Ein Adapter muss deshalb `UsageExceeded` als eigene Fehlerklasse erkennen. Strukturierte Plattformdaten haben Vorrang vor Textmustern; Textmuster dienen nur als dokumentierter Fallback.

Bei `UsageExceeded` gelten atomar folgende Regeln:

1. Der Ausführungsversuch endet mit Ergebnis `UsageExceeded` und erzeugt ein Ereignis der Schwere `Error`; ein vorhandener nicht-null CLI-Exitcode wird zusätzlich gespeichert.
2. Der Auftrag wechselt zu `WartetAufUsage`. Der normale Fehler-/Retry-Zähler wird nicht erhöht.
3. Es wird kein Auto-Commit ausgeführt, da die Aufgabe unvollständig sein kann.
4. Der aktuelle Usage-Snapshot wird sofort verworfen und neu abgefragt.
5. Für die betroffene Plattform wird ein `PlatformUsageBlock` gesetzt. Dadurch starten auf dieser Plattform in keinem Projekt weitere Aufträge, solange das Limit serverseitig erreicht ist beziehungsweise die effektive Grenze nicht unterschritten wird.
6. Für das betroffene Projekt wird ein `ProjectExecutionHold` gesetzt. Dadurch starten auch über die andere Plattform keine weiteren Prompts dieses Projekts, weil der Arbeitsbaum teilweise verändert worden sein kann.
7. Andere Plattformen dürfen in anderen Projekten weiterarbeiten.
8. Nach dem Reset genügt die Uhrzeit allein nicht: Erst ein frischer Usage-Snapshot mit `UsedPercent < EffektiveGrenze` und ohne erreichten Limitstatus hebt den Plattformblock auf.
9. Der unterbrochene Auftrag hat Vorrang und wird in derselben Plattform-Sitzung fortgesetzt, sofern eine Sitzungs-ID und Resume-Unterstützung vorhanden sind.
10. Der Projekt-Hold wird erst nach erfolgreichem Abschluss, ausdrücklichem Abbruch oder manueller Freigabe aufgehoben. Ist keine sichere Fortsetzung möglich, wechselt der Auftrag zu `MenschlichePruefung` und der Projekt-Hold bleibt bestehen.

## 9. Git-Verhalten

- Zielbranch pro Projekt, Standard `master`.
- Auto-Commit pro Auftrag ein-/ausschaltbar.
- Bei aktiviertem Auto-Commit muss der Arbeitsbaum vor dem Start sauber sein.
- Die Anwendung wechselt den Branch nicht automatisch, wenn lokale Änderungen vorliegen.
- Nach erfolgreichem CLI-Lauf: Änderungen feststellen, `git add -A`, Commit mit konfigurierbarer Nachricht erzeugen.
- Gibt es keine Änderungen, wird kein leerer Commit erzeugt; der Auftrag endet als `ErfolgreichMitWarnung`.
- Scheitert der verpflichtende Commit, endet der Auftrag in `MenschlichePruefung` und wird nicht als technisch erfolgreich behandelt.
- Ohne Auto-Commit wird der Git-Zustand nur protokolliert.

## 10. Oberfläche

Hauptbereiche:

- Warteschlange mit Priorität, Plattform, Modell, Effort, Projekt, Usage und Status
- Auftragseditor mit Auto-Commit-Option
- Live-Ansicht für stdout, stderr und Ereignisse
- Plattformübersicht mit Zustand, aktuellem Verbrauch und Reset
- sichtbare Plattform- und Projektsperren mit Ursache, auslösendem Auftrag und möglicher Freigabezeit
- Projektroot-Dialog und Assistent für neue Projekte
- Usage-Regel-Editor einschließlich Endspurt
- Einstellungen für CLI, Modelle, Regex/JSON-Provider und Polling
- Verlauf der Ausführungsversuche
- Aktion „In Plattform prüfen“ beziehungsweise Anzeige/Kopieren des Fortsetzungsbefehls
- Tray-Menü: Öffnen, Verarbeitung pausieren/fortsetzen, laufende Aufträge anzeigen, Beenden

Beim Schließen über das Fensterkreuz wird standardmäßig in den Tray minimiert. Ein ausdrücklicher Menüpunkt beendet die Anwendung kontrolliert.

## 11. Sicherheit und Betrieb

- Keine Administratorrechte und kein Windows-Dienst in Version 1.
- Keine frei zusammengesetzten Shell-Kommandos; ausführbare Datei und Argumente werden separat gespeichert.
- Geheimnisse nicht im Klartext in SQLite speichern. Vorhandene CLI-Anmeldungen werden bevorzugt wiederverwendet.
- Logs dürfen keine Tokens oder Authentifizierungsheader enthalten.
- Abbruch beendet den gesamten gestarteten Prozessbaum.
- Pro Auftrag gelten Timeout, maximale Versuche und Wiederholungsverzögerung.
- SQLite wird mit Migrationen, Foreign Keys und WAL-Modus betrieben.
- Nur eine KIScheduler-Instanz darf dieselbe Datenbank als aktiver Worker verwenden.

## 12. Nichtziele der ersten Version

- Windows-Dienst oder Ausführung nach Benutzerabmeldung
- automatische Wahl einer anderen KI-Plattform
- automatische fachliche Bewertung durch eine zweite KI
- eigene Chat-Oberfläche für Rückfragen
- mehrere parallele Aufträge derselben Plattform
- automatische Feature-Branches oder Worktrees
- verteilte Verarbeitung auf mehreren Rechnern

## 13. Arbeitspakete

| Datei | Thema | Abhängigkeit | Empfohlene KI |
|---|---|---|---|
| `000_AP0.md` | Solution-Grundgerüst und technische Basis | keine | GPT-5.6 Sol, medium |
| `001_AP1.md` | Domänenmodell und Statusautomat | AP0 | GPT-5.6 Sol, medium |
| `002_AP2.md` | SQLite-Persistenz | AP1 | GPT-5.6 Sol, medium |
| `003_AP3.md` | Sicherer Prozess-Runner | AP0 | GPT-5.6 Sol, high |
| `004_AP4.md` | Plattform- und Usage-Provider-Verträge | AP1, AP3 | GPT-5.6 Sol, medium |
| `005_AP5.md` | Projektroot-Auflösung und Projekterzeugung | AP1 | GPT-5.6 Sol, medium |
| `006_AP6.md` | Usage-Regeln und Scheduler | AP2, AP4, AP5 | GPT-5.6 Sol, high |
| `007_AP7.md` | Codex-Adapter und App-Server-Usage | AP4, AP6 | GPT-5.6 Sol, high |
| `008_AP8.md` | Claude-Adapter und Regex-Usage | AP4, AP6 | GPT-5.6 Sol, medium |
| `009_AP9.md` | Git-Prüfung und Auto-Commit | AP3, AP5 | GPT-5.6 Sol, high |
| `010_AP10.md` | WinForms-Oberfläche und Tray | AP2, AP6–AP9 | GPT-5.6 Sol, medium |
| `011_AP11.md` | Wiederanlauf und menschliche Prüfung | AP6–AP10 | GPT-5.6 Sol, high |
| `012_AP12.md` | End-to-End-Tests und Veröffentlichung | alle | GPT-5.6 Sol, high |

`medium` ist der Standard für klar abgegrenzte Implementierungsarbeit. `high` wird nur dort eingesetzt, wo Nebenläufigkeit, Prozesslebenszyklus, Protokollintegration, Wiederanlauf oder irreversible Git-Aktionen zusätzliche Fehlermöglichkeiten erzeugen. `xhigh` oder `max` sind für die geplanten Pakete nicht erforderlich. Falls ein High-Paket Usage sparen muss, darf es zunächst mit medium versucht werden; bei nicht bestandenen Abnahmekriterien wird dieselbe Session mit high fortgesetzt, statt ein neues Paket zu beginnen.

## 14. Arbeitsweise für separate KI-Sessions

Jede Session erhält genau eine Arbeitspaketdatei als Hauptauftrag.

Verbindliche Reihenfolge:

1. `PLAN.md` vollständig lesen.
2. die eigene AP-Datei vollständig lesen.
3. vorhandenen Stand und Tests prüfen.
4. nur den beschriebenen Umfang implementieren.
5. Tests und Abnahmekriterien ausführen.
6. keine nachfolgenden Arbeitspakete vorwegnehmen.
7. bei Unklarheiten oder Konflikten anhalten und dokumentieren.
8. bei aktiviertem Git-Auto-Commit genau einen thematischen Commit erzeugen.

## 15. Quellenhinweis zu Codex

- Codex `exec`, stdin, JSONL und Sitzungsfortsetzung: https://learn.chatgpt.com/docs/developer-commands?surface=cli
- Codex App Server mit `account/rateLimits/read`, Mehrfach-Buckets, Resetzeit und Limitstatus: https://learn.chatgpt.com/docs/app-server
- Codex Analytics API nur als aggregierte Workspace-Auswertung, nicht als Scheduler-Rate-Limit-Quelle: https://learn.chatgpt.com/de-DE/docs/enterprise/analytics-api
