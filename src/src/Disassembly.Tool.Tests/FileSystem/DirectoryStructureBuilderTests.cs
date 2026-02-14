using Disassembly.Tool.FileSystem;
using Disassembly.Tool.Core;
using Xunit;

namespace Disassembly.Tool.Tests.FileSystem;

/// <summary>
/// Тесты для класса DirectoryStructureBuilder
/// </summary>
public class DirectoryStructureBuilderTests
{
    private readonly DirectoryStructureBuilder _builder;

    public DirectoryStructureBuilderTests()
    {
        _builder = new DirectoryStructureBuilder();
    }

    [Fact]
    public void CreatePackageDirectory_CreatesCorrectPath()
    {
        // Arrange
        var outputRoot = Path.GetTempPath();
        var packageInfo = new PackageInfo(
            "TestPackage",
            "1.0.0",
            "/path/to/package.dll",
            null
        );

        try
        {
            // Act
            var result = _builder.CreatePackageDirectory(outputRoot, packageInfo);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("TestPackage", result);
            Assert.Contains("1.0.0", result);
            Assert.True(Directory.Exists(result));
        }
        finally
        {
            // Cleanup
            var packageDir = Path.Combine(outputRoot, "TestPackage", "1.0.0");
            if (Directory.Exists(packageDir))
            {
                Directory.Delete(packageDir, true);
            }
        }
    }

    [Fact]
    public void GetFilePathForType_WithNamespace_CreatesSubdirectories()
    {
        // Arrange
        var outputRoot = Path.GetTempPath();
        var packageInfo = new PackageInfo(
            "TestPackage",
            "1.0.0",
            "/path/to/package.dll",
            null
        );
        var packageRoot = _builder.CreatePackageDirectory(outputRoot, packageInfo);

        var typeMetadata = new TypeMetadata(
            "TestClass",
            "TestNamespace.SubNamespace",
            TypeKind.Class,
            false,
            new List<string>(),
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            null
        );

        try
        {
            // Act
            var result = _builder.GetFilePathForType(packageRoot, typeMetadata, "TestClass.cs");

            // Assert
            Assert.NotNull(result);
            Assert.Contains("TestNamespace", result);
            Assert.Contains("SubNamespace", result);
            Assert.Contains("TestClass.cs", result);
            Assert.True(Directory.Exists(Path.GetDirectoryName(result)));
        }
        finally
        {
            // Cleanup
            var packageDir = Path.Combine(outputRoot, "TestPackage");
            if (Directory.Exists(packageDir))
            {
                Directory.Delete(packageDir, true);
            }
        }
    }

    [Fact]
    public void GetFilePathForType_WithoutNamespace_UsesPackageRoot()
    {
        // Arrange
        var outputRoot = Path.GetTempPath();
        var packageInfo = new PackageInfo(
            "TestPackage",
            "1.0.0",
            "/path/to/package.dll",
            null
        );
        var packageRoot = _builder.CreatePackageDirectory(outputRoot, packageInfo);

        var typeMetadata = new TypeMetadata(
            "TestClass",
            null,
            TypeKind.Class,
            false,
            new List<string>(),
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            null
        );

        try
        {
            // Act
            var result = _builder.GetFilePathForType(packageRoot, typeMetadata, "TestClass.cs");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(Path.Combine(packageRoot, "TestClass.cs"), result);
        }
        finally
        {
            // Cleanup
            var packageDir = Path.Combine(outputRoot, "TestPackage");
            if (Directory.Exists(packageDir))
            {
                Directory.Delete(packageDir, true);
            }
        }
    }

    [Fact]
    public void GetFilePathForType_WithEmptyNamespace_UsesPackageRoot()
    {
        // Arrange
        var outputRoot = Path.GetTempPath();
        var packageInfo = new PackageInfo(
            "TestPackage",
            "1.0.0",
            "/path/to/package.dll",
            null
        );
        var packageRoot = _builder.CreatePackageDirectory(outputRoot, packageInfo);

        var typeMetadata = new TypeMetadata(
            "TestClass",
            "",
            TypeKind.Class,
            false,
            new List<string>(),
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            null
        );

        try
        {
            // Act
            var result = _builder.GetFilePathForType(packageRoot, typeMetadata, "TestClass.cs");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(Path.Combine(packageRoot, "TestClass.cs"), result);
        }
        finally
        {
            // Cleanup
            var packageDir = Path.Combine(outputRoot, "TestPackage");
            if (Directory.Exists(packageDir))
            {
                Directory.Delete(packageDir, true);
            }
        }
    }

    [Fact]
    public void OrganizeByNamespace_GroupsTypesByNamespace()
    {
        // Arrange
        var types = new List<TypeMetadata>
        {
            new TypeMetadata("Class1", "Namespace1", TypeKind.Class, false, new List<string>(), new List<MemberMetadata>(), new List<TypeMetadata>(), null),
            new TypeMetadata("Class2", "Namespace1", TypeKind.Class, false, new List<string>(), new List<MemberMetadata>(), new List<TypeMetadata>(), null),
            new TypeMetadata("Class3", "Namespace2", TypeKind.Class, false, new List<string>(), new List<MemberMetadata>(), new List<TypeMetadata>(), null),
            new TypeMetadata("Class4", null, TypeKind.Class, false, new List<string>(), new List<MemberMetadata>(), new List<TypeMetadata>(), null)
        };

        // Act
        var result = _builder.OrganizeByNamespace(types);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Contains("Namespace1", result.Keys);
        Assert.Contains("Namespace2", result.Keys);
        Assert.Contains("Global", result.Keys);
        Assert.Equal(2, result["Namespace1"].Count);
        Assert.Single(result["Namespace2"]);
        Assert.Single(result["Global"]);
    }

    [Fact]
    public void OrganizeByNamespace_WithEmptyList_ReturnsEmptyDictionary()
    {
        // Arrange
        var types = new List<TypeMetadata>();

        // Act
        var result = _builder.OrganizeByNamespace(types);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void OrganizeByNamespace_WithNullNamespaces_UsesGlobal()
    {
        // Arrange
        var types = new List<TypeMetadata>
        {
            new TypeMetadata("Class1", null, TypeKind.Class, false, new List<string>(), new List<MemberMetadata>(), new List<TypeMetadata>(), null),
            new TypeMetadata("Class2", null, TypeKind.Class, false, new List<string>(), new List<MemberMetadata>(), new List<TypeMetadata>(), null)
        };

        // Act
        var result = _builder.OrganizeByNamespace(types);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Contains("Global", result.Keys);
        Assert.Equal(2, result["Global"].Count);
    }
}

