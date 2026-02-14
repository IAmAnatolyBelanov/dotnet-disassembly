using System.Xml.Linq;

namespace Disassembly.Tool.Core;

/// <summary>
/// Информация о проекте
/// </summary>
public record ProjectInfo(
    string Path,
    string Name,
    List<PackageReference> PackageReferences
);

/// <summary>
/// Ссылка на NuGet пакет
/// </summary>
public record PackageReference(
    string Name,
    string Version
);

/// <summary>
/// Анализатор решения для извлечения информации о проектах и NuGet пакетах
/// </summary>
public class SolutionAnalyzer
{
    /// <summary>
    /// Парсит .sln файл и возвращает список проектов
    /// </summary>
    public List<ProjectInfo> ParseSolution(string solutionPath)
    {
        if (!File.Exists(solutionPath))
            throw new FileNotFoundException($"Solution file not found: {solutionPath}");

        var projects = new List<ProjectInfo>();
        var solutionDirectory = Path.GetDirectoryName(solutionPath) ?? string.Empty;

        var lines = File.ReadAllLines(solutionPath);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Project(", StringComparison.OrdinalIgnoreCase))
            {
                // Парсим строку вида: Project("{...}") = "ProjectName", "ProjectPath.csproj", "{...}"
                var parts = trimmed.Split('=');
                if (parts.Length >= 2)
                {
                    var projectPathPart = parts[1].Trim().Split(',');
                    if (projectPathPart.Length >= 2)
                    {
                        var projectPath = projectPathPart[1].Trim().Trim('"');
                        // Нормализуем путь (заменяем обратные слеши на прямые для кроссплатформенности)
                        projectPath = projectPath.Replace('\\', Path.DirectorySeparatorChar);
                        var fullPath = Path.IsPathRooted(projectPath)
                            ? projectPath
                            : Path.Combine(solutionDirectory, projectPath);
                        // Нормализуем полный путь
                        fullPath = Path.GetFullPath(fullPath);

                        if (File.Exists(fullPath) && fullPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                        {
                            var projectInfo = ParseProject(fullPath);
                            if (projectInfo != null)
                            {
                                projects.Add(projectInfo);
                            }
                        }
                    }
                }
            }
        }

        return projects;
    }

    /// <summary>
    /// Парсит .csproj файл и извлекает PackageReference
    /// </summary>
    public ProjectInfo? ParseProject(string projectPath)
    {
        if (!File.Exists(projectPath))
            return null;

        try
        {
            var doc = XDocument.Load(projectPath);
            var root = doc.Root;
            if (root == null)
                return null;

            XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

            var packageReferences = new List<PackageReference>();

            // Ищем PackageReference элементы
            var packageRefs = root.Descendants()
                .Where(e => e.Name.LocalName == "PackageReference")
                .ToList();

            foreach (var packageRef in packageRefs)
            {
                var include = packageRef.Attribute("Include")?.Value;
                var version = packageRef.Attribute("Version")?.Value
                    ?? packageRef.Element(ns + "Version")?.Value
                    ?? packageRef.Element(XName.Get("Version", ""))?.Value;

                if (!string.IsNullOrWhiteSpace(include) && !string.IsNullOrWhiteSpace(version))
                {
                    packageReferences.Add(new PackageReference(include, version));
                }
            }

            var projectName = Path.GetFileNameWithoutExtension(projectPath);

            return new ProjectInfo(projectPath, projectName, packageReferences);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing project {projectPath}: {ex.Message}");
            return null;
        }
    }
}

