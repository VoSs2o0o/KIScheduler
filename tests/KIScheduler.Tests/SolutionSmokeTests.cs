using CoreMarker = KIScheduler.Core.ProjectAssemblyMarker;
using InfrastructureMarker = KIScheduler.Infrastructure.ProjectAssemblyMarker;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PlatformsMarker = KIScheduler.Platforms.ProjectAssemblyMarker;

namespace KIScheduler.Tests;

[TestClass]
public sealed class SolutionSmokeTests
{
    [TestMethod]
    public void PlannedAssembliesCanBeLoaded()
    {
        Assert.AreEqual("KIScheduler.Core", typeof(CoreMarker).Assembly.GetName().Name);
        Assert.AreEqual("KIScheduler.Infrastructure", typeof(InfrastructureMarker).Assembly.GetName().Name);
        Assert.AreEqual("KIScheduler.Platforms", typeof(PlatformsMarker).Assembly.GetName().Name);
    }
}
