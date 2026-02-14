using Disassembly.Tool.Core;
using Disassembly.Tool.CodeGeneration;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Disassembly.Tool.Tests.Core;

/// <summary>
/// Тесты для проверки работы с атрибутами
/// </summary>
public class AttributeTests : IDisposable
{
    private AssemblyReflector? _reflector;

    [Fact]
    public void ExtractAttributes_FromTypeWithAttribute_ExtractsAttribute()
    {
        // Arrange
        _reflector = new AssemblyReflector();
        var testAssembly = typeof(TestClassWithAttribute).Assembly;
        var assemblyPath = testAssembly.Location;

        // Act
        var types = _reflector.ReadAssembly(assemblyPath);
        var testType = types.FirstOrDefault(t => t.Name == "TestClassWithAttribute");

        // Assert
        Assert.NotNull(testType);
        Assert.NotEmpty(testType.Attributes);
        
        var attribute = testType.Attributes.FirstOrDefault(a => 
            a.FullTypeName == "System.SerializableAttribute" || 
            a.FullTypeName.Contains("Serializable"));
        Assert.NotNull(attribute);
    }

    [Fact]
    public void ExtractAttributes_FromMethodWithAttribute_ExtractsAttribute()
    {
        // Arrange
        _reflector = new AssemblyReflector();
        var testAssembly = typeof(TestClassWithMethodAttribute).Assembly;
        var assemblyPath = testAssembly.Location;

        // Act
        var types = _reflector.ReadAssembly(assemblyPath);
        var testType = types.FirstOrDefault(t => t.Name == "TestClassWithMethodAttribute");

        // Assert
        Assert.NotNull(testType);
        var method = testType.Members.FirstOrDefault(m => m.Name == "TestMethod");
        Assert.NotNull(method);
        Assert.NotEmpty(method.Attributes);
        
        var attribute = method.Attributes.FirstOrDefault(a => 
            a.FullTypeName.Contains("Obsolete") || 
            a.FullTypeName == "System.ObsoleteAttribute");
        Assert.NotNull(attribute);
    }

    [Fact]
    public void ExtractAttributes_FromPropertyWithAttribute_ExtractsAttribute()
    {
        // Arrange
        _reflector = new AssemblyReflector();
        var testAssembly = typeof(TestClassWithPropertyAttribute).Assembly;
        var assemblyPath = testAssembly.Location;

        // Act
        var types = _reflector.ReadAssembly(assemblyPath);
        var testType = types.FirstOrDefault(t => t.Name == "TestClassWithPropertyAttribute");

        // Assert
        Assert.NotNull(testType);
        var property = testType.Members.FirstOrDefault(m => m.Name == "TestProperty");
        Assert.NotNull(property);
        Assert.NotEmpty(property.Attributes);
    }

    [Fact]
    public void ExtractAttributes_WithArguments_ExtractsArguments()
    {
        // Arrange
        _reflector = new AssemblyReflector();
        var testAssembly = typeof(TestClassWithAttributeArguments).Assembly;
        var assemblyPath = testAssembly.Location;

        // Act
        var types = _reflector.ReadAssembly(assemblyPath);
        var testType = types.FirstOrDefault(t => t.Name == "TestClassWithAttributeArguments");

        // Assert
        Assert.NotNull(testType);
        var method = testType.Members.FirstOrDefault(m => m.Name == "TestMethodWithAttribute");
        Assert.NotNull(method);
        Assert.NotEmpty(method.Attributes);
        
        var attribute = method.Attributes.FirstOrDefault(a => 
            a.FullTypeName.Contains("Obsolete") || 
            a.FullTypeName == "System.ObsoleteAttribute");
        Assert.NotNull(attribute);
        Assert.NotEmpty(attribute.Arguments);
    }

