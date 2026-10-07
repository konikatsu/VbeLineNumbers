using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]

[TestClass]
public sealed class TestSuite
{
    private static void RunSta(Action action)
    {
        Exception failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception error) { failure = error; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(60))) throw new TimeoutException("STA test timed out");
        if (failure != null) throw failure;
    }

    [TestMethod]
    public void LayoutAndRenderingRegressions()
    {
        RunSta(RegressionTests.Run);
    }

    [TestMethod]
    public void AccessIntegration()
    {
        string database = Environment.GetEnvironmentVariable("VBE_TEST_DATABASE");
        if (string.IsNullOrEmpty(database)) Assert.Inconclusive("Set VBE_TEST_DATABASE to the dedicated test database.");
        RunSta(() => AccessSmokeTest.Run(new[] { database }));
    }
}
