# KIScheduler – Benutzerhandbuch

## Installation und Start

Die erste Version wird als framework-abhängiges Windows-x64-Paket veröffentlicht. Voraussetzung ist die
.NET 8 Desktop Runtime (x64). Das veröffentlichte Verzeichnis kann in einen beliebigen Ordner entpackt
werden; Administratorrechte und ein Windows-Dienst sind nicht erforderlich. Startdatei ist
`KIScheduler.WinForms.exe`.

Beim Schließen über das Fensterkreuz bleibt KIScheduler im Infobereich aktiv. Nur **Beenden** im
Tray-Menü beendet Worker und Anwendung kontrolliert. Während einer laufenden Ausführung wartet die
Anwendung beim Beenden auf den kontrollierten Abbruch; der Auftrag wird so gespeichert, dass er beim
nächsten Start als unterbrochen erkennbar ist.

## Daten-, Konfigurations- und Logpfade

Die mitgelieferte `appsettings.json` liegt schreibgeschützt neben der Anwendung. Standardmäßig werden
alle veränderlichen Daten ohne Administratorrechte unter `%LOCALAPPDATA%\KIScheduler` abgelegt:

| Inhalt | Standardpfad |
|---|---|
| SQLite-Datenbank | `%LOCALAPPDATA%\KIScheduler\data\kischeduler.db` |
| SQLite-WAL/SHM | neben der Datenbank |
| Protokolle | `%LOCALAPPDATA%\KIScheduler\logs` |

Für einen vollständig portablen Betrieb kann in `appsettings.json` gesetzt werden:

```json
"Runtime": {
  "DataDirectory": "."
}
```

Relative Datenbank- und Logpfade werden dann vom Programmverzeichnis aus aufgelöst. Der portable
Ordner muss für den angemeldeten Benutzer beschreibbar sein; ein Ordner unter `Programme` ist dafür
nicht geeignet. Ein absoluter Pfad und Windows-Umgebungsvariablen wie `%USERPROFILE%` sind ebenfalls
zulässig. Für automatisierte Starts können Werte mit dem Präfix `KISCHEDULER_` überschrieben werden,
beispielsweise `KISCHEDULER_Runtime__DataDirectory`; Kommandozeilenwerte haben die höchste Priorität.

## Erstkonfiguration

1. Codex CLI und/oder Claude CLI im selben Windows-Benutzerkonto installieren und dort anmelden.
2. KIScheduler starten. Die Plattformübersicht muss die gewünschte CLI als verfügbar anzeigen.
3. Unter **Einstellungen** ausführbare Datei, Modelle und Effort-Stufen prüfen. Für Claude außerdem den
   Usage-Befehl samt Regex gegen die lokal installierte Version testen.
4. Ein Projekt mit Rootverzeichnis und Zielbranch anlegen. `master` ist nur der Standard; der reale
   Branch muss übereinstimmen.
5. Eine Prompt-Datei unter `<Projekt>\docs` oder `<Projekt>\docprompts` auswählen, Plattform, Modell,
   Priorität und Auto-Commit festlegen und den Auftrag einreihen.
6. Zunächst einen kleinen Testauftrag ohne Auto-Commit ausführen. Danach Verlauf, stdout/stderr,
   Sitzung und Git-Status kontrollieren.

Bei Auto-Commit muss das Repository vor dem Start sauber sein. KIScheduler wechselt weder Branch noch
Worktree automatisch und erzeugt keinen leeren Commit.

## Plattformprofile

Über **Plattformen & Usage** und **Plattform und Profile verwalten** lassen sich mehrere getrennte
Profile je Plattform anlegen. Ein Profil besteht aus einem Anzeigenamen und einem vorhandenen oder
neu anzulegenden Konfigurationsordner, zum Beispiel `C:\Users\<Benutzer>\.codex2`. Der Ordner wird
von KIScheduler nicht angelegt, gelöscht oder verändert; dort liegen ausschließlich die von der
jeweiligen CLI verwaltete Anmeldung und Konfiguration.

Beim Anlegen eines Auftrags wird das Profil nach der Plattform und vor Modell und Effort fest gewählt.
Nur aktive Profile sind auswählbar. Beim Bearbeiten oder Duplizieren bleibt die bisherige
Profilzuordnung erhalten, sofern das Profil noch aktiv ist. Historische Versuche zeigen ihre frühere
Profilzuordnung auch dann weiter an, wenn das Profil später deaktiviert wurde.

Usage und Health werden je Profil geprüft. Die zusammengefasste Plattformzeile verwendet ausschließlich
das Standardprofil. Für die Statusleiste kann die Usage-Anzeige unabhängig für jedes Profil aktiviert
werden; dieses Anzeigehäkchen verändert keine Scheduler-Entscheidung.

Das Deaktivieren entfernt weder Dateien noch Anmeldedaten. Wartende oder bearbeitbare Aufträge müssen
zuerst einem anderen aktiven Profil zugeordnet werden. Das Standardprofil kann nur deaktiviert werden,
wenn im selben Dialog ein aktives Ersatzprofil ausgewählt wird.

## Status und typische Fehler

- **WartetAufUsage:** Mindestens ein Usage-Fenster ist an seiner exklusiven Grenze, Usage ist gemäß
  Richtlinie unbekannt/veraltet oder die Plattform ist serverseitig limitiert. Exakt 100 Prozent bleibt
  auch im Endspurt blockierend.
- **ProjektFehlt:** Prompt oder bestätigtes Projektroot fehlt. Pfad erneut auswählen; bei verschobenen
  Prompts wird die Auflösung erneut geprüft.
- **MenschlichePruefung:** Anmeldung, Start, Timeout, Rückfrage, unklare Ausgabe, fehlende sichere
  Sitzungsfortsetzung oder verpflichtender Git-Commit erfordern eine Entscheidung. In Verlauf und
  Detailansicht stehen Fehlergrund, Session-ID, Logverweis und Fortsetzungsbefehl.
- **Projekt angehalten:** Eine abgebrochene oder nur teilweise ausgeführte Aufgabe könnte den
  Arbeitsbaum verändert haben. Repository prüfen und danach die auslösende Aufgabe fortsetzen,
  abbrechen oder den Hold bewusst manuell freigeben.
- **Zweite Instanz beendet sich:** Pro Datenbank ist absichtlich nur ein aktiver Worker erlaubt.

## Backup und Restore

KIScheduler vor Backup oder Restore über **Beenden** vollständig schließen. Anschließend den gesamten
Ordner `%LOCALAPPDATA%\KIScheduler\data` sichern, nicht nur die `.db`-Datei; bei aktivem WAL können auch
`kischeduler.db-wal` und `kischeduler.db-shm` relevant sein.

Zum Wiederherstellen KIScheduler schließen, den aktuellen Datenordner zusätzlich wegsichern und den
gesicherten Datenordner an dieselbe Stelle kopieren. Beim nächsten Start werden ausstehende
Datenbankmigrationen automatisch angewandt. Ein Backup aus einer neueren Anwendungsversion sollte
nicht mit einer älteren Version geöffnet werden.

## Grenzen der Usage-Ermittlung

Codex verwendet ausschließlich den Codex App Server und bevorzugt dessen Mehrfach-Bucket-Ansicht.
Ist App Server oder Anmeldung nicht verfügbar, wird Usage als unbekannt behandelt; es gibt keinen
Fallback auf private HTTP-Endpunkte. Ein Resetzeitpunkt allein entsperrt nichts: erforderlich ist ein
frischer Snapshot unterhalb der Grenze und ohne serverseitigen Limitstatus.

Claude besitzt in Version 1 keinen vorausgesetzten stabilen maschinenlesbaren Usage-Endpunkt. Die
Ausgabe eines konfigurierten lokalen Befehls wird per Regex gelesen. Änderungen der CLI-Ausgabe können
die Messung daher auf **unbekannt** setzen. Ohne gelesenen oder fest konfigurierten Resetzeitpunkt ist
kein Endspurt möglich.
