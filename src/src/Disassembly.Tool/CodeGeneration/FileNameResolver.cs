using Disassembly.Tool.Core;

namespace Disassembly.Tool.CodeGeneration;

/// <summary>
/// Резолвер имен файлов с обработкой дубликатов
/// </summary>
public class FileNameResolver
{
    /// <summary>
    /// Разрешает имя файла для типа, учитывая дубликаты
    /// </summary>
    public string ResolveFileName(TypeMetadata typeMetadata, Dictionary<string, int> nameCounters)
    {
        var baseName = GetBaseFileName(typeMetadata);
        var key = GetTypeKey(typeMetadata);

        if (!nameCounters.ContainsKey(key))
        {
            nameCounters[key] = 0;
            return $"{baseName}.cs";
        }

        nameCounters[key]++;
        var counter = nameCounters[key];

        return counter == 1 
            ? $"{baseName}1.cs" 
            : $"{baseName}{counter}.cs";
    }

    private string GetBaseFileName(TypeMetadata typeMetadata)
    {
        var name = typeMetadata.Name;

        // Для generic типов можно добавить суффикс, но пока используем просто имя
        // В будущем можно сделать: TypeName_Generic.cs для JsonConverter<T>
        if (typeMetadata.IsGeneric)
        {
            // Можно использовать просто имя или добавить суффикс
            // name = $"{name}_Generic";
        }

        return name;
    }

    private string GetTypeKey(TypeMetadata typeMetadata)
    {
        // Ключ для отслеживания дубликатов: namespace + имя типа
        var ns = typeMetadata.Namespace ?? "Global";
        return $"{ns}.{typeMetadata.Name}";
    }

    /// <summary>
    /// Инициализирует счетчики имен для всех типов
    /// </summary>
    public Dictionary<string, int> InitializeNameCounters(List<TypeMetadata> types)
    {
        var counters = new Dictionary<string, int>();
        var seen = new HashSet<string>();

        foreach (var type in types)
        {
            var key = GetTypeKey(type);
            if (seen.Contains(key))
            {
                if (!counters.ContainsKey(key))
                {
                    counters[key] = 1;
                }
                else
                {
                    counters[key]++;
                }
            }
            else
            {
                seen.Add(key);
            }
        }

        return counters;
    }
}

