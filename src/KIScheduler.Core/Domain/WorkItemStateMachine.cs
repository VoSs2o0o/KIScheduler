using System.Collections.Frozen;

namespace KIScheduler.Core.Domain;

public static class WorkItemStateMachine
{
    private static readonly IReadOnlyDictionary<WorkItemStatus, IReadOnlySet<WorkItemStatus>> Transitions =
            new Dictionary<WorkItemStatus, IReadOnlySet<WorkItemStatus>>
            {
                [WorkItemStatus.Entwurf] = Set(WorkItemStatus.InWarteschlange, WorkItemStatus.Pausiert, WorkItemStatus.Abgebrochen),
                [WorkItemStatus.InWarteschlange] = Set(WorkItemStatus.Reserviert, WorkItemStatus.WartetAufUsage, WorkItemStatus.ProjektFehlt, WorkItemStatus.Pausiert, WorkItemStatus.Abgebrochen),
                [WorkItemStatus.Reserviert] = Set(WorkItemStatus.InBearbeitung, WorkItemStatus.InWarteschlange, WorkItemStatus.WartetAufUsage, WorkItemStatus.ProjektFehlt, WorkItemStatus.Pausiert, WorkItemStatus.MenschlichePruefung, WorkItemStatus.Abgebrochen, WorkItemStatus.Unterbrochen),
                [WorkItemStatus.InBearbeitung] = Set(WorkItemStatus.WartetAufUsage, WorkItemStatus.MenschlichePruefung, WorkItemStatus.TechnischErfolgreich, WorkItemStatus.ErfolgreichMitWarnung, WorkItemStatus.Fehlgeschlagen, WorkItemStatus.Abgebrochen, WorkItemStatus.Unterbrochen),
                [WorkItemStatus.WartetAufUsage] = Set(WorkItemStatus.Reserviert, WorkItemStatus.InWarteschlange, WorkItemStatus.ProjektFehlt, WorkItemStatus.Pausiert, WorkItemStatus.MenschlichePruefung, WorkItemStatus.Abgebrochen),
                [WorkItemStatus.ProjektFehlt] = Set(WorkItemStatus.InWarteschlange, WorkItemStatus.Entwurf, WorkItemStatus.Pausiert, WorkItemStatus.Abgebrochen),
                [WorkItemStatus.Pausiert] = Set(WorkItemStatus.Entwurf, WorkItemStatus.InWarteschlange, WorkItemStatus.Abgebrochen),
                [WorkItemStatus.MenschlichePruefung] = Set(WorkItemStatus.InWarteschlange, WorkItemStatus.Abgebrochen),
                [WorkItemStatus.Unterbrochen] = Set(WorkItemStatus.InWarteschlange, WorkItemStatus.WartetAufUsage, WorkItemStatus.MenschlichePruefung, WorkItemStatus.Abgebrochen),
                [WorkItemStatus.TechnischErfolgreich] = Set(),
                [WorkItemStatus.ErfolgreichMitWarnung] = Set(),
                // A failed attempt may be returned to the queue by the retry policy. Manual retries of
                // terminal failures still create a new work item so their audit history stays separate.
                [WorkItemStatus.Fehlgeschlagen] = Set(WorkItemStatus.InWarteschlange),
                [WorkItemStatus.Abgebrochen] = Set()
            }.ToFrozenDictionary();

    public static IReadOnlySet<WorkItemStatus> AllowedTargets(WorkItemStatus source) => Transitions[source];

    public static bool CanTransition(WorkItemStatus source, WorkItemStatus target) =>
        Transitions.TryGetValue(source, out var targets) && targets.Contains(target);

    public static void EnsureAllowed(WorkItemStatus source, WorkItemStatus target)
    {
        if (!CanTransition(source, target))
        {
            throw new InvalidWorkItemStatusTransitionException(source, target);
        }
    }

    private static IReadOnlySet<WorkItemStatus> Set(params WorkItemStatus[] statuses) => statuses.ToFrozenSet();
}

public sealed class InvalidWorkItemStatusTransitionException : InvalidOperationException
{
    public InvalidWorkItemStatusTransitionException(WorkItemStatus source, WorkItemStatus target)
        : base($"Der Statusübergang von '{source}' nach '{target}' ist nicht erlaubt.")
    {
        SourceStatus = source;
        TargetStatus = target;
    }

    public WorkItemStatus SourceStatus { get; }
    public WorkItemStatus TargetStatus { get; }
}
