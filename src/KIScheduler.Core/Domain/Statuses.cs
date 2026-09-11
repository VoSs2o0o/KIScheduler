namespace KIScheduler.Core.Domain;

public enum WorkItemStatus
{
    Entwurf,
    InWarteschlange,
    Reserviert,
    InBearbeitung,
    WartetAufUsage,
    ProjektFehlt,
    Pausiert,
    MenschlichePruefung,
    TechnischErfolgreich,
    ErfolgreichMitWarnung,
    Fehlgeschlagen,
    Abgebrochen,
    Unterbrochen
}

public enum WorkItemDisplayStatus
{
    Entwurf,
    InWarteschlange,
    Reserviert,
    InBearbeitung,
    WartetAufUsage,
    ProjektFehlt,
    Pausiert,
    MenschlichePruefung,
    TechnischErfolgreich,
    ErfolgreichMitWarnung,
    Fehlgeschlagen,
    Abgebrochen,
    Unterbrochen,
    ProjektAngehalten
}

public enum ExecutionAttemptResult
{
    TechnischErfolgreich,
    ErfolgreichMitWarnung,
    Fehlgeschlagen,
    Abgebrochen,
    Unterbrochen,
    MenschlichePruefung,
    UsageExceeded
}

public enum UsageQuality { Aktuell, Veraltet, Unbekannt, Geschaetzt }
public enum UnknownUsageBehavior { Blockieren, Erlauben }
public enum ExecutionEventSeverity { Trace, Information, Warning, Error }
public enum ProjectHoldReleaseRule { ErfolgreicherAbschlussOderExpliziterAbbruch, NurManuell }
public enum PlatformBlockReleaseRule { FrischerZulaessigerUsageSnapshot, NurManuell }
