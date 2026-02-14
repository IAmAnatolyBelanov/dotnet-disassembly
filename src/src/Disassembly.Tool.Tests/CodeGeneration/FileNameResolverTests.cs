using Disassembly.Tool.CodeGeneration;
using Disassembly.Tool.Core;
using Xunit;

namespace Disassembly.Tool.Tests.CodeGeneration;

/// <summary>
/// Тесты для класса FileNameResolver
/// </summary>
public class FileNameResolverTests
{
    private readonly FileNameResolver _resolver;

    public FileNameResolverTests()
    {
        _resolver = new FileNameResolver();
    }

    [Fact]
    public void ResolveFileName_ForFirstOccurrence_ReturnsBaseName()
    {
        // Arrange
        var typeMetadata = new TypeMetadata(
            "TestClass",
            "TestNamespace",
            TypeKind.Class,
            false,
            new List<string>(),
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            null
        );
        var nameCounters = new Dictionary<string, int>();

        // Act
        var result = _resolver.ResolveFileName(typeMetadata, nameCounters);

        // Assert
        Assert.Equal("TestClass.cs", result);
        Assert.Contains("TestNamespace.TestClass", nameCounters.Keys);
        Assert.Equal(0, nameCounters["TestNamespace.TestClass"]);
    }

    [Fact]
    public void ResolveFileName_ForSecondOccurrence_ReturnsNameWithIndex1()
    {
        // Arrange
        var typeMetadata = new TypeMetadata(
            "TestClass",
            "TestNamespace",
            TypeKind.Class,
            false,
            new List<string>(),
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            null
        );
        var nameCounters = new Dictionary<string, int>
        {
            { "TestNamespace.TestClass", 0 }
        };

        // Act
        var result = _resolver.ResolveFileName(typeMetadata, nameCounters);

        // Assert
        Assert.Equal("TestClass1.cs", result);
        Assert.Equal(1, nameCounters["TestNamespace.TestClass"]);
    }

    [Fact]
    public void ResolveFileName_ForThirdOccurrence_ReturnsNameWithIndex2()
    {
        // Arrange
        var typeMetadata = new TypeMetadata(
            "TestClass",
            "TestNamespace",
            TypeKind.Class,
            false,
            new List<string>(),
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            null
        );
        var nameCounters = new Dictionary<string, int>
        {
            { "TestNamespace.TestClass", 1 }
        };

        // Act
        var result = _resolver.ResolveFileName(typeMetadata, nameCounters);

        // Assert
        Assert.Equal("TestClass2.cs", result);
        Assert.Equal(2, nameCounters["TestNamespace.TestClass"]);
    }

    [Fact]
    public void ResolveFileName_WithNullNamespace_UsesGlobal()
    {
        // Arrange
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
        var nameCounters = new Dictionary<string, int>();

        // Act
        var result = _resolver.ResolveFileName(typeMetadata, nameCounters);

        // Assert
        Assert.Equal("TestClass.cs", result);
        Assert.Contains("Global.TestClass", nameCounters.Keys);
    }

    [Fact]
    public void ResolveFileName_WithGenericType_UsesBaseName()
    {
        // Arrange
        var typeMetadata = new TypeMetadata(
            "GenericClass",
            "TestNamespace",
            TypeKind.Class,
            true,
            new List<string> { "T" },
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            null
        );
        var nameCounters = new Dictionary<string, int>();

        // Act
        var result = _resolver.ResolveFileName(typeMetadata, nameCounters);

        // Assert
        Assert.Equal("GenericClass.cs", result);
    }

    [Fact]
    public void InitializeNameCounters_WithUniqueTypes_ReturnsEmptyDictionary()
    {
        // Arrange
        var types = new List<TypeMetadata>
        {
            new TypeMetadata("Class1", "Namespace1", TypeKind.Class, false, new List<string>(), new List<MemberMetadata>(), new List<TypeMetadata>(), null),
            new TypeMetadata("Class2", "Namespace1", TypeKind.Class, false, new List<string>(), new List<MemberMetadata>(), new List<TypeMetadata>(), null),
            new TypeMetadata("Class1", "Namespace2", TypeKind.Class, false, new List<string>(), new List<MemberMetadata>(), new List<TypeMetadata>(), null)
        };

        // Act
        var result = _resolver.InitializeNameCounters(types);

        // Assert
        Assert.NotNull(result);
        // Все типы уникальны по ключу (namespace + name), поэтому счетчики должны быть пустыми
        Assert.Empty(result);
    }

    [Fact]
    public void InitializeNameCounters_WithDuplicateTypes_ReturnsCounters()
    {
        // Arrange
        var types = new List<TypeMetadata>
        {
            new TypeMetadata("TestClass", "TestNamespace", TypeKind.Class, false, new List<string>(), new List<MemberMetadata>(), new List<TypeMetadata>(), null),
            new TypeMetadata("TestClass", "TestNamespace", TypeKind.Class, false, new List<string>(), new List<MemberMetadata>(), new List<TypeMetadata>(), null),
            new TypeMetadata("TestClass", "TestNamespace", TypeKind.Class, false, new List<string>(), new List<MemberMetadata>(), new List<TypeMetadata>(), null)
        };

        // Act
        var result = _resolver.InitializeNameCounters(types);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("TestNamespace.TestClass", result.Keys);
        // Первое вхождение не учитывается, остальные два должны дать счетчик 2
        Assert.Equal(2, result["TestNamespace.TestClass"]);
    }

    [Fact]
    public void InitializeNameCounters_WithEmptyList_ReturnsEmptyDictionary()
    {
        // Arrange
        var types = new List<TypeMetadata>();

        // Act
        var result = _resolver.InitializeNameCounters(types);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}

