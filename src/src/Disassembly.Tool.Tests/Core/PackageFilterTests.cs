using Disassembly.Tool.Core;
using Xunit;

namespace Disassembly.Tool.Tests.Core;

public class PackageFilterTests
{
    private static PackageInfo Pkg(string name, string version = "1.0.0") =>
        new(name, version, "/path/to/dll", null);

    [Fact]
    public void ApplyFilter_WithoutInclude_ExcludesDefaultMicrosoftPackages()
    {
        var packages = new List<PackageInfo>
        {
            Pkg("Newtonsoft.Json"),
            Pkg("System.Linq"),
            Pkg("System.Collections"),
        };

        var result = PackageFilter.ApplyFilter(
            packages,
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal),
            includeDefault: false);

        Assert.Single(result);
        Assert.Equal("Newtonsoft.Json", result[0].Name);
    }

    [Fact]
    public void ApplyFilter_WithIncludeDefault_DoesNotExcludeMicrosoftPackagesByDefault()
    {
        var packages = new List<PackageInfo>
        {
            Pkg("Newtonsoft.Json"),
            Pkg("System.Linq"),
        };

        var result = PackageFilter.ApplyFilter(
            packages,
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal),
            includeDefault: true);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Name == "Newtonsoft.Json");
        Assert.Contains(result, p => p.Name == "System.Linq");
    }

    [Fact]
    public void ApplyFilter_WithUserExclude_ExcludesSpecifiedPackages()
    {
        var packages = new List<PackageInfo>
        {
            Pkg("Newtonsoft.Json"),
            Pkg("SomeOther.Package"),
        };

        var exclude = new HashSet<string>(StringComparer.Ordinal) { "Newtonsoft.Json" };
        var result = PackageFilter.ApplyFilter(
            packages,
            exclude,
            new HashSet<string>(StringComparer.Ordinal),
            includeDefault: true);

        Assert.Single(result);
        Assert.Equal("SomeOther.Package", result[0].Name);
    }

    [Fact]
    public void ApplyFilter_WithInclude_ReturnsOnlyIncludedPackages()
    {
        var packages = new List<PackageInfo>
        {
            Pkg("Newtonsoft.Json"),
            Pkg("SomeOther.Package"),
            Pkg("Third.Package"),
        };

        var include = new HashSet<string>(StringComparer.Ordinal) { "Newtonsoft.Json", "Third.Package" };
        var result = PackageFilter.ApplyFilter(
            packages,
            new HashSet<string>(StringComparer.Ordinal),
            include,
            includeDefault: true);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Name == "Newtonsoft.Json");
        Assert.Contains(result, p => p.Name == "Third.Package");
        Assert.DoesNotContain(result, p => p.Name == "SomeOther.Package");
    }

    [Fact]
    public void ApplyFilter_WithIncludeAndExclude_ExcludeTakesPrecedence()
    {
        var packages = new List<PackageInfo>
        {
            Pkg("Newtonsoft.Json"),
            Pkg("SomeOther.Package"),
            Pkg("Third.Package"),
        };

        var include = new HashSet<string>(StringComparer.Ordinal) { "Newtonsoft.Json", "SomeOther.Package", "Third.Package" };
        var exclude = new HashSet<string>(StringComparer.Ordinal) { "SomeOther.Package" };
        var result = PackageFilter.ApplyFilter(packages, exclude, include, includeDefault: true);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Name == "Newtonsoft.Json");
        Assert.Contains(result, p => p.Name == "Third.Package");
        Assert.DoesNotContain(result, p => p.Name == "SomeOther.Package");
    }

    [Fact]
    public void ApplyFilter_CaseSensitive_DifferentCaseNotExcluded()
    {
        var packages = new List<PackageInfo>
        {
            Pkg("System.Linq"),
            Pkg("system.linq"),
        };

        // system.linq (lowercase) is NOT in default exclude list, so with includeDefault: false
        // only System.Linq should be excluded
        var result = PackageFilter.ApplyFilter(
            packages,
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal),
            includeDefault: false);

        Assert.Single(result);
        Assert.Equal("system.linq", result[0].Name);
    }

    [Fact]
    public void ApplyFilter_UserExclude_CaseSensitive()
    {
        var packages = new List<PackageInfo>
        {
            Pkg("MyPackage"),
            Pkg("mypackage"),
        };

        var exclude = new HashSet<string>(StringComparer.Ordinal) { "MyPackage" };
        var result = PackageFilter.ApplyFilter(
            packages,
            exclude,
            new HashSet<string>(StringComparer.Ordinal),
            includeDefault: true);

        Assert.Single(result);
        Assert.Equal("mypackage", result[0].Name);
    }

    [Fact]
    public void AddParsedNames_AddsToExistingSet()
    {
        var target = new HashSet<string>(StringComparer.Ordinal) { "Existing" };
        PackageFilter.AddParsedNames(target, "New1, New2");

        Assert.Equal(3, target.Count);
        Assert.Contains("Existing", target);
        Assert.Contains("New1", target);
        Assert.Contains("New2", target);
    }
}
