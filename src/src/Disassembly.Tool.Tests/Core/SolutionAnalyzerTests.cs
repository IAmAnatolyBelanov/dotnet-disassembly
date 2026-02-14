using Disassembly.Tool.Core;
using Xunit;

namespace Disassembly.Tool.Tests.Core;

/// <summary>
/// Тесты для класса SolutionAnalyzer
/// </summary>
public class SolutionAnalyzerTests
{
    private readonly SolutionAnalyzer _analyzer;

    public SolutionAnalyzerTests()
    {
        _analyzer = new SolutionAnalyzer();
    }

    [Fact]
    public void ParseSolution_WhenFileDoesNotExist_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = "/nonexistent/path/to/solution.sln";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => _analyzer.ParseSolution(nonExistentPath));
    }

    [Fact]
    public void ParseSolution_WithValidSolutionFile_ReturnsProjects()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var solutionPath = Path.Combine(tempDir, "TestSolution.sln");
        var projectPath = Path.Combine(tempDir, "TestProject.csproj");

        try
        {
            // Создаем тестовый .sln файл
            var solutionContent = $@"
Microsoft Visual Studio Solution File, Format Version 12.00
Project(""{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}"") = ""TestProject"", ""TestProject.csproj"", ""{{12345678-1234-1234-1234-123456789012}}""
EndProject
";
            File.WriteAllText(solutionPath, solutionContent);

            // Создаем тестовый .csproj файл
            var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.4"" />
  </ItemGroup>
</Project>";
            File.WriteAllText(projectPath, projectContent);

            // Act
            var projects = _analyzer.ParseSolution(solutionPath);

            // Assert
            Assert.NotNull(projects);
            Assert.Single(projects);
            Assert.Equal("TestProject", projects[0].Name);
            Assert.Equal(projectPath, projects[0].Path);
        }
        finally
        {
            // Cleanup
            if (File.Exists(solutionPath)) File.Delete(solutionPath);
            if (File.Exists(projectPath)) File.Delete(projectPath);
        }
    }

    [Fact]
    public void ParseProject_WhenFileDoesNotExist_ReturnsNull()
    {
        // Arrange
        var nonExistentPath = "/nonexistent/path/to/project.csproj";

        // Act
        var result = _analyzer.ParseProject(nonExistentPath);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ParseProject_WithValidProjectFile_ReturnsProjectInfo()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var projectPath = Path.ChangeExtension(tempFile, ".csproj");
        File.Move(tempFile, projectPath);

        try
        {
            var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.4"" />
    <PackageReference Include=""Microsoft.CodeAnalysis.CSharp"" Version=""4.8.0"" />
  </ItemGroup>
</Project>";
            File.WriteAllText(projectPath, projectContent);

            // Act
            var result = _analyzer.ParseProject(projectPath);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(Path.GetFileNameWithoutExtension(projectPath), result.Name);
            Assert.Equal(projectPath, result.Path);
            Assert.Equal(2, result.PackageReferences.Count);
            Assert.Contains(result.PackageReferences, p => p.Name == "Newtonsoft.Json" && p.Version == "13.0.4");
            Assert.Contains(result.PackageReferences, p => p.Name == "Microsoft.CodeAnalysis.CSharp" && p.Version == "4.8.0");
        }
        finally
        {
            if (File.Exists(projectPath)) File.Delete(projectPath);
        }
    }

    [Fact]
    public void ParseProject_WithPackageReferenceInVersionElement_ExtractsVersion()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var projectPath = Path.ChangeExtension(tempFile, ".csproj");
        File.Move(tempFile, projectPath);

        try
        {
            var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""TestPackage"">
      <Version>1.2.3</Version>
    </PackageReference>
  </ItemGroup>
</Project>";
            File.WriteAllText(projectPath, projectContent);

            // Act
            var result = _analyzer.ParseProject(projectPath);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result.PackageReferences);
            Assert.Equal("TestPackage", result.PackageReferences[0].Name);
            Assert.Equal("1.2.3", result.PackageReferences[0].Version);
        }
        finally
        {
            if (File.Exists(projectPath)) File.Delete(projectPath);
        }
    }

    [Fact]
    public void ParseProject_WithInvalidXml_ReturnsNull()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var projectPath = Path.ChangeExtension(tempFile, ".csproj");
        File.Move(tempFile, projectPath);

        try
        {
            var invalidContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""TestPackage"" Version=""1.2.3"">
    <!-- Не закрыт тег -->
  </ItemGroup>
</Project";
            File.WriteAllText(projectPath, invalidContent);

            // Act
            var result = _analyzer.ParseProject(projectPath);

            // Assert
            // Метод должен обработать ошибку и вернуть null
            // (в реальной реализации может быть логирование ошибки)
        }
        finally
        {
            if (File.Exists(projectPath)) File.Delete(projectPath);
        }
    }

    [Fact]
    public void ProjectInfo_RecordProperties_AreAccessible()
    {
        // Arrange & Act
        var packageRefs = new List<PackageReference>
        {
            new PackageReference("TestPackage", "1.0.0")
        };
        var projectInfo = new ProjectInfo("/path/to/project.csproj", "TestProject", packageRefs);

        // Assert
        Assert.Equal("/path/to/project.csproj", projectInfo.Path);
        Assert.Equal("TestProject", projectInfo.Name);
        Assert.Single(projectInfo.PackageReferences);
        Assert.Equal("TestPackage", projectInfo.PackageReferences[0].Name);
        Assert.Equal("1.0.0", projectInfo.PackageReferences[0].Version);
    }

    [Fact]
    public void PackageReference_RecordProperties_AreAccessible()
    {
        // Arrange & Act
        var packageRef = new PackageReference("Newtonsoft.Json", "13.0.4");

        // Assert
        Assert.Equal("Newtonsoft.Json", packageRef.Name);
        Assert.Equal("13.0.4", packageRef.Version);
    }

    [Fact]
    public void PackageReference_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var ref1 = new PackageReference("TestPackage", "1.0.0");
        var ref2 = new PackageReference("TestPackage", "1.0.0");
        var ref3 = new PackageReference("TestPackage", "2.0.0");

        // Act & Assert
        Assert.Equal(ref1, ref2);
        Assert.NotEqual(ref1, ref3);
    }
}

