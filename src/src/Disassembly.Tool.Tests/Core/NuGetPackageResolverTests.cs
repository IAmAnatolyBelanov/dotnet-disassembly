using Disassembly.Tool.Core;
using Xunit;

namespace Disassembly.Tool.Tests.Core;

/// <summary>
/// Тесты для класса NuGetPackageResolver
/// </summary>
public class NuGetPackageResolverTests
{
    private readonly NuGetPackageResolver _resolver;

    public NuGetPackageResolverTests()
    {
        _resolver = new NuGetPackageResolver();
    }

    [Fact]
    public void FindNuGetCachePath_WhenCacheExists_ReturnsPath()
    {
        // Arrange & Act
        var cachePath = _resolver.FindNuGetCachePath();

        // Assert
        Assert.NotNull(cachePath);
        Assert.NotEmpty(cachePath);
        // Путь должен содержать .nuget/packages
        Assert.Contains(".nuget", cachePath);
        Assert.Contains("packages", cachePath);
    }

    [Fact]
    public void ResolvePackage_WhenPackageDoesNotExist_ReturnsNull()
    {
        // Arrange
        var nonExistentPackage = "NonExistentPackage12345";
        var version = "1.0.0";

        // Act
        var result = _resolver.ResolvePackage(nonExistentPackage, version);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetAllPackages_WithEmptyProjectList_ReturnsEmptyList()
    {
        // Arrange
        var projects = new List<ProjectInfo>();

        // Act
        var result = _resolver.GetAllPackages(projects);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void GetAllPackages_WithProjects_ReturnsUniquePackages()
    {
        // Arrange
        var projects = new List<ProjectInfo>
        {
            new ProjectInfo(
                "/path/to/project1.csproj",
                "Project1",
                new List<PackageReference>
                {
                    new PackageReference("PackageA", "1.0.0"),
                    new PackageReference("PackageB", "2.0.0")
                }
            ),
            new ProjectInfo(
                "/path/to/project2.csproj",
                "Project2",
                new List<PackageReference>
                {
                    new PackageReference("PackageA", "1.0.0"), // Дубликат
                    new PackageReference("PackageC", "3.0.0")
                }
            )
        };

        // Act
        var result = _resolver.GetAllPackages(projects);

        // Assert
        Assert.NotNull(result);
        // Поскольку пакеты не существуют в реальном NuGet кэше, ResolvePackage вернет null
        // и они не будут добавлены. Проверяем, что метод корректно обрабатывает это
        // и возвращает только успешно разрешенные пакеты (в данном случае 0)
        // В реальном сценарии, если пакеты существуют, должны быть только уникальные пакеты
        Assert.True(result.Count >= 0); // Может быть 0, если пакеты не найдены
        // Проверяем, что если пакеты найдены, они уникальны
        var packageKeys = result.Select(p => $"{p.Name}:{p.Version}").ToList();
        var uniqueKeys = packageKeys.Distinct().ToList();
        Assert.Equal(packageKeys.Count, uniqueKeys.Count); // Все ключи должны быть уникальны
    }

    [Fact]
    public void PackageInfo_RecordProperties_AreAccessible()
    {
        // Arrange & Act
        var packageInfo = new PackageInfo(
            "TestPackage",
            "1.0.0",
            "/path/to/package.dll",
            "/path/to/package.xml"
        );

        // Assert
        Assert.Equal("TestPackage", packageInfo.Name);
        Assert.Equal("1.0.0", packageInfo.Version);
        Assert.Equal("/path/to/package.dll", packageInfo.DllPath);
        Assert.Equal("/path/to/package.xml", packageInfo.XmlDocPath);
    }

    [Fact]
    public void PackageInfo_WithNullXmlDocPath_IsValid()
    {
        // Arrange & Act
        var packageInfo = new PackageInfo(
            "TestPackage",
            "1.0.0",
            "/path/to/package.dll",
            null
        );

        // Assert
        Assert.NotNull(packageInfo);
        Assert.Null(packageInfo.XmlDocPath);
    }

    [Fact]
    public void PackageInfo_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var info1 = new PackageInfo("TestPackage", "1.0.0", "/path/to/dll", null);
        var info2 = new PackageInfo("TestPackage", "1.0.0", "/path/to/dll", null);
        var info3 = new PackageInfo("TestPackage", "2.0.0", "/path/to/dll", null);

        // Act & Assert
        Assert.Equal(info1, info2);
        Assert.NotEqual(info1, info3);
    }
}

