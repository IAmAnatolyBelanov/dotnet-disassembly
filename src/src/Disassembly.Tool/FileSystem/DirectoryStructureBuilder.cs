using Disassembly.Tool.Core;

namespace Disassembly.Tool.FileSystem;

/// <summary>
/// Построитель структуры директорий для выходных файлов
/// </summary>
public class DirectoryStructureBuilder
{
    /// <summary>
    /// Создает структуру директорий для пакета
    /// </summary>
    public string CreatePackageDirectory(string outputRoot, PackageInfo packageInfo)
    {
        var packageDir = Path.Combine(outputRoot, packageInfo.Name, packageInfo.Version);
        Directory.CreateDirectory(packageDir);
        return packageDir;
    }

    /// <summary>
    /// Создает путь к файлу на основе namespace
    /// </summary>
    public string GetFilePathForType(string packageRoot, TypeMetadata typeMetadata, string fileName)
    {
        var ns = typeMetadata.Namespace;
        
        if (string.IsNullOrWhiteSpace(ns))
        {
            return Path.Combine(packageRoot, fileName);
        }

        // Разбиваем namespace на части и создаем поддиректории
        var nsParts = ns.Split('.');
        var typeDir = Path.Combine(packageRoot, Path.Combine(nsParts));
        Directory.CreateDirectory(typeDir);

        return Path.Combine(typeDir, fileName);
    }

    /// <summary>
    /// Организует типы по namespace
    /// </summary>
    public Dictionary<string, List<TypeMetadata>> OrganizeByNamespace(List<TypeMetadata> types)
    {
        var organized = new Dictionary<string, List<TypeMetadata>>();

        foreach (var type in types)
        {
            var ns = type.Namespace ?? "Global";
            if (!organized.ContainsKey(ns))
            {
                organized[ns] = new List<TypeMetadata>();
            }
            organized[ns].Add(type);
        }

        return organized;
    }
}