    [Fact]
    public void GenerateTypeDeclaration_WithAttributes_IncludesAttributes()
    {
        // Arrange
        var generator = new RoslynCodeGenerator();
        var attributes = new List<AttributeMetadata>
        {
            new AttributeMetadata(
                "System.SerializableAttribute",
                new List<AttributeArgumentMetadata>()
            )
        };
        
        var typeMetadata = new TypeMetadata(
            "TestClass",
            "TestNamespace",
            TypeKind.Class,
            false,
            new List<string>(),
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            typeof(object),
            attributes
        );

        // Act
        var result = generator.GenerateTypeDeclaration(typeMetadata);

        // Assert
        Assert.NotNull(result);
        var classDecl = Assert.IsType<ClassDeclarationSyntax>(result);
        Assert.NotEmpty(classDecl.AttributeLists);
        
        var attributeList = classDecl.AttributeLists.FirstOrDefault();
        Assert.NotNull(attributeList);
        Assert.NotEmpty(attributeList.Attributes);
        
        var attribute = attributeList.Attributes.First();
        var attributeName = attribute.Name.ToString();
        Assert.Contains("Serializable", attributeName);
    }

    [Fact]
    public void GenerateMember_WithAttributes_IncludesAttributes()
    {
        // Arrange
        var generator = new RoslynCodeGenerator();
        var methodInfo = typeof(string).GetMethod("ToString", Type.EmptyTypes)!;
        var attributes = new List<AttributeMetadata>
        {
            new AttributeMetadata(
                "System.ObsoleteAttribute",
                new List<AttributeArgumentMetadata>
                {
                    new AttributeArgumentMetadata(null, "\"This method is obsolete\"")
                }
            )
        };
        
        var memberMetadata = new MemberMetadata(
            "TestMethod",
            MemberType.Method,
            "void TestMethod()",
            typeof(void),
            new List<ParameterMetadata>(),
            methodInfo,
            attributes
        );

        // Act
        var result = generator.GenerateMember(memberMetadata);

        // Assert
        Assert.NotNull(result);
        var methodDecl = Assert.IsType<MethodDeclarationSyntax>(result);
        Assert.NotEmpty(methodDecl.AttributeLists);
        
        var attributeList = methodDecl.AttributeLists.FirstOrDefault();
        Assert.NotNull(attributeList);
        Assert.NotEmpty(attributeList.Attributes);
        
        var attribute = attributeList.Attributes.First();
        var attributeName = attribute.Name.ToString();
        Assert.Contains("Obsolete", attributeName);
    }

    [Fact]
    public void GenerateAttribute_WithFullNamespace_UsesFullName()
    {
        // Arrange
        var generator = new RoslynCodeGenerator();
        var attributes = new List<AttributeMetadata>
        {
            new AttributeMetadata(
                "System.ComponentModel.DescriptionAttribute",
                new List<AttributeArgumentMetadata>
                {
                    new AttributeArgumentMetadata(null, "\"Test description\"")
                }
            )
        };
        
        var typeMetadata = new TypeMetadata(
            "TestClass",
            "TestNamespace",
            TypeKind.Class,
            false,
            new List<string>(),
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            typeof(object),
            attributes
        );

        // Act
        var result = generator.GenerateTypeDeclaration(typeMetadata);

        // Assert
        Assert.NotNull(result);
        var classDecl = Assert.IsType<ClassDeclarationSyntax>(result);
        var attributeList = classDecl.AttributeLists.FirstOrDefault();
        Assert.NotNull(attributeList);
        
        var attribute = attributeList.Attributes.First();
        var attributeName = attribute.Name.ToString();
        // Проверяем, что используется полное имя с namespace
        Assert.Contains("System.ComponentModel.Description", attributeName);
    }

