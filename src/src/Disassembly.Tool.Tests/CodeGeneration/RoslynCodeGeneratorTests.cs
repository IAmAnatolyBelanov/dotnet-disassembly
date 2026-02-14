using Disassembly.Tool.CodeGeneration;
using Disassembly.Tool.Core;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using CoreTypeKind = Disassembly.Tool.Core.TypeKind;
using Xunit;

namespace Disassembly.Tool.Tests.CodeGeneration;

/// <summary>
/// Тесты для класса RoslynCodeGenerator
/// </summary>
public class RoslynCodeGeneratorTests
{
    private readonly RoslynCodeGenerator _generator;

    public RoslynCodeGeneratorTests()
    {
        _generator = new RoslynCodeGenerator();
    }

    [Fact]
    public void GenerateTypeDeclaration_WithClass_ReturnsClassDeclaration()
    {
        // Arrange
        var typeMetadata = new TypeMetadata(
            "TestClass",
            "TestNamespace",
            CoreTypeKind.Class,
            false,
            new List<string>(),
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            typeof(object)
        );

        // Act
        var result = _generator.GenerateTypeDeclaration(typeMetadata);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>(result);
    }

    [Fact]
    public void GenerateTypeDeclaration_WithInterface_ReturnsInterfaceDeclaration()
    {
        // Arrange
        var typeMetadata = new TypeMetadata(
            "ITestInterface",
            "TestNamespace",
            CoreTypeKind.Interface,
            false,
            new List<string>(),
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            null
        );

        // Act
        var result = _generator.GenerateTypeDeclaration(typeMetadata);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Microsoft.CodeAnalysis.CSharp.Syntax.InterfaceDeclarationSyntax>(result);
    }

    [Fact]
    public void GenerateTypeDeclaration_WithStruct_ReturnsStructDeclaration()
    {
        // Arrange
        var typeMetadata = new TypeMetadata(
            "TestStruct",
            "TestNamespace",
            CoreTypeKind.Struct,
            false,
            new List<string>(),
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            typeof(ValueType)
        );

        // Act
        var result = _generator.GenerateTypeDeclaration(typeMetadata);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Microsoft.CodeAnalysis.CSharp.Syntax.StructDeclarationSyntax>(result);
    }

    [Fact]
    public void GenerateTypeDeclaration_WithEnum_ReturnsEnumDeclaration()
    {
        // Arrange
        var typeMetadata = new TypeMetadata(
            "TestEnum",
            "TestNamespace",
            CoreTypeKind.Enum,
            false,
            new List<string>(),
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            typeof(Enum)
        );

        // Act
        var result = _generator.GenerateTypeDeclaration(typeMetadata);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Microsoft.CodeAnalysis.CSharp.Syntax.EnumDeclarationSyntax>(result);
    }

    [Fact]
    public void GenerateTypeDeclaration_WithGenericType_IncludesTypeParameters()
    {
        // Arrange
        var typeMetadata = new TypeMetadata(
            "GenericClass",
            "TestNamespace",
            CoreTypeKind.Class,
            true,
            new List<string> { "T", "U" },
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            typeof(object)
        );

        // Act
        var result = _generator.GenerateTypeDeclaration(typeMetadata);

        // Assert
        Assert.NotNull(result);
        var classDecl = Assert.IsType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>(result);
        Assert.NotNull(classDecl.TypeParameterList);
        Assert.Equal(2, classDecl.TypeParameterList.Parameters.Count);
    }

    [Fact]
    public void GenerateMember_WithMethod_ReturnsMethodDeclaration()
    {
        // Arrange
        var methodInfo = typeof(string).GetMethod("ToString", Type.EmptyTypes)!;
        var memberMetadata = new MemberMetadata(
            "TestMethod",
            MemberType.Method,
            "void TestMethod()",
            typeof(void),
            new List<ParameterMetadata>(),
            methodInfo
        );

        // Act
        var result = _generator.GenerateMember(memberMetadata);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>(result);
    }

    [Fact]
    public void GenerateMember_WithProperty_ReturnsPropertyDeclaration()
    {
        // Arrange
        var propertyInfo = typeof(string).GetProperty("Length")!;
        var memberMetadata = new MemberMetadata(
            "TestProperty",
            MemberType.Property,
            "int TestProperty",
            typeof(int),
            new List<ParameterMetadata>(),
            propertyInfo
        );

        // Act
        var result = _generator.GenerateMember(memberMetadata);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax>(result);
    }

    [Fact]
    public void GenerateMember_WithField_ReturnsFieldDeclaration()
    {
        // Arrange
        var fieldInfo = typeof(Environment).GetField("CurrentDirectory", BindingFlags.Public | BindingFlags.Static);
        if (fieldInfo == null)
        {
            // Если поле не найдено, создаем тестовое поле через рефлексию
            var testType = typeof(TestClassWithField);
            fieldInfo = testType.GetField("TestField", BindingFlags.Public | BindingFlags.Instance)!;
        }

        var memberMetadata = new MemberMetadata(
            "TestField",
            MemberType.Field,
            "int TestField",
            typeof(int),
            new List<ParameterMetadata>(),
            fieldInfo
        );

        // Act
        var result = _generator.GenerateMember(memberMetadata);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax>(result);
    }

