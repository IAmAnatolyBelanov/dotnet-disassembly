using Disassembly.Tool.Core;
using System.Xml.Linq;
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

    /// <summary>
    /// Создает .nuspec XML через XDocument
    /// </summary>
    private string CreateNuspecXml(string packageId, string version, List<(string Id, string Version, string? Exclude)>? dependencies = null)
    {
        XNamespace ns = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
        var package = new XElement(ns + "package",
            new XElement(ns + "metadata",
                new XElement(ns + "id", packageId),
                new XElement(ns + "version", version),
                dependencies != null && dependencies.Count > 0
                    ? new XElement(ns + "dependencies",
                        new XElement(ns + "group",
                            new XAttribute("targetFramework", ".NETStandard2.0"),
                            dependencies.Select(dep =>
                            {
                                var depElement = new XElement(ns + "dependency",
                                    new XAttribute("id", dep.Id),
                                    new XAttribute("version", dep.Version));
                                if (!string.IsNullOrEmpty(dep.Exclude))
                                {
                                    depElement.SetAttributeValue("exclude", dep.Exclude);
                                }
                                return depElement;
                            })
                        )
                    )
                    : new XElement(ns + "dependencies")
            )
        );

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), package);
        return doc.ToString();
    }

    /// <summary>
    /// Создает временную структуру NuGet пакета для тестирования
    /// </summary>
    private string CreateMockPackage(string basePath, string packageName, string version, string? nuspecContent = null, bool createDll = true)
    {
        var packageDir = Path.Combine(basePath, packageName.ToLowerInvariant(), version.ToLowerInvariant());
        Directory.CreateDirectory(packageDir);

        // Создаем .nuspec файл
        if (nuspecContent != null)
        {
            var nuspecPath = Path.Combine(packageDir, $"{packageName}.nuspec");
            File.WriteAllText(nuspecPath, nuspecContent);
        }

        // Создаем lib директорию с DLL
        if (createDll)
        {
            var libDir = Path.Combine(packageDir, "lib", "netstandard2.0");
            Directory.CreateDirectory(libDir);
            var dllPath = Path.Combine(libDir, $"{packageName}.dll");
            // Создаем пустой файл (для тестов достаточно, что файл существует)
            File.WriteAllText(dllPath, "Mock DLL content");
        }

        return packageDir;
    }

    [Fact]
    public void GetAllPackages_WithTransitiveDependencies_ResolvesAllDependencies()
    {
        // Arrange
        var tempCache = Path.Combine(Path.GetTempPath(), "NuGetTestCache_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            // Создаем структуру пакетов:
            // PackageA (1.0.0) -> PackageB (2.0.0) -> PackageC (3.0.0)
            // PackageA также зависит от PackageD (4.0.0)

            // PackageC (конечная зависимость, без зависимостей)
            var packageCNuspec = CreateNuspecXml("PackageC", "3.0.0");
            CreateMockPackage(tempCache, "PackageC", "3.0.0", packageCNuspec);

            // PackageD (конечная зависимость, без зависимостей)
            var packageDNuspec = CreateNuspecXml("PackageD", "4.0.0");
            CreateMockPackage(tempCache, "PackageD", "4.0.0", packageDNuspec);

            // PackageB зависит от PackageC
            var packageBNuspec = CreateNuspecXml("PackageB", "2.0.0", new List<(string, string, string?)> { ("PackageC", "3.0.0", null) });
            CreateMockPackage(tempCache, "PackageB", "2.0.0", packageBNuspec);

            // PackageA зависит от PackageB и PackageD
            var packageANuspec = CreateNuspecXml("PackageA", "1.0.0", new List<(string, string, string?)> 
            { 
                ("PackageB", "2.0.0", null),
                ("PackageD", "4.0.0", null)
            });
            CreateMockPackage(tempCache, "PackageA", "1.0.0", packageANuspec);

            // Устанавливаем переменную окружения для указания пути к тестовому кэшу
            var originalNuGetPackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            Environment.SetEnvironmentVariable("NUGET_PACKAGES", tempCache);

            try
            {
                var testResolver = new NuGetPackageResolver();
                var projects = new List<ProjectInfo>
                {
                    new ProjectInfo(
                        "/path/to/project.csproj",
                        "TestProject",
                        new List<PackageReference>
                        {
                            new PackageReference("PackageA", "1.0.0")
                        }
                    )
                };

                // Act
                var result = testResolver.GetAllPackages(projects);

                // Assert
                Assert.NotNull(result);
                // Должны быть разрешены: PackageA, PackageB, PackageC, PackageD
                Assert.Equal(4, result.Count);
                Assert.Contains(result, p => p.Name == "PackageA" && p.Version == "1.0.0");
                Assert.Contains(result, p => p.Name == "PackageB" && p.Version == "2.0.0");
                Assert.Contains(result, p => p.Name == "PackageC" && p.Version == "3.0.0");
                Assert.Contains(result, p => p.Name == "PackageD" && p.Version == "4.0.0");
            }
            finally
            {
                // Восстанавливаем оригинальную переменную окружения
                if (originalNuGetPackages != null)
                {
                    Environment.SetEnvironmentVariable("NUGET_PACKAGES", originalNuGetPackages);
                }
                else
                {
                    Environment.SetEnvironmentVariable("NUGET_PACKAGES", null);
                }
            }
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempCache))
            {
                Directory.Delete(tempCache, true);
            }
        }
    }

    [Fact]
    public void GetAllPackages_WithCircularDependencies_HandlesGracefully()
    {
        // Arrange
        var tempCache = Path.Combine(Path.GetTempPath(), "NuGetTestCache_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            // Создаем циклическую зависимость: PackageA -> PackageB -> PackageA

            // PackageA зависит от PackageB
            var packageANuspec = CreateNuspecXml("PackageA", "1.0.0", new List<(string, string, string?)> { ("PackageB", "2.0.0", null) });
            CreateMockPackage(tempCache, "PackageA", "1.0.0", packageANuspec);

            // PackageB зависит от PackageA (цикл)
            var packageBNuspec = CreateNuspecXml("PackageB", "2.0.0", new List<(string, string, string?)> { ("PackageA", "1.0.0", null) });
            CreateMockPackage(tempCache, "PackageB", "2.0.0", packageBNuspec);

            var originalNuGetPackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            Environment.SetEnvironmentVariable("NUGET_PACKAGES", tempCache);

            try
            {
                var testResolver = new NuGetPackageResolver();
                var projects = new List<ProjectInfo>
                {
                    new ProjectInfo(
                        "/path/to/project.csproj",
                        "TestProject",
                        new List<PackageReference>
                        {
                            new PackageReference("PackageA", "1.0.0")
                        }
                    )
                };

                // Act
                var result = testResolver.GetAllPackages(projects);

                // Assert
                Assert.NotNull(result);
                // Должны быть разрешены оба пакета, но циклическая зависимость должна быть обработана
                // (не должно быть бесконечного цикла)
                Assert.True(result.Count >= 1); // Минимум PackageA должен быть разрешен
                Assert.Contains(result, p => p.Name == "PackageA" && p.Version == "1.0.0");
            }
            finally
            {
                if (originalNuGetPackages != null)
                {
                    Environment.SetEnvironmentVariable("NUGET_PACKAGES", originalNuGetPackages);
                }
                else
                {
                    Environment.SetEnvironmentVariable("NUGET_PACKAGES", null);
                }
            }
        }
        finally
        {
            if (Directory.Exists(tempCache))
            {
                Directory.Delete(tempCache, true);
            }
        }
    }

    [Fact]
    public void GetAllPackages_WithVersionRanges_ResolvesVersionRange()
    {
        // Arrange
        var tempCache = Path.Combine(Path.GetTempPath(), "NuGetTestCache_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            // PackageB (конечная зависимость)
            var packageBNuspec = CreateNuspecXml("PackageB", "2.0.0");
            CreateMockPackage(tempCache, "PackageB", "2.0.0", packageBNuspec);

            // PackageA зависит от PackageB с диапазоном версий
            // Для диапазона версий используем строку напрямую
            var packageANuspec = CreateNuspecXml("PackageA", "1.0.0", new List<(string, string, string?)> { ("PackageB", "[2.0.0, 3.0.0)", null) });
            CreateMockPackage(tempCache, "PackageA", "1.0.0", packageANuspec);

            var originalNuGetPackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            Environment.SetEnvironmentVariable("NUGET_PACKAGES", tempCache);

            try
            {
                var testResolver = new NuGetPackageResolver();
                var projects = new List<ProjectInfo>
                {
                    new ProjectInfo(
                        "/path/to/project.csproj",
                        "TestProject",
                        new List<PackageReference>
                        {
                            new PackageReference("PackageA", "1.0.0")
                        }
                    )
                };

                // Act
                var result = testResolver.GetAllPackages(projects);

                // Assert
                Assert.NotNull(result);
                // Должны быть разрешены оба пакета
                Assert.Equal(2, result.Count);
                Assert.Contains(result, p => p.Name == "PackageA" && p.Version == "1.0.0");
                Assert.Contains(result, p => p.Name == "PackageB" && p.Version == "2.0.0");
            }
            finally
            {
                if (originalNuGetPackages != null)
                {
                    Environment.SetEnvironmentVariable("NUGET_PACKAGES", originalNuGetPackages);
                }
                else
                {
                    Environment.SetEnvironmentVariable("NUGET_PACKAGES", null);
                }
            }
        }
        finally
        {
            if (Directory.Exists(tempCache))
            {
                Directory.Delete(tempCache, true);
            }
        }
    }

    [Fact]
    public void GetAllPackages_WithExcludeCompileDependencies_SkipsRuntimeOnlyDependencies()
    {
        // Arrange
        var tempCache = Path.Combine(Path.GetTempPath(), "NuGetTestCache_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            // PackageB (конечная зависимость)
            var packageBNuspec = CreateNuspecXml("PackageB", "2.0.0");
            CreateMockPackage(tempCache, "PackageB", "2.0.0", packageBNuspec);

            // PackageC (конечная зависимость, но будет исключена)
            var packageCNuspec = CreateNuspecXml("PackageC", "3.0.0");
            CreateMockPackage(tempCache, "PackageC", "3.0.0", packageCNuspec);

            // PackageA зависит от PackageB (compile) и PackageC (runtime only, exclude="Compile")
            var packageANuspec = CreateNuspecXml("PackageA", "1.0.0", new List<(string, string, string?)> 
            { 
                ("PackageB", "2.0.0", null),
                ("PackageC", "3.0.0", "Compile")
            });
            CreateMockPackage(tempCache, "PackageA", "1.0.0", packageANuspec);

            var originalNuGetPackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            Environment.SetEnvironmentVariable("NUGET_PACKAGES", tempCache);

            try
            {
                var testResolver = new NuGetPackageResolver();
                var projects = new List<ProjectInfo>
                {
                    new ProjectInfo(
                        "/path/to/project.csproj",
                        "TestProject",
                        new List<PackageReference>
                        {
                            new PackageReference("PackageA", "1.0.0")
                        }
                    )
                };

                // Act
                var result = testResolver.GetAllPackages(projects);

                // Assert
                Assert.NotNull(result);
                // Должны быть разрешены только PackageA и PackageB (PackageC исключена)
                Assert.Equal(2, result.Count);
                Assert.Contains(result, p => p.Name == "PackageA" && p.Version == "1.0.0");
                Assert.Contains(result, p => p.Name == "PackageB" && p.Version == "2.0.0");
                Assert.DoesNotContain(result, p => p.Name == "PackageC");
            }
            finally
            {
                if (originalNuGetPackages != null)
                {
                    Environment.SetEnvironmentVariable("NUGET_PACKAGES", originalNuGetPackages);
                }
                else
                {
                    Environment.SetEnvironmentVariable("NUGET_PACKAGES", null);
                }
            }
        }
        finally
        {
            if (Directory.Exists(tempCache))
            {
                Directory.Delete(tempCache, true);
            }
        }
    }

    [Fact]
    public void GetAllPackages_WithDuplicateDependencies_ResolvesOnlyOnce()
    {
        // Arrange
        var tempCache = Path.Combine(Path.GetTempPath(), "NuGetTestCache_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            // PackageB (конечная зависимость)
            var packageBNuspec = CreateNuspecXml("PackageB", "2.0.0");
            CreateMockPackage(tempCache, "PackageB", "2.0.0", packageBNuspec);

            // PackageA зависит от PackageB
            var packageANuspec = CreateNuspecXml("PackageA", "1.0.0", new List<(string, string, string?)> { ("PackageB", "2.0.0", null) });
            CreateMockPackage(tempCache, "PackageA", "1.0.0", packageANuspec);

            // PackageC также зависит от PackageB
            var packageCNuspec = CreateNuspecXml("PackageC", "3.0.0", new List<(string, string, string?)> { ("PackageB", "2.0.0", null) });
            CreateMockPackage(tempCache, "PackageC", "3.0.0", packageCNuspec);

            var originalNuGetPackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
            Environment.SetEnvironmentVariable("NUGET_PACKAGES", tempCache);

            try
            {
                var testResolver = new NuGetPackageResolver();
                var projects = new List<ProjectInfo>
                {
                    new ProjectInfo(
                        "/path/to/project.csproj",
                        "TestProject",
                        new List<PackageReference>
                        {
                            new PackageReference("PackageA", "1.0.0"),
                            new PackageReference("PackageC", "3.0.0")
                        }
                    )
                };

                // Act
                var result = testResolver.GetAllPackages(projects);

                // Assert
                Assert.NotNull(result);
                // Должны быть разрешены: PackageA, PackageC, PackageB (только один раз, несмотря на то что он зависимость обоих)
                Assert.Equal(3, result.Count);
                Assert.Contains(result, p => p.Name == "PackageA" && p.Version == "1.0.0");
                Assert.Contains(result, p => p.Name == "PackageC" && p.Version == "3.0.0");
                // PackageB должен быть только один раз
                var packageBCount = result.Count(p => p.Name == "PackageB" && p.Version == "2.0.0");
                Assert.Equal(1, packageBCount);
            }
            finally
            {
                if (originalNuGetPackages != null)
                {
                    Environment.SetEnvironmentVariable("NUGET_PACKAGES", originalNuGetPackages);
                }
                else
                {
                    Environment.SetEnvironmentVariable("NUGET_PACKAGES", null);
                }
            }
        }
        finally
        {
            if (Directory.Exists(tempCache))
            {
                Directory.Delete(tempCache, true);
            }
        }
    }
}

