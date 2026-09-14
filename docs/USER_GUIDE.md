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
Profile je Plattform anlegen. Ein Profil besteht aus einem Anzeigenamen und einem zuvor außerhalb
von KIScheduler angelegten Konfigurationsordner, zum Beispiel
`C:\Users\<Benutzer>\.codex2`. Der Ordner wird
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

### Codex-Zweitprofil `codex2` getrennt anmelden

Für zuverlässig getrennte Codex-Anmeldungen muss jedes Profil den dateibasierten Credential-Store
verwenden. Der Windows-Credential-Store ist benutzerweit und bietet deshalb keine verlässliche Trennung
allein durch unterschiedliche `CODEX_HOME`-Ordner.

1. `%USERPROFILE%\.codex2` anlegen und dort in `config.toml` den Wert
   `cli_auth_credentials_store = "file"` setzen. Für das Standardprofil dieselbe Einstellung in
   `%USERPROFILE%\.codex\config.toml` verwenden.
2. Ein neues PowerShell-Fenster öffnen und nur für diese Anmeldung setzen:

   ```powershell
   $env:CODEX_HOME = "$env:USERPROFILE\.codex2"
   $env:CODEX_SQLITE_HOME = $env:CODEX_HOME
   codex login
   codex login status
   ```

3. In KIScheduler das Profil `codex2` mit Anzeigename `codex2` und genau diesem Ordner anlegen.
4. **Profil prüfen** ausführen. Erst wenn CLI-Anmeldung und Usage plausibel sind, Aufträge zuordnen.

`codex login` öffnet standardmäßig die Browser-Anmeldung; bei Bedarf unterstützt die CLI auch
`codex login --device-auth`. Die Einstellung `file` legt die von Codex selbst verwaltete `auth.json`
unter dem jeweiligen `CODEX_HOME` ab. Diese Datei wie ein Kennwort behandeln. KIScheduler liest,
kopiert oder löscht sie nicht.

Eine in `config.toml` gesetzte Codex-Option `sqlite_home` kann `CODEX_SQLITE_HOME` nach den
Prioritätsregeln der CLI übersteuern. Außerdem meldet `codex login status` nur, ob eine Anmeldung
vorhanden ist; KIScheduler liest die Credential-Datei nicht und vergleicht daher keine Kontoidentitäten.

### Claude-Zweitprofil getrennt anmelden

1. Ein neues PowerShell-Fenster öffnen und den gewünschten Profilordner setzen:

   ```powershell
   $env:CLAUDE_CONFIG_DIR = "$env:USERPROFILE\.claude2"
   claude
   ```

2. In der interaktiven Claude-CLI `/login` ausführen und danach die Sitzung beenden.
3. In KIScheduler `claude2` mit genau diesem Ordner anlegen und **Profil prüfen** ausführen.

Claude legt unter Windows Einstellungen, Sitzungsverlauf und `.credentials.json` im gesetzten
`CLAUDE_CONFIG_DIR` ab. Die Isolation gilt nur, wenn Anmeldung, Usage-Prüfung und Ausführung mit
demselben Ordner erfolgen. Globale Anbieter-Variablen wie `ANTHROPIC_API_KEY` können die
Subscription-Anmeldung übersteuern und müssen bei einer solchen Prüfung berücksichtigt werden.
Auch hier prüft KIScheduler keine Credential-Inhalte oder Kontoidentitäten, sondern ausschließlich
CLI-Status und -Verhalten im ausgewählten Profilordner.

KIScheduler setzt bei jedem Ausführungs-, Health- und Usage-Prozess die gespeicherten Profilvariablen
explizit. Bereits im Elternprozess vorhandene Werte von `CODEX_HOME`, `CODEX_SQLITE_HOME` oder
`CLAUDE_CONFIG_DIR` überschreiben daher weder das gespeicherte Standardprofil noch das gewählte
Zweitprofil.

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
- **Profilordner fehlt:** Den in der Profilverwaltung gespeicherten absoluten Pfad prüfen. KIScheduler
  erzeugt den Ordner absichtlich nicht und sucht nicht selbst nach Credential-Dateien.
- **Profil deaktiviert oder nicht gefunden:** Einen noch nicht gestarteten Auftrag auf ein aktives
  Profil umstellen. Nach dem ersten Versuch bleibt die ursprüngliche Profil-ID für Retry und Resume
  unveränderlich; bei fehlender sicherer Fortsetzung ist menschliche Prüfung erforderlich.
- **Falsches Codex-Konto oder identische Usage in beiden Profilen:** In beiden Profilordnern
  `cli_auth_credentials_store = "file"` kontrollieren und `codex login status` jeweils in einer
  getrennt gesetzten `CODEX_HOME`-/`CODEX_SQLITE_HOME`-Umgebung ausführen. `keyring` und `auto` können
  denselben benutzerweiten Windows-Credential-Store verwenden.
- **Falsches Claude-Konto:** Claude in einer frischen Shell mit dem gespeicherten
  `CLAUDE_CONFIG_DIR` starten und `/login` prüfen. Zusätzlich geerbte `ANTHROPIC_API_KEY`- oder
  `ANTHROPIC_AUTH_TOKEN`-Werte ausschließen.
- **Logs für Support sammeln:** Nur KIScheduler-Log, sichtbare Fehlermeldung, Plattform-/Profilname,
  Zeitpunkt und CLI-Version weitergeben. Niemals `auth.json`, `.credentials.json`, Token,
  Authorization-Header oder den Inhalt eines Credential-Ordners anhängen.

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

Offizielle Referenzen: [Codex-Authentifizierung](https://learn.chatgpt.com/docs/auth),
[Codex-CLI-Befehle](https://learn.chatgpt.com/docs/developer-commands?surface=cli),
[Claude-Umgebungsvariablen](https://code.claude.com/docs/en/env-vars) und
[Claude-Authentifizierung](https://code.claude.com/docs/en/team).
