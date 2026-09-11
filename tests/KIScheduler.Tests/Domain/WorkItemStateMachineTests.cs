using KIScheduler.Core.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KIScheduler.Tests.Domain;

[TestClass]
public sealed class WorkItemStateMachineTests
{
    public static IEnumerable<object[]> AllowedTransitions()
    {
        foreach (var source in Enum.GetValues<WorkItemStatus>())
        {
            foreach (var target in WorkItemStateMachine.AllowedTargets(source))
            {
                yield return [source, target];
            }
        }
    }

    [DataTestMethod]
    [DynamicData(nameof(AllowedTransitions), DynamicDataSourceType.Method)]
    public void EveryDeclaredTransitionIsAllowed(WorkItemStatus source, WorkItemStatus target)
    {
        Assert.IsTrue(WorkItemStateMachine.CanTransition(source, target));
        WorkItemStateMachine.EnsureAllowed(source, target);
    }

    [DataTestMethod]
    [DataRow(WorkItemStatus.Entwurf, WorkItemStatus.InBearbeitung)]
    [DataRow(WorkItemStatus.InWarteschlange, WorkItemStatus.TechnischErfolgreich)]
    [DataRow(WorkItemStatus.InBearbeitung, WorkItemStatus.Entwurf)]
    [DataRow(WorkItemStatus.TechnischErfolgreich, WorkItemStatus.InWarteschlange)]
    [DataRow(WorkItemStatus.Abgebrochen, WorkItemStatus.InBearbeitung)]
    public void ForbiddenTransitionIsRejectedWithContext(WorkItemStatus source, WorkItemStatus target)
    {
        var exception = Assert.ThrowsException<InvalidWorkItemStatusTransitionException>(
            () => WorkItemStateMachine.EnsureAllowed(source, target));

        Assert.AreEqual(source, exception.SourceStatus);
        Assert.AreEqual(target, exception.TargetStatus);
    }
}
