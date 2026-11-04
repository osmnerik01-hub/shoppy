using System.Xml.Linq;
using Xunit;

namespace Architecture.Tests;

public sealed class DependencyTests
{
    private static readonly string Root = FindRoot();
    private static readonly string[] Services = ["Catalog", "Inventory", "Orders"];
    private static readonly string[] Layers = ["Domain", "Application", "Infrastructure", "Api"];

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Project1.sln"))) return directory.FullName;
        throw new InvalidOperationException("Solution root not found.");
    }

    private static bool Allowed(string source, string target) => (source, target) switch
    {
        ("Application", "Domain") => true,
        ("Infrastructure", "Application" or "Domain") => true,
        ("Api", "Application" or "Infrastructure") => true,
        _ => false
    };

    [Fact]
    public void AllServicesHaveFourLayersAndOnlyInwardLocalReferences()
    {
        foreach (var service in Services)
        foreach (var layer in Layers)
        {
            var path = Path.Combine(Root, "src", "Services", service, $"{service}.{layer}", $"{service}.{layer}.csproj");
            Assert.True(File.Exists(path), path);
            var xml = XDocument.Load(path);
            foreach (var reference in xml.Descendants("ProjectReference"))
            {
                var target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, reference.Attribute("Include")!.Value));
                var targetLayer = Layers.SingleOrDefault(candidate => target == Path.Combine(Root, "src", "Services", service, $"{service}.{candidate}", $"{service}.{candidate}.csproj"));
                var sharedTransport = layer == "Infrastructure" && target == Path.Combine(Root, "src", "Shared", "Messaging", "Messaging.csproj");
                var sharedObservability = layer == "Api" && target == Path.Combine(Root, "src", "Shared", "Observability", "Observability.csproj");
                Assert.True(sharedObservability || sharedTransport || (targetLayer != null && Allowed(layer, targetLayer)), $"Forbidden reference: {path} -> {target}");
            }
            // Inner layers stay framework independent; persistence providers belong to Infrastructure.
            foreach (var package in xml.Descendants("PackageReference"))
            {
                var name = package.Attribute("Include")!.Value;
                Assert.False(layer is "Domain" or "Application", $"Inner-layer package: {name}");
                if (name.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase)) Assert.Equal("Infrastructure", layer);
                Assert.Null(package.Attribute("Version"));
            }
            if (layer is "Domain" or "Application") Assert.Empty(xml.Descendants("FrameworkReference"));
        }
    }

    [Theory]
    [InlineData("Domain", "Infrastructure")]
    [InlineData("Domain", "Api")]
    [InlineData("Application", "Infrastructure")]
    [InlineData("Infrastructure", "Api")]
    [InlineData("Api", "Domain")]
    public void PolicyRejectsReverseAndBypassReferences(string source, string target) => Assert.False(Allowed(source, target));

    [Fact]
    public void DomainSourcesDoNotUsePersistenceOrHttp()
    {
        foreach (var service in Services)
        foreach (var file in SourceFiles(Path.Combine(Root, "src", "Services", service, $"{service}.Domain")))
        {
            var source = File.ReadAllText(file);
            foreach (var forbidden in new[] { "Microsoft.EntityFrameworkCore", "System.Net.Http", "Microsoft.AspNetCore", "DbContext", "HttpClient" })
                Assert.DoesNotContain(forbidden, source);
        }
    }

    [Fact]
    public void PersistenceIsOwnedByOneServiceAndCannotQueryAnotherDatabase()
    {
        foreach (var service in Services)
        {
            var directory = Path.Combine(Root, "src", "Services", service);
            foreach (var file in SourceFiles(directory))
            {
                var source = File.ReadAllText(file);
                foreach (var other in Services.Where(other => other != service))
                {
                    Assert.DoesNotContain($"{other}DbContext", source, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain($"{other.ToLowerInvariant()}.db", source, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain($"using {other}.", source);
                }
                if (source.Contains(": DbContext", StringComparison.Ordinal))
                    Assert.Contains($"{service}.Infrastructure", file);
            }
        }
    }

    private static IEnumerable<string> SourceFiles(string directory) =>
        Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin"));
}
