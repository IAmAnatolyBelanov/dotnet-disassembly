using Disassembly.Tool.Core;
using System.Reflection;
using Xunit;

namespace Disassembly.Tool.Tests.Core;

/// <summary>
/// Тесты для класса XmlDocumentationReader
/// </summary>
public class XmlDocumentationReaderTests
{
    [Fact]
    public void LoadXmlDocumentation_WhenFileDoesNotExist_DoesNotThrow()
    {
        // Arrange
        var reader = new XmlDocumentationReader();
        var nonExistentPath = "/nonexistent/path/to/file.xml";

        // Act & Assert
        reader.LoadXmlDocumentation(nonExistentPath); // Не должно выбрасывать исключение
    }

    [Fact]
    public void LoadXmlDocumentation_WithValidXmlFile_LoadsDocumentation()
    {
        // Arrange
        var reader = new XmlDocumentationReader();
        var tempFile = Path.GetTempFileName();
        var xmlContent = @"<?xml version=""1.0""?>
<doc>
    <assembly>
        <name>TestAssembly</name>
    </assembly>
    <members>
        <member name=""T:TestNamespace.TestClass"">
            <summary>Test class summary</summary>
            <remarks>Test remarks</remarks>
        </member>
        <member name=""M:TestNamespace.TestClass.TestMethod(System.String)"">
            <summary>Test method summary</summary>
            <param name=""param1"">Parameter description</param>
            <returns>Return value description</returns>
        </member>
    </members>
</doc>";

        try
        {
            File.WriteAllText(tempFile, xmlContent);

            // Act
            reader.LoadXmlDocumentation(tempFile);

            // Assert
            var typeComments = reader.GetTypeComments("T:TestNamespace.TestClass");
            Assert.NotNull(typeComments);
            Assert.Equal("Test class summary", typeComments.Summary);
            Assert.Equal("Test remarks", typeComments.Remarks);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetTypeComments_WithValidXmlId_ReturnsComments()
    {
        // Arrange
        var reader = new XmlDocumentationReader();
        var tempFile = Path.GetTempFileName();
        var xmlContent = @"<?xml version=""1.0""?>
<doc>
    <members>
        <member name=""T:TestNamespace.TestClass"">
            <summary>Test summary</summary>
            <remarks>Test remarks</remarks>
            <example>Example 1</example>
            <example>Example 2</example>
        </member>
    </members>
</doc>";

        try
        {
            File.WriteAllText(tempFile, xmlContent);
            reader.LoadXmlDocumentation(tempFile);

            // Act
            var comments = reader.GetTypeComments("T:TestNamespace.TestClass");

            // Assert
            Assert.NotNull(comments);
            Assert.Equal("Test summary", comments.Summary);
            Assert.Equal("Test remarks", comments.Remarks);
            Assert.Equal(2, comments.Examples.Count);
            Assert.Contains("Example 1", comments.Examples);
            Assert.Contains("Example 2", comments.Examples);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetTypeComments_WithInvalidXmlId_ReturnsNull()
    {
        // Arrange
        var reader = new XmlDocumentationReader();
        var tempFile = Path.GetTempFileName();
        var xmlContent = @"<?xml version=""1.0""?>
<doc>
    <members>
        <member name=""T:TestNamespace.TestClass"">
            <summary>Test summary</summary>
        </member>
    </members>
</doc>";

        try
        {
            File.WriteAllText(tempFile, xmlContent);
            reader.LoadXmlDocumentation(tempFile);

            // Act
            var comments = reader.GetTypeComments("T:TestNamespace.NonExistentClass");

            // Assert
            Assert.Null(comments);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetMemberComments_WithMethod_ReturnsComments()
    {
        // Arrange
        var reader = new XmlDocumentationReader();
        var tempFile = Path.GetTempFileName();
        var xmlContent = @"<?xml version=""1.0""?>
<doc>
    <members>
        <member name=""M:TestNamespace.TestClass.TestMethod(System.String,System.Int32)"">
            <summary>Method summary</summary>
            <param name=""param1"">First parameter</param>
            <param name=""param2"">Second parameter</param>
            <returns>Return description</returns>
            <remarks>Method remarks</remarks>
            <exception cref=""T:System.ArgumentException"">Exception description</exception>
        </member>
    </members>
</doc>";

        try
        {
            File.WriteAllText(tempFile, xmlContent);
            reader.LoadXmlDocumentation(tempFile);

            // Act
            var comments = reader.GetMemberComments("M:TestNamespace.TestClass.TestMethod(System.String,System.Int32)");

            // Assert
            Assert.NotNull(comments);
            Assert.Equal("Method summary", comments.Summary);
            Assert.Equal("Return description", comments.Returns);
            Assert.Equal("Method remarks", comments.Remarks);
            Assert.Equal(2, comments.Parameters.Count);
            Assert.Equal("First parameter", comments.Parameters["param1"]);
            Assert.Equal("Second parameter", comments.Parameters["param2"]);
            Assert.Single(comments.Exceptions);
            Assert.True(comments.Exceptions.ContainsKey("System.ArgumentException"));
            Assert.Equal("Exception description", comments.Exceptions["System.ArgumentException"]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetMemberComments_WithProperty_ReturnsComments()
    {
        // Arrange
        var reader = new XmlDocumentationReader();
        var tempFile = Path.GetTempFileName();
        var xmlContent = @"<?xml version=""1.0""?>
<doc>
    <members>
        <member name=""P:TestNamespace.TestClass.TestProperty"">
            <summary>Property summary</summary>
            <value>Property value description</value>
        </member>
    </members>
</doc>";

        try
        {
            File.WriteAllText(tempFile, xmlContent);
            reader.LoadXmlDocumentation(tempFile);

            // Act
            var comments = reader.GetMemberComments("P:TestNamespace.TestClass.TestProperty");

            // Assert
            Assert.NotNull(comments);
            Assert.Equal("Property summary", comments.Summary);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void GenerateTypeXmlId_WithSimpleType_ReturnsCorrectId()
    {
        // Arrange
        var type = typeof(string);

        // Act
        var xmlId = XmlDocumentationReader.GenerateTypeXmlId(type);

        // Assert
        Assert.Equal("T:System.String", xmlId);
    }

    [Fact]
    public void GenerateTypeXmlId_WithGenericType_ReturnsCorrectId()
    {
        // Arrange
        var type = typeof(List<>);

        // Act
        var xmlId = XmlDocumentationReader.GenerateTypeXmlId(type);

        // Assert
        Assert.StartsWith("T:System.Collections.Generic.List`", xmlId);
    }

    [Fact]
    public void GenerateMemberXmlId_WithMethod_ReturnsCorrectId()
    {
        // Arrange
        var method = typeof(string).GetMethod("Substring", new[] { typeof(int) })!;

        // Act
        var xmlId = XmlDocumentationReader.GenerateMemberXmlId(method);

        // Assert
        Assert.StartsWith("M:System.String.Substring", xmlId);
        Assert.Contains("System.Int32", xmlId);
    }

    [Fact]
    public void GenerateMemberXmlId_WithProperty_ReturnsCorrectId()
    {
        // Arrange
        var property = typeof(string).GetProperty("Length")!;

        // Act
        var xmlId = XmlDocumentationReader.GenerateMemberXmlId(property);

        // Assert
        Assert.Equal("P:System.String.Length", xmlId);
    }

    [Fact]
    public void GenerateMemberXmlId_WithField_ReturnsCorrectId()
    {
        // Arrange
        var field = typeof(DateTime).GetField("MinValue")!;

        // Act
        var xmlId = XmlDocumentationReader.GenerateMemberXmlId(field);

        // Assert
        Assert.Equal("F:System.DateTime.MinValue", xmlId);
    }

    [Fact]
    public void GenerateMemberXmlId_WithConstructor_ReturnsCorrectId()
    {
        // Arrange
        var constructor = typeof(string).GetConstructor(new[] { typeof(char[]) })!;

        // Act
        var xmlId = XmlDocumentationReader.GenerateMemberXmlId(constructor);

        // Assert
        Assert.StartsWith("M:System.String.#ctor", xmlId);
        Assert.Contains("System.Char[]", xmlId);
    }

    [Fact]
    public void GetTypeComments_WithTypeObject_ReturnsComments()
    {
        // Arrange
        var reader = new XmlDocumentationReader();
        var tempFile = Path.GetTempFileName();
        var xmlContent = @"<?xml version=""1.0""?>
<doc>
    <members>
        <member name=""T:System.String"">
            <summary>String type summary</summary>
        </member>
    </members>
</doc>";

        try
        {
            File.WriteAllText(tempFile, xmlContent);
            reader.LoadXmlDocumentation(tempFile);

            // Act
            var comments = reader.GetTypeComments(typeof(string));

            // Assert
            Assert.NotNull(comments);
            Assert.Equal("String type summary", comments.Summary);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetMemberComments_WithMemberInfo_ReturnsComments()
    {
        // Arrange
        var reader = new XmlDocumentationReader();
        var tempFile = Path.GetTempFileName();
        var xmlContent = @"<?xml version=""1.0""?>
<doc>
    <members>
        <member name=""P:System.String.Length"">
            <summary>Length property summary</summary>
        </member>
    </members>
</doc>";

        try
        {
            File.WriteAllText(tempFile, xmlContent);
            reader.LoadXmlDocumentation(tempFile);

            var property = typeof(string).GetProperty("Length")!;

            // Act
            var comments = reader.GetMemberComments(property);

            // Assert
            Assert.NotNull(comments);
            Assert.Equal("Length property summary", comments.Summary);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetTypeComments_WithEmptySummary_ReturnsNull()
    {
        // Arrange
        var reader = new XmlDocumentationReader();
        var tempFile = Path.GetTempFileName();
        var xmlContent = @"<?xml version=""1.0""?>
<doc>
    <members>
        <member name=""T:TestNamespace.TestClass"">
            <summary></summary>
        </member>
    </members>
</doc>";

        try
        {
            File.WriteAllText(tempFile, xmlContent);
            reader.LoadXmlDocumentation(tempFile);

            // Act
            var comments = reader.GetTypeComments("T:TestNamespace.TestClass");

            // Assert
            Assert.Null(comments); // Пустые комментарии должны возвращать null
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}

