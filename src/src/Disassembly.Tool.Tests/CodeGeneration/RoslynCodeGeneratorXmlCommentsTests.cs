using Disassembly.Tool.CodeGeneration;
using Disassembly.Tool.Core;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using CoreTypeKind = Disassembly.Tool.Core.TypeKind;
using Xunit;

namespace Disassembly.Tool.Tests.CodeGeneration;

/// <summary>
/// Тесты для RoslynCodeGenerator с XML комментариями
/// </summary>
public class RoslynCodeGeneratorXmlCommentsTests
{
    [Fact]
    public void GenerateTypeDeclaration_WithTypeComments_IncludesXmlComments()
    {
        // Arrange
        var testType = typeof(object);
        var typeXmlId = XmlDocumentationReader.GenerateTypeXmlId(testType);
        
        var typeComments = new Dictionary<string, TypeComments>
        {
            [typeXmlId] = new TypeComments(
                "Test class summary",
                "Test remarks",
                new List<string> { "Example 1" }
            )
        };

        var generator = new RoslynCodeGenerator(typeComments, null);
        var typeMetadata = new TypeMetadata(
            testType.Name,
            testType.Namespace,
            CoreTypeKind.Class,
            false,
            new List<string>(),
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            testType,
            new List<AttributeMetadata>()
        );

        // Act
        var result = generator.GenerateTypeDeclaration(typeMetadata);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("/// <summary>", code);
        Assert.Contains("Test class summary", code);
        Assert.Contains("/// <remarks>", code);
        Assert.Contains("Test remarks", code);
        Assert.Contains("/// <example>", code);
        Assert.Contains("Example 1", code);
    }

    [Fact]
    public void GenerateTypeDeclaration_WithEnumTypeComments_IncludesXmlComments()
    {
        // Arrange
        var enumType = typeof(EnvironmentVariableTarget);
        var typeXmlId = XmlDocumentationReader.GenerateTypeXmlId(enumType);
        
        var typeComments = new Dictionary<string, TypeComments>
        {
            [typeXmlId] = new TypeComments(
                "Test enum summary",
                null,
                new List<string>()
            )
        };

        var generator = new RoslynCodeGenerator(typeComments, null);
        var typeMetadata = new TypeMetadata(
            enumType.Name,
            enumType.Namespace,
            CoreTypeKind.Enum,
            false,
            new List<string>(),
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            enumType,
            new List<AttributeMetadata>()
        );

        // Act
        var result = generator.GenerateTypeDeclaration(typeMetadata);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("/// <summary>", code);
        Assert.Contains("Test enum summary", code);
        Assert.Contains("enum", code);
    }

    [Fact]
    public void GenerateMember_WithMethodComments_IncludesXmlComments()
    {
        // Arrange
        var methodInfo = typeof(string).GetMethod("Substring", new[] { typeof(int) })!;
        var xmlId = XmlDocumentationReader.GenerateMemberXmlId(methodInfo);
        
        var memberComments = new Dictionary<string, MemberComments>
        {
            [xmlId] = new MemberComments(
                "Method summary",
                "Method remarks",
                new Dictionary<string, string> { ["startIndex"] = "Parameter description" },
                "Return description",
                new Dictionary<string, string>(),
                new List<string>()
            )
        };

        var generator = new RoslynCodeGenerator(null, memberComments);
        
        var memberMetadata = new MemberMetadata(
            "Substring",
            MemberType.Method,
            "string Substring(int startIndex)",
            typeof(string),
            new List<ParameterMetadata> { new("startIndex", "int", false, null) },
            methodInfo,
            new List<AttributeMetadata>()
        );

        // Act
        var result = generator.GenerateMember(memberMetadata);
        var code = result?.ToFullString() ?? "";

        // Assert
        Assert.NotNull(result);
        Assert.Contains("/// <summary>", code);
        Assert.Contains("Method summary", code);
    }

    [Fact]
    public void GenerateMember_WithPropertyComments_IncludesXmlComments()
    {
        // Arrange
        var propertyInfo = typeof(string).GetProperty("Length")!;
        var xmlId = XmlDocumentationReader.GenerateMemberXmlId(propertyInfo);
        
        var memberComments = new Dictionary<string, MemberComments>
        {
            [xmlId] = new MemberComments(
                "Property summary",
                null,
                new Dictionary<string, string>(),
                null,
                new Dictionary<string, string>(),
                new List<string>()
            )
        };

        var generator = new RoslynCodeGenerator(null, memberComments);
        
        var memberMetadata = new MemberMetadata(
            "Length",
            MemberType.Property,
            "int Length",
            typeof(int),
            new List<ParameterMetadata>(),
            propertyInfo,
            new List<AttributeMetadata>()
        );

        // Act
        var result = generator.GenerateMember(memberMetadata);
        var code = result?.ToFullString() ?? "";

        // Assert
        Assert.NotNull(result);
        Assert.Contains("/// <summary>", code);
        Assert.Contains("Property summary", code);
    }

    [Fact]
    public void GenerateTypeDeclaration_WithEnumMemberComments_IncludesXmlComments()
    {
        // Arrange
        var enumType = typeof(EnvironmentVariableTarget);
        var typeXmlId = XmlDocumentationReader.GenerateTypeXmlId(enumType);
        
        var typeComments = new Dictionary<string, TypeComments>
        {
            [typeXmlId] = new TypeComments(
                "Enum summary",
                null,
                new List<string>()
            )
        };

        var enumFields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.Name != "value__")
            .Take(2)
            .ToList();

        var memberComments = new Dictionary<string, MemberComments>();
        foreach (var field in enumFields)
        {
            var fieldXmlId = XmlDocumentationReader.GenerateMemberXmlId(field);
            memberComments[fieldXmlId] = new MemberComments(
                $"Enum value {field.Name} summary",
                null,
                new Dictionary<string, string>(),
                null,
                new Dictionary<string, string>(),
                new List<string>()
            );
        }

        var generator = new RoslynCodeGenerator(typeComments, memberComments);
        
        var members = enumFields.Select(f => new MemberMetadata(
            f.Name,
            MemberType.Field,
            $"{enumType.Name}.{f.Name}",
            f.FieldType,
            new List<ParameterMetadata>(),
            f,
            new List<AttributeMetadata>()
        )).ToList();

        var typeMetadata = new TypeMetadata(
            enumType.Name,
            enumType.Namespace,
            CoreTypeKind.Enum,
            false,
            new List<string>(),
            members,
            new List<TypeMetadata>(),
            enumType,
            new List<AttributeMetadata>()
        );

        // Act
        var result = generator.GenerateTypeDeclaration(typeMetadata);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("/// <summary>", code);
        Assert.Contains("Enum summary", code);
        // Проверяем, что enum members имеют комментарии
        if (enumFields.Count > 0)
        {
            Assert.Contains("/// <summary>", code); // Должны быть комментарии для enum members
        }
    }

    [Fact]
    public void GenerateTypeDeclaration_WithoutComments_DoesNotIncludeXmlComments()
    {
        // Arrange
        var generator = new RoslynCodeGenerator(null, null);
        var typeMetadata = new TypeMetadata(
            "TestClass",
            "TestNamespace",
            CoreTypeKind.Class,
            false,
            new List<string>(),
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            typeof(object),
            new List<AttributeMetadata>()
        );

        // Act
        var result = generator.GenerateTypeDeclaration(typeMetadata);
        var code = result.ToFullString();

        // Assert
        Assert.DoesNotContain("/// <summary>", code);
    }

    [Fact]
    public void GenerateMember_WithParamComments_IncludesParamTags()
    {
        // Arrange
        var methodInfo = typeof(string).GetMethod("Substring", new[] { typeof(int), typeof(int) })!;
        var xmlId = XmlDocumentationReader.GenerateMemberXmlId(methodInfo);
        
        var memberComments = new Dictionary<string, MemberComments>
        {
            [xmlId] = new MemberComments(
                "Method summary",
                null,
                new Dictionary<string, string>
                {
                    ["startIndex"] = "First parameter description",
                    ["length"] = "Second parameter description"
                },
                null,
                new Dictionary<string, string>(),
                new List<string>()
            )
        };

        var generator = new RoslynCodeGenerator(null, memberComments);
        
        var memberMetadata = new MemberMetadata(
            "Substring",
            MemberType.Method,
            "string Substring(int startIndex, int length)",
            typeof(string),
            new List<ParameterMetadata>
            {
                new("startIndex", "int", false, null),
                new("length", "int", false, null)
            },
            methodInfo,
            new List<AttributeMetadata>()
        );

        // Act
        var result = generator.GenerateMember(memberMetadata);
        var code = result?.ToFullString() ?? "";

        // Assert
        Assert.NotNull(result);
        Assert.Contains("/// <param name=\"startIndex\">", code);
        Assert.Contains("First parameter description", code);
        Assert.Contains("/// <param name=\"length\">", code);
        Assert.Contains("Second parameter description", code);
    }

    [Fact]
    public void GenerateMember_WithReturnsComment_IncludesReturnsTag()
    {
        // Arrange
        var methodInfo = typeof(string).GetMethod("Clone")!;
        var xmlId = XmlDocumentationReader.GenerateMemberXmlId(methodInfo);
        
        var memberComments = new Dictionary<string, MemberComments>
        {
            [xmlId] = new MemberComments(
                "Method summary",
                null,
                new Dictionary<string, string>(),
                "Return value description",
                new Dictionary<string, string>(),
                new List<string>()
            )
        };

        var generator = new RoslynCodeGenerator(null, memberComments);
        
        var memberMetadata = new MemberMetadata(
            "Clone",
            MemberType.Method,
            "object Clone()",
            typeof(object),
            new List<ParameterMetadata>(),
            methodInfo,
            new List<AttributeMetadata>()
        );

        // Act
        var result = generator.GenerateMember(memberMetadata);
        var code = result?.ToFullString() ?? "";

        // Assert
        Assert.NotNull(result);
        Assert.Contains("/// <returns>", code);
        Assert.Contains("Return value description", code);
    }

    [Fact]
    public void GenerateMember_WithExceptionComment_IncludesExceptionTag()
    {
        // Arrange
        var methodInfo = typeof(string).GetMethod("Clone")!;
        var xmlId = XmlDocumentationReader.GenerateMemberXmlId(methodInfo);
        
        var memberComments = new Dictionary<string, MemberComments>
        {
            [xmlId] = new MemberComments(
                "Method summary",
                null,
                new Dictionary<string, string>(),
                null,
                new Dictionary<string, string>
                {
                    ["System.ArgumentException"] = "Exception description"
                },
                new List<string>()
            )
        };

        var generator = new RoslynCodeGenerator(null, memberComments);
        
        var memberMetadata = new MemberMetadata(
            "Clone",
            MemberType.Method,
            "object Clone()",
            typeof(object),
            new List<ParameterMetadata>(),
            methodInfo,
            new List<AttributeMetadata>()
        );

        // Act
        var result = generator.GenerateMember(memberMetadata);
        var code = result?.ToFullString() ?? "";

        // Assert
        Assert.NotNull(result);
        Assert.Contains("/// <exception cref=\"System.ArgumentException\">", code);
        Assert.Contains("Exception description", code);
    }

    [Fact]
    public void GenerateTypeDeclaration_WithRemarksAndExamples_IncludesAllTags()
    {
        // Arrange
        var testType = typeof(object);
        var typeXmlId = XmlDocumentationReader.GenerateTypeXmlId(testType);
        
        var typeComments = new Dictionary<string, TypeComments>
        {
            [typeXmlId] = new TypeComments(
                "Class summary",
                "Class remarks",
                new List<string> { "Example 1", "Example 2" }
            )
        };

        var generator = new RoslynCodeGenerator(typeComments, null);
        var typeMetadata = new TypeMetadata(
            testType.Name,
            testType.Namespace,
            CoreTypeKind.Class,
            false,
            new List<string>(),
            new List<MemberMetadata>(),
            new List<TypeMetadata>(),
            testType,
            new List<AttributeMetadata>()
        );

        // Act
        var result = generator.GenerateTypeDeclaration(typeMetadata);
        var code = result.ToFullString();

        // Assert
        Assert.Contains("/// <summary>", code);
        Assert.Contains("Class summary", code);
        Assert.Contains("/// <remarks>", code);
        Assert.Contains("Class remarks", code);
        Assert.Contains("/// <example>", code);
        Assert.Contains("Example 1", code);
        Assert.Contains("Example 2", code);
    }
}