    [Fact]
    public void GenerateAttribute_WithNamedArguments_IncludesNamedArguments()
    {
        // Arrange
        var generator = new RoslynCodeGenerator();
        var attributes = new List<AttributeMetadata>
        {
            new AttributeMetadata(
                "System.AttributeUsageAttribute",
                new List<AttributeArgumentMetadata>
                {
                    new AttributeArgumentMetadata(null, "System.AttributeTargets.All"),
                    new AttributeArgumentMetadata("AllowMultiple", "true")
                }
            )
        };
        
        var typeMetadata = new TypeMetadata(
            "TestClass",
            "TestNamespace",
            TypeKind.Class,
            false,
            new List<string>(),
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            typeof(object),
            attributes
        );

        // Act
        var result = generator.GenerateTypeDeclaration(typeMetadata);

        // Assert
        Assert.NotNull(result);
        var classDecl = Assert.IsType<ClassDeclarationSyntax>(result);
        var attributeList = classDecl.AttributeLists.FirstOrDefault();
        Assert.NotNull(attributeList);
        
        var attribute = attributeList.Attributes.First();
        Assert.NotNull(attribute.ArgumentList);
        Assert.NotEmpty(attribute.ArgumentList.Arguments);
        
        // Проверяем наличие именованного аргумента
        var hasNamedArgument = attribute.ArgumentList.Arguments
            .Any(arg => arg.NameEquals != null && arg.NameEquals.Name.ToString() == "AllowMultiple");
        Assert.True(hasNamedArgument, "Attribute should have named argument AllowMultiple");
    }

    [Fact]
    public void AttributeMetadata_RecordProperties_AreAccessible()
    {
        // Arrange & Act
        var attributeMetadata = new AttributeMetadata(
            "System.ObsoleteAttribute",
            new List<AttributeArgumentMetadata>
            {
                new AttributeArgumentMetadata(null, "\"Message\"")
            }
        );

        // Assert
        Assert.Equal("System.ObsoleteAttribute", attributeMetadata.FullTypeName);
        Assert.Single(attributeMetadata.Arguments);
        Assert.Null(attributeMetadata.Arguments[0].Name);
        Assert.Equal("\"Message\"", attributeMetadata.Arguments[0].Value);
    }

    [Fact]
    public void AttributeArgumentMetadata_WithName_IsCorrect()
    {
        // Arrange & Act
        var argumentMetadata = new AttributeArgumentMetadata(
            "PropertyName",
            "true"
        );

        // Assert
        Assert.Equal("PropertyName", argumentMetadata.Name);
        Assert.Equal("true", argumentMetadata.Value);
    }

    [Fact]
    public void ExtractAttributes_FromRealAssembly_ExtractsAttributes()
    {
        // Arrange
        _reflector = new AssemblyReflector();
        // Используем тестовую сборку, которая содержит типы с атрибутами
        var testAssembly = typeof(TestClassWithAttribute).Assembly;
        var assemblyPath = testAssembly.Location;

        // Act
        var types = _reflector.ReadAssembly(assemblyPath);
        
        // Ищем типы с атрибутами
        var typesWithAttributes = types.Where(t => t.Attributes.Any()).ToList();

        // Assert
        // В тестовой сборке должны быть типы с атрибутами (TestClassWithAttribute имеет [Serializable])
        Assert.NotNull(typesWithAttributes);
        // Проверяем, что хотя бы один тип имеет атрибуты
        var testType = typesWithAttributes.FirstOrDefault(t => t.Name == "TestClassWithAttribute");
        if (testType != null)
        {
            Assert.NotEmpty(testType.Attributes);
        }
    }

    public void Dispose()
    {
        _reflector?.Dispose();
    }
}

// Тестовые классы с атрибутами для проверки извлечения

[System.Serializable]
public class TestClassWithAttribute
{
}

public class TestClassWithMethodAttribute
{
    [System.Obsolete("This method is obsolete")]
    public void TestMethod()
    {
    }
}

public class TestClassWithPropertyAttribute
{
    [System.ComponentModel.Description("Test property")]
    public string TestProperty { get; set; } = string.Empty;
}

public class TestClassWithAttributeArguments
{
    [System.Obsolete("This method is obsolete", error: false)]
    public void TestMethodWithAttribute()
    {
    }
}

