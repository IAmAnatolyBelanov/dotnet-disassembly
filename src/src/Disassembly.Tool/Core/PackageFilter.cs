namespace Disassembly.Tool.Core;

/// <summary>
/// Фильтр пакетов по имени (include/exclude) с поддержкой стандартных исключений Microsoft BCL.
/// </summary>
public static class PackageFilter
{
    private static readonly HashSet<string> DefaultExcludedPackages = new(StringComparer.Ordinal)
    {
        "System.Linq",
        "System.Linq.Expressions",
        "System.Linq.Queryable",
        "System.Linq.Parallel",
        "System.Collections",
        "System.Collections.NonGeneric",
        "System.Collections.Concurrent",
        "System.Collections.Immutable",
        "System.Numerics",
        "System.Numerics.Vectors",
        "System.Runtime",
        "System.Runtime.Extensions",
        "System.Runtime.CompilerServices.Unsafe",
        "System.Memory",
        "System.Buffers",
        "System.Threading",
        "System.Threading.Tasks",
        "System.Threading.Tasks.Extensions",
        "System.Threading.Channels",
        "System.ValueTuple",
        "System.Text.Json",
        "System.Text.RegularExpressions",
        "System.Text.Encoding",
        "netstandard",
        "Microsoft.Win32.Primitives",
        "Microsoft.Win32.Registry",
        "Microsoft.Bcl.AsyncInterfaces",
        "System.ObjectModel",
        "System.ComponentModel.Annotations",
        "System.ComponentModel.Primitives",
        "System.ComponentModel.TypeConverter",
        "System.Reflection.Emit",
        "System.Reflection.Emit.ILGeneration",
        "System.Reflection.Primitives",
        "System.Runtime.InteropServices",
        "System.Runtime.InteropServices.RuntimeInformation",
        "System.Private.CoreLib",
    };

    /// <summary>
    /// Применяет фильтр к списку пакетов.
    /// </summary>
    /// <param name="packages">Исходный список пакетов.</param>
    /// <param name="exclude">Имена пакетов для исключения (case-sensitive).</param>
    /// <param name="include">Имена пакетов для включения — обрабатываются только они. Если пусто, обрабатываются все.</param>
    /// <param name="includeDefault">Если true, стандартные Microsoft-библиотеки НЕ добавляются в exclude.</param>
    /// <returns>Отфильтрованный список пакетов.</returns>
    public static List<PackageInfo> ApplyFilter(
        List<PackageInfo> packages,
        HashSet<string> exclude,
        HashSet<string> include,
        bool includeDefault)
    {
        var effectiveExclude = new HashSet<string>(exclude, StringComparer.Ordinal);
        if (!includeDefault)
        {
            foreach (var name in DefaultExcludedPackages)
            {
                effectiveExclude.Add(name);
            }
        }

        IEnumerable<PackageInfo> result = packages;

        if (include.Count > 0)
        {
            result = result.Where(p => include.Contains(p.Name));
        }

        result = result.Where(p => !effectiveExclude.Contains(p.Name));

        return result.ToList();
    }

    /// <summary>
    /// Добавляет имена из value в существующий HashSet.
    /// </summary>
    public static void AddParsedNames(HashSet<string> target, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        foreach (var part in value.Split(','))
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                target.Add(trimmed);
            }
        }
    }
}
