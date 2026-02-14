using Disassembly.Tool.Core;
using System.Reflection;
using Xunit;

namespace Disassembly.Tool.Tests.Core;

/// <summary>
/// Тесты для класса AssemblyReflector
/// </summary>
public class AssemblyReflectorTests : IDisposable
{
    private AssemblyReflector? _reflector;

    [Fact]
    public void ReadAssembly_WhenFileDoesNotExist_ThrowsFileNotFoundException()
    {
        // Arrange
        _reflector = new AssemblyReflector();
        var nonExistentPath = "/nonexistent/path/to/assembly.dll";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => _reflector.ReadAssembly(nonExistentPath));
    }

    [Fact]
    public void ReadAssembly_WithValidAssembly_ReturnsTypeMetadata()
    {
        // Arrange
        _reflector = new AssemblyReflector();
        // Используем текущую тестовую сборку вместо системной
        // Системные сборки могут не загружаться в изолированном контексте
        var testAssembly = typeof(AssemblyReflectorTests).Assembly;
        var assemblyPath = testAssembly.Location;

        // Act
        var types = _reflector.ReadAssembly(assemblyPath);

        // Assert
        Assert.NotNull(types);
        // Должны быть только публичные типы
        Assert.All(types, t => Assert.True(t.Name != null && !string.IsNullOrEmpty(t.Name)));
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes_WithoutException()
    {
        // Arrange
        _reflector = new AssemblyReflector();

        // Act & Assert
        _reflector.Dispose();
        _reflector.Dispose(); // Не должно выбрасывать исключение
    }

    [Fact]
    public void TypeMetadata_RecordProperties_AreAccessible()
    {
        // Arrange & Act
        var typeMetadata = new TypeMetadata(
            "TestType",
            "TestNamespace",
            TypeKind.Class,
            false,
            new List<string>(),
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            null,
            new List<AttributeMetadata>()
        );

        // Assert
        Assert.Equal("TestType", typeMetadata.Name);
        Assert.Equal("TestNamespace", typeMetadata.Namespace);
        Assert.Equal(TypeKind.Class, typeMetadata.Kind);
        Assert.False(typeMetadata.IsGeneric);
        Assert.Empty(typeMetadata.GenericParameters);
        Assert.Empty(typeMetadata.Members);
        Assert.Empty(typeMetadata.NestedTypes);
    }

    [Fact]
    public void TypeMetadata_WithGenericParameters_IsCorrect()
    {
        // Arrange & Act
        var typeMetadata = new TypeMetadata(
            "GenericType",
            "TestNamespace",
            TypeKind.Class,
            true,
            new List<string> { "T", "U" },
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            null,
            new List<AttributeMetadata>()
        );

        // Assert
        Assert.True(typeMetadata.IsGeneric);
        Assert.Equal(2, typeMetadata.GenericParameters.Count);
        Assert.Contains("T", typeMetadata.GenericParameters);
        Assert.Contains("U", typeMetadata.GenericParameters);
    }

    [Fact]
    public void MemberMetadata_RecordProperties_AreAccessible()
    {
        // Arrange & Act
        var memberMetadata = new MemberMetadata(
            "TestMethod",
            MemberType.Method,
            "void TestMethod()",
            typeof(void),
            new List<ParameterMetadata>(),
            null,
            new List<AttributeMetadata>()
        );

        // Assert
        Assert.Equal("TestMethod", memberMetadata.Name);
        Assert.Equal(MemberType.Method, memberMetadata.Type);
        Assert.Equal("void TestMethod()", memberMetadata.Signature);
        Assert.Equal(typeof(void), memberMetadata.ReturnType);
        Assert.Empty(memberMetadata.Parameters);
    }

    [Fact]
    public void MemberMetadata_WithParameters_IsCorrect()
    {
        // Arrange & Act
        var parameters = new List<ParameterMetadata>
        {
            new ParameterMetadata("param1", "string", false, null),
            new ParameterMetadata("param2", "int", true, 0)
        };
        var memberMetadata = new MemberMetadata(
            "TestMethod",
            MemberType.Method,
            "void TestMethod(string param1, int param2)",
            typeof(void),
            parameters,
            null,
            new List<AttributeMetadata>()
        );

        // Assert
        Assert.Equal(2, memberMetadata.Parameters.Count);
        Assert.Equal("param1", memberMetadata.Parameters[0].Name);
        Assert.Equal("string", memberMetadata.Parameters[0].TypeName);
        Assert.False(memberMetadata.Parameters[0].IsOptional);
        Assert.Equal("param2", memberMetadata.Parameters[1].Name);
        Assert.True(memberMetadata.Parameters[1].IsOptional);
    }

    [Fact]
    public void ParameterMetadata_RecordProperties_AreAccessible()
    {
        // Arrange & Act
        var paramMetadata = new ParameterMetadata(
            "testParam",
            "string",
            false,
            null
        );

        // Assert
        Assert.Equal("testParam", paramMetadata.Name);
        Assert.Equal("string", paramMetadata.TypeName);
        Assert.False(paramMetadata.IsOptional);
        Assert.Null(paramMetadata.DefaultValue);
    }

    [Fact]
    public void ParameterMetadata_WithDefaultValue_IsCorrect()
    {
        // Arrange & Act
        var paramMetadata = new ParameterMetadata(
            "testParam",
            "int",
            true,
            42
        );

        // Assert
        Assert.True(paramMetadata.IsOptional);
        Assert.Equal(42, paramMetadata.DefaultValue);
    }

    [Fact]
    public void TypeKind_EnumValues_AreDefined()
    {
        // Arrange & Act
        var classKind = TypeKind.Class;
        var interfaceKind = TypeKind.Interface;
        var structKind = TypeKind.Struct;
        var enumKind = TypeKind.Enum;
        var delegateKind = TypeKind.Delegate;

        // Assert
        Assert.Equal(0, (int)classKind);
        Assert.Equal(1, (int)interfaceKind);
        Assert.Equal(2, (int)structKind);
        Assert.Equal(3, (int)enumKind);
        Assert.Equal(4, (int)delegateKind);
    }

    [Fact]
    public void MemberType_EnumValues_AreDefined()
    {
        // Arrange & Act
        var methodType = MemberType.Method;
        var propertyType = MemberType.Property;
        var fieldType = MemberType.Field;
        var eventType = MemberType.Event;
        var constructorType = MemberType.Constructor;

        // Assert
        Assert.Equal(0, (int)methodType);
        Assert.Equal(1, (int)propertyType);
        Assert.Equal(2, (int)fieldType);
        Assert.Equal(3, (int)eventType);
        Assert.Equal(4, (int)constructorType);
    }

    public void Dispose()
    {
        _reflector?.Dispose();
    }
}

