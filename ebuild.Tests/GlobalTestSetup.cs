using System.Diagnostics;
using NUnit.Framework;

namespace ebuild.Tests;


[SetUpFixture]
public class GlobalTestSetup
{
    [OneTimeSetUp]
    public void GlobalSetup()
    {
        Trace.Listeners.Add(new ConsoleTraceListener());
        EBuild.InitializeEBuild();
    }

    [OneTimeTearDown]
    public void GlobalTeardown()
    {
        Trace.Flush();
    }
}