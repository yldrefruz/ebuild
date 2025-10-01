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
        // Optionally enable verbose logging for tests
        EBuild.VerboseEnabled = true;
    }

    [OneTimeTearDown]
    public void GlobalTeardown()
    {
        Trace.Flush();
    }
}