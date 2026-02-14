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
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var nugetPath = Path.Combine(homeDir, ".nuget", "packages");

        if (Directory.Exists(nugetPath))
            return nugetPath;

        // Альтернативные пути
        var altPaths = new[]
        {
            Path.Combine(homeDir, ".nuget", "packages"),
            Path.Combine(Environment.GetEnvironmentVariable("NUGET_PACKAGES") ?? string.Empty),
        };

        foreach (var path in altPaths)
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                return path;
        }

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
    /// Получает все пакеты из решения
    /// </summary>
    public List<PackageInfo> GetAllPackages(List<ProjectInfo> projects)
    {
        var packages = new Dictionary<string, PackageInfo>();

        foreach (var project in projects)
        {
            foreach (var packageRef in project.PackageReferences)
            {
                var key = $"{packageRef.Name}:{packageRef.Version}";
                if (!packages.ContainsKey(key))
                {
                    var packageInfo = ResolvePackage(packageRef.Name, packageRef.Version);
                    if (packageInfo != null)
                    {
                        packages[key] = packageInfo;
                    }
                }
            }
        }

        return packages.Values.ToList();
    }
}

