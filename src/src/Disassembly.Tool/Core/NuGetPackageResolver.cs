using System.Xml.Linq;

namespace Disassembly.Tool.Core;

/// <summary>
/// Информация о NuGet пакете
/// </summary>
public record PackageInfo(
    string Name,
    string Version,
    string DllPath,
    string? XmlDocPath
);

/// <summary>
/// Резолвер для поиска NuGet пакетов в кэше
/// </summary>
public class NuGetPackageResolver
{
    /// <summary>
    /// Находит путь к NuGet кэшу
    /// </summary>
    public string FindNuGetCachePath()
    {
        // Сначала проверяем переменную окружения (имеет приоритет для тестирования)
        var nugetPackagesEnv = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrWhiteSpace(nugetPackagesEnv) && Directory.Exists(nugetPackagesEnv))
            return nugetPackagesEnv;

        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var nugetPath = Path.Combine(homeDir, ".nuget", "packages");

        if (Directory.Exists(nugetPath))
            return nugetPath;

        throw new DirectoryNotFoundException("NuGet packages cache not found. Please ensure NuGet packages are restored.");
    }

    /// <summary>
    /// Резолвит пакет по имени и версии
    /// </summary>
    public PackageInfo? ResolvePackage(string name, string version)
    {
        var cachePath = FindNuGetCachePath();
        var packageDir = Path.Combine(cachePath, name.ToLowerInvariant(), version.ToLowerInvariant());

        if (!Directory.Exists(packageDir))
            return null;

        // Ищем DLL файлы в lib/
        var libDir = Path.Combine(packageDir, "lib");
        if (!Directory.Exists(libDir))
            return null;

        string? dllPath = null;
        string? xmlDocPath = null;

        // Ищем в поддиректориях lib (netstandard2.0, net6.0, etc.)
        var frameworkDirs = Directory.GetDirectories(libDir);
        foreach (var frameworkDir in frameworkDirs)
        {
            var dllFiles = Directory.GetFiles(frameworkDir, "*.dll", SearchOption.TopDirectoryOnly);
            if (dllFiles.Length > 0)
            {
                // Предпочитаем основной DLL (обычно с именем пакета)
                var mainDll = dllFiles.FirstOrDefault(f => 
                    Path.GetFileNameWithoutExtension(f).Equals(name, StringComparison.OrdinalIgnoreCase));

                if (mainDll != null)
                {
                    dllPath = mainDll;
                    var xmlFile = Path.ChangeExtension(mainDll, ".xml");
                    if (File.Exists(xmlFile))
                    {
                        xmlDocPath = xmlFile;
                    }
                    break;
                }

                // Если не нашли основной, берем первый
                if (dllPath == null)
                {
                    dllPath = dllFiles[0];
                    var xmlFile = Path.ChangeExtension(dllPath, ".xml");
                    if (File.Exists(xmlFile))
                    {
                        xmlDocPath = xmlFile;
                    }
                }
            }
        }

        if (dllPath == null)
            return null;

        return new PackageInfo(name, version, dllPath, xmlDocPath);
    }

    /// <summary>
    /// Читает зависимости из .nuspec файла пакета
    /// </summary>
    private List<PackageReference> ReadPackageDependencies(string packageName, string version)
    {
        var dependencies = new List<PackageReference>();
        var cachePath = FindNuGetCachePath();
        var packageDir = Path.Combine(cachePath, packageName.ToLowerInvariant(), version.ToLowerInvariant());

        if (!Directory.Exists(packageDir))
            return dependencies;

        // Ищем .nuspec файл
        var nuspecFiles = Directory.GetFiles(packageDir, "*.nuspec", SearchOption.TopDirectoryOnly);
        if (nuspecFiles.Length == 0)
            return dependencies;

        try
        {
            var nuspecPath = nuspecFiles[0];
            var doc = XDocument.Load(nuspecPath);
            var root = doc.Root;
            if (root == null)
                return dependencies;

            XNamespace ns = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
            
            // Ищем секцию dependencies
            var metadata = root.Element(ns + "metadata");
            if (metadata == null)
                return dependencies;

            var dependenciesElement = metadata.Element(ns + "dependencies");
            if (dependenciesElement == null)
                return dependencies;

            // Обрабатываем все группы зависимостей
            var groups = dependenciesElement.Elements(ns + "group");
            foreach (var group in groups)
            {
                var dependencyElements = group.Elements(ns + "dependency");
                foreach (var dep in dependencyElements)
                {
                    var depId = dep.Attribute("id")?.Value;
                    var depVersion = dep.Attribute("version")?.Value;
                    var exclude = dep.Attribute("exclude")?.Value;

                    // Пропускаем зависимости с exclude="Compile" (они не являются compile-time зависимостями)
                    if (exclude != null && exclude.Contains("Compile", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!string.IsNullOrWhiteSpace(depId) && !string.IsNullOrWhiteSpace(depVersion))
                    {
                        // Обрабатываем версии с диапазонами (например, "[4.3.0, 5.0.0)")
                        // Для простоты берем минимальную версию из диапазона
                        var resolvedVersion = ResolveVersionRange(depVersion);
                        if (resolvedVersion != null)
                        {
                            // Проверяем, что не дублируем зависимость с той же версией
                            var key = $"{depId}:{resolvedVersion}";
                            if (!dependencies.Any(d => $"{d.Name}:{d.Version}".Equals(key, StringComparison.OrdinalIgnoreCase)))
                            {
                                dependencies.Add(new PackageReference(depId, resolvedVersion));
                            }
                        }
                    }
                }
            }

            // Также обрабатываем зависимости без группы (устаревший формат)
            var directDeps = dependenciesElement.Elements(ns + "dependency");
            foreach (var dep in directDeps)
            {
                var depId = dep.Attribute("id")?.Value;
                var depVersion = dep.Attribute("version")?.Value;
                var exclude = dep.Attribute("exclude")?.Value;

                if (exclude != null && exclude.Contains("Compile", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!string.IsNullOrWhiteSpace(depId) && !string.IsNullOrWhiteSpace(depVersion))
                {
                    var resolvedVersion = ResolveVersionRange(depVersion);
                    if (resolvedVersion != null)
                    {
                        // Проверяем, что не дублируем зависимость
                        if (!dependencies.Any(d => d.Name.Equals(depId, StringComparison.OrdinalIgnoreCase)))
                        {
                            dependencies.Add(new PackageReference(depId, resolvedVersion));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to read dependencies from {packageName} {version}: {ex.Message}");
        }

        return dependencies;
    }

    /// <summary>
    /// Разрешает диапазон версий в конкретную версию
    /// </summary>
    private string? ResolveVersionRange(string versionRange)
    {
        if (string.IsNullOrWhiteSpace(versionRange))
            return null;

        // Убираем пробелы
        versionRange = versionRange.Trim();

        // Если это простая версия без диапазона, возвращаем как есть
        if (!versionRange.StartsWith('[') && !versionRange.StartsWith('('))
            return versionRange;

        // Обрабатываем диапазоны вида [4.3.0, 5.0.0) или (4.3.0, 5.0.0]
        // Берем минимальную версию из диапазона
        var parts = versionRange.Split(',');
        if (parts.Length >= 2)
        {
            var minVersion = parts[0].TrimStart('[', '(').Trim();
            return minVersion;
        }

        // Если не удалось распарсить, возвращаем как есть
        return versionRange;
    }

    /// <summary>
    /// Рекурсивно разрешает все зависимости пакета, включая транзитивные
    /// </summary>
    private void ResolvePackageWithDependencies(
        string packageName,
        string version,
        Dictionary<string, PackageInfo> resolvedPackages,
        HashSet<string> processingPackages)
    {
        var key = $"{packageName}:{version}";
        
        // Если уже обработали этот пакет, пропускаем
        if (resolvedPackages.ContainsKey(key))
            return;

        // Если пакет уже обрабатывается (циклическая зависимость), пропускаем
        if (processingPackages.Contains(key))
        {
            Console.WriteLine($"Warning: Circular dependency detected for {packageName} {version}");
            return;
        }

        processingPackages.Add(key);

        // Резолвим сам пакет
        var packageInfo = ResolvePackage(packageName, version);
        if (packageInfo == null)
        {
            Console.WriteLine($"Warning: Could not resolve package {packageName} {version}");
            processingPackages.Remove(key);
            return;
        }

        resolvedPackages[key] = packageInfo;

        // Читаем зависимости пакета
        var dependencies = ReadPackageDependencies(packageName, version);
        
        // Рекурсивно разрешаем каждую зависимость
        foreach (var dependency in dependencies)
        {
            ResolvePackageWithDependencies(
                dependency.Name,
                dependency.Version,
                resolvedPackages,
                processingPackages);
        }

        processingPackages.Remove(key);
    }

    /// <summary>
    /// Получает все пакеты из решения, включая транзитивные зависимости
    /// </summary>
    public List<PackageInfo> GetAllPackages(List<ProjectInfo> projects)
    {
        var packages = new Dictionary<string, PackageInfo>();
        var processingPackages = new HashSet<string>();

        // Сначала собираем все прямые зависимости из проектов
        var directDependencies = new List<PackageReference>();
        foreach (var project in projects)
        {
            directDependencies.AddRange(project.PackageReferences);
        }

        // Рекурсивно разрешаем все зависимости
        foreach (var packageRef in directDependencies)
        {
            ResolvePackageWithDependencies(
                packageRef.Name,
                packageRef.Version,
                packages,
                processingPackages);
        }

        return packages.Values.ToList();
    }
}

