using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ebuild.api;
using ebuild.api.Toolchain;
using ebuild.Modules.BuildGraph;
using ebuild.Platforms;
using NUnit.Framework;

namespace ebuild.Tests.Unit;


[TestFixture]
[Order(99)]
public class CircularDependencyUnitTests
{
    private string _circularDependencyExamplePath = string.Empty;
    private string _testModuleAPath = string.Empty;
    private string _testModuleBPath = string.Empty;
    private string _testOutputDir = string.Empty;
    private string _ebuildExePath = string.Empty;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _circularDependencyExamplePath = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "examples", "circular-dependency");
        _testModuleAPath = Path.Combine(_circularDependencyExamplePath, "test_circular_a.ebuild.cs");
        _testModuleBPath = Path.Combine(_circularDependencyExamplePath, "test_circular_b.ebuild.cs");
        _testOutputDir = Path.Combine(Path.GetTempPath(), "ebuild_test", "circular_dependency");
        var thisAssemblyLocation = Assembly.GetAssembly(GetType())!.Location;
        _ebuildExePath = Path.Combine(Path.GetDirectoryName(thisAssemblyLocation)!, "ebuild.dll");

        Directory.CreateDirectory(_testOutputDir);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        if (Directory.Exists(_testOutputDir))
        {
            Directory.Delete(_testOutputDir, true);
        }
    }



    [Category("Unit")]
    [Test]
    public Task BuildGraph_ShouldHandleCircularDependencyGracefully()
    {
        // Arrange
        var moduleFile = ModuleFile.Get(new ModuleReference(_testModuleAPath));
        var instancingParams = new ModuleInstancingParams
        {
            SelfModuleReference = new ModuleReference(_testModuleAPath),
            Configuration = "Debug",
            Architecture = RuntimeInformation.ProcessArchitecture,
            Toolchain = IToolchainRegistry.Get().GetToolchain(PlatformRegistry.GetHostPlatform().GetDefaultToolchainName()!)!, // should always succeed in test environments.
            Platform = PlatformRegistry.GetHostPlatform(),
            AdditionalLinkerOptions = []
        };

        // Act
        Graph? buildGraph = null;
        Assert.DoesNotThrowAsync(async () =>
        {
            buildGraph = await moduleFile.BuildOrGetBuildGraph(instancingParams);
        });

        // Assert
        Assert.That(buildGraph, Is.Not.Null, "Build graph should be created even with circular dependencies");
        Assert.That(buildGraph!.HasCircularDependency(), Is.True, "Build graph should detect circular dependency");

        var circularPath = buildGraph.GetCircularDependencyPath();
        Assert.That(circularPath, Is.Not.Empty, "Circular dependency path should not be empty");
        Assert.That(circularPath.Count, Is.GreaterThan(1), "Circular dependency path should contain multiple nodes");
        return Task.CompletedTask;
    }

    [Category("Unit")]
    [Test]
    public async Task BuildGraph_CachingWorksCorrectly()
    {
        // Arrange
        var moduleFile = ModuleFile.Get(new ModuleReference(_testModuleAPath));
        var instancingParams = new ModuleInstancingParams
        {
            SelfModuleReference = new ModuleReference(_testModuleAPath),
            Configuration = "Debug",
            Architecture = RuntimeInformation.ProcessArchitecture,
            Toolchain = IToolchainRegistry.Get().GetToolchain(PlatformRegistry.GetHostPlatform().GetDefaultToolchainName()!)!, // should always succeed in test environments.
            Platform = PlatformRegistry.GetHostPlatform(),
            AdditionalLinkerOptions = []
        };

        var buildGraph = await moduleFile.BuildOrGetBuildGraph(instancingParams);
        Assert.That(buildGraph, Is.Not.Null);

        // Act - Call multiple times to test caching
        var stopwatch = Stopwatch.StartNew();
        var hasCircular1 = buildGraph!.HasCircularDependency();
        var time1 = stopwatch.ElapsedMilliseconds;

        var hasCircular2 = buildGraph!.HasCircularDependency();
        var time2 = stopwatch.ElapsedMilliseconds - time1;

        var path1 = buildGraph!.GetCircularDependencyPath();
        var time3 = stopwatch.ElapsedMilliseconds - time1 - time2;

        var path2 = buildGraph.GetCircularDependencyPath();
        var time4 = stopwatch.ElapsedMilliseconds - time1 - time2 - time3;

        // Assert
        Assert.That(hasCircular1, Is.EqualTo(hasCircular2), "Cached results should be consistent");
        Assert.That(path1.Count, Is.EqualTo(path2.Count), "Cached path results should be consistent");

        // Second calls should be faster due to caching (allowing for some variance)
        Assert.That(time2, Is.LessThanOrEqualTo(time1), "Second HasCircularDependency call should be faster (cached)");
        Assert.That(time4, Is.LessThanOrEqualTo(time3), "Second GetCircularDependencyPath call should be faster (cached)");
    }
}