# AP12 – Release- und Smoke-Test-Checkliste

Stand: 2026-09-12

## Veröffentlichungsentscheidung

Version 1 wird **framework-abhängig für Windows x64** veröffentlicht. Das hält das Paket klein und
wartbar; Zielsysteme benötigen die aktuelle .NET 8 Desktop Runtime x64. Das Publish-Profil
`win-x64.pubxml` und `scripts/Publish-Release.ps1` bilden diese Entscheidung reproduzierbar ab.

Release erzeugen:

```powershell
.\scripts\Publish-Release.ps1
```

Die Ausgabe liegt standardmäßig unter `artifacts\publish\win-x64`. Das Skript führt Restore,
Release-Build, alle Tests und Publish aus. Danach startet es die veröffentlichte EXE mit
`--startup-check` gegen einen isolierten Datenordner und beendet Host und Worker kontrolliert. Vor
Weitergabe den ganzen Ausgabeordner als ZIP verpacken.

## Automatisch abgesicherte Kernfälle

- Erfolg, technischer Fehler mit Backoff, Timeout und kontrollierter Abbruch
- Usage-Warten, Endspurt, exakt 100 Prozent und serverseitiger Limitstatus unter 100 Prozent
- `UsageExceeded` während der Ausführung, Plattformblock, Projekt-Hold und Fortsetzung derselben Sitzung
- keine Freigabe allein aufgrund verstrichener Resetzeit; frischer zulässiger Snapshot erforderlich
- höchstens eine Ausführung je Plattform sowie projektweite Sperre über Plattformgrenzen hinweg
- parallele Plattformen in verschiedenen Projekten
- Projektauflösung über `docs`, `docprompts`, Verschachtelung und Groß-/Kleinschreibung
- Crash-Recovery, idempotente Datenbankmigration und exklusive Worker-Sperre
- Git-Auto-Commit inklusive neuer, geänderter und gelöschter Dateien in temporären Repositories
- Maskierung sensibler Argumente, Umgebungswerte, Authentifizierungsheader, strukturierter Secrets und
  Exception-Texte in Prozess- und Dateiprotokollen
- Standard- und portable Laufzeitpfade

## Manueller Smoke-Test vor jeder Veröffentlichung

Diese Prüfung muss auf einem normalen, nicht erhöhten Windows-Benutzerkonto mit den für den Release
vorgesehenen CLI-Versionen ausgeführt und in der Release-Notiz mit Datum, Tester und Versionen quittiert
werden. Echte KI-Aufrufe sind bewusst kein automatischer Test, weil sie Anmeldung, Kontingent und
Kosten verbrauchen.

| Nr. | Prüfung | Erwartung |
|---:|---|---|
| 1 | ZIP in einen benutzereigenen Ordner entpacken und EXE ohne „Als Administrator“ starten | Hauptfenster öffnet; DB und Log entstehen unter `%LOCALAPPDATA%\KIScheduler` |
| 2 | Fensterkreuz verwenden | Fenster verschwindet, Tray-Symbol und Menü bleiben erreichbar |
| 3 | Verarbeitung im Tray pausieren/fortsetzen | Neue Aufträge starten pausiert nicht und nach Fortsetzen wieder |
| 4 | Codex-Testauftrag ohne Auto-Commit ausführen | nicht-interaktiver Lauf, strukturierte Ausgabe, Verlauf und Session-ID sind sichtbar |
| 5 | Codex-Auftrag mit Auto-Commit in sauberem temporärem Repository ausführen | genau ein Commit auf dem konfigurierten Branch; kein Branchwechsel |
| 6 | Claude-Testauftrag ohne Auto-Commit ausführen | Print-/Stream-JSON-Lauf beendet sich ohne Rückfrage; Verlauf ist vollständig |
| 7 | Claude-Usage-Test in Einstellungen ausführen | Regex liefert plausiblen Prozentwert; unbekannte Ausgabe wird nicht als 0 Prozent gewertet |
| 8 | Laufenden Testauftrag kontrolliert über Tray **Beenden** stoppen und neu starten | Auftrag ist `Unterbrochen`, Diagnose bleibt erhalten und Projekt-Hold ist sichtbar |
| 9 | „In Plattform prüfen“/Fortsetzungsbefehl für einen Review-Fall öffnen bzw. kopieren | Plattform, Session, Log und korrekt gequoteter Befehl sind verfügbar; kein automatischer Start |
| 10 | Anwendung vollständig über Tray **Beenden** schließen | Worker und CLI-Prozessbaum laufen nicht weiter; erneuter Start gelingt |
| 11 | `Runtime:DataDirectory` auf `.` setzen und aus beschreibbarem Ordner starten | DB und Logs entstehen portabel neben der Anwendung |
| 12 | Datenordner bei beendeter Anwendung sichern, Testdaten ändern, Sicherung restaurieren | Zustand wird nach Neustart aus der Sicherung geladen |

## Lokaler Nachweis für AP12

Am 2026-09-12 wurde auf der Entwicklungsmaschine `codex-cli 0.154.0` erkannt. Eine Claude-CLI war dort
nicht installiert. Daher bleiben die echten Codex-/Claude-Aufrufe und der sichtbare Tray-Test ein
bewusstes manuelles Release-Gate; sie dürfen erst als bestanden markiert werden, wenn beide Ziel-CLIs
auf dem Abnahmesystem installiert und angemeldet sind.