    [Fact]
    public void GenerateMember_WithEvent_ReturnsEventDeclaration()
    {
        // Arrange
        var eventInfo = typeof(AppDomain).GetEvent("DomainUnload");
        if (eventInfo == null)
        {
            // Если событие не найдено, создаем тестовое событие
            var testType = typeof(TestClassWithEvent);
            eventInfo = testType.GetEvent("TestEvent", BindingFlags.Public | BindingFlags.Instance)!;
        }

        if (eventInfo != null)
        {
            var memberMetadata = new MemberMetadata(
                "TestEvent",
                MemberType.Event,
                "event EventHandler TestEvent",
                typeof(EventHandler),
                new List<ParameterMetadata>(),
                eventInfo
            );

            // Act
            var result = _generator.GenerateMember(memberMetadata);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<Microsoft.CodeAnalysis.CSharp.Syntax.EventFieldDeclarationSyntax>(result);
        }
    }

    [Fact]
    public void GenerateMember_WithConstructor_ReturnsConstructorDeclaration()
    {
        // Arrange
        var constructorInfo = typeof(string).GetConstructor(new[] { typeof(char[]) })!;
        var memberMetadata = new MemberMetadata(
            ".ctor",
            MemberType.Constructor,
            "String(char[])",
            null,
            new List<ParameterMetadata>
            {
                new ParameterMetadata("chars", "char[]", false, null)
            },
            constructorInfo
        );

        // Act
        var result = _generator.GenerateMember(memberMetadata);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Microsoft.CodeAnalysis.CSharp.Syntax.ConstructorDeclarationSyntax>(result);
    }

    [Fact]
    public void GenerateFile_WithTypeMetadata_ReturnsCompilationUnit()
    {
        // Arrange
        var typeMetadata = new TypeMetadata(
            "TestClass",
            "TestNamespace",
            CoreTypeKind.Class,
            false,
            new List<string>(),
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            typeof(object)
        );

        // Act
        var result = _generator.GenerateFile(typeMetadata);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax>(result);
        Assert.Single(result.Members);
    }

    [Fact]
    public void GenerateFile_WithNullNamespace_UsesGlobalNamespace()
    {
        // Arrange
        var typeMetadata = new TypeMetadata(
            "TestClass",
            null,
            CoreTypeKind.Class,
            false,
            new List<string>(),
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            typeof(object)
        );

        // Act
        var result = _generator.GenerateFile(typeMetadata);

        // Assert
        Assert.NotNull(result);
        var namespaceDecl = result.Members.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.NamespaceDeclarationSyntax>().First();
        Assert.Equal("Global", namespaceDecl.Name.ToString());
    }

    // Вспомогательные классы для тестирования
    private class TestClassWithField
    {
#pragma warning disable CS0649 // Field is never assigned to
        public int TestField;
#pragma warning restore CS0649
    }

    private class TestClassWithEvent
    {
#pragma warning disable CS0067 // Event is never used
        public event EventHandler? TestEvent;
#pragma warning restore CS0067
    }

    [Fact]
    public void GenerateMember_WithNullableReferenceTypes_MarksTypesWithQuestionMark()
    {
        // Arrange - создаем метаданные метода с nullable reference type параметром
        // В реальной реализации TypeName в ParameterMetadata будет содержать "string?" 
        // для nullable reference types (определяется через NullableAttribute в рефлексии)
        var methodInfo = typeof(string).GetMethod("ToString", Type.EmptyTypes)!;
        var methodMetadata = new MemberMetadata(
            "MethodWithNullableParameter",
            MemberType.Method,
            "void MethodWithNullableParameter(string? param)",
            typeof(void),
            new List<ParameterMetadata>
            {
                // TypeName содержит "string?" для nullable reference type
                new ParameterMetadata("param", "string?", false, null)
            },
            methodInfo
        );

        // Act
        var methodResult = _generator.GenerateMember(methodMetadata);

        // Assert - проверяем, что nullable reference types помечены явно через `?`
        Assert.NotNull(methodResult);
        var methodSyntax = Assert.IsType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>(methodResult);
        var methodCode = methodSyntax.ToFullString();
        
        // Проверяем, что параметр помечен как nullable
        // RoslynCodeGenerator использует SyntaxFactory.ParseTypeName(p.TypeName),
        // который должен корректно обработать "string?" и сгенерировать код с `?`
        Assert.Contains("string?", methodCode);
        
        // Также проверяем, что это именно параметр метода, а не что-то другое
        var parameters = methodSyntax.ParameterList.Parameters;
        Assert.Single(parameters);
        var paramType = parameters[0].Type?.ToString();
        Assert.Equal("string?", paramType);
    }
}

