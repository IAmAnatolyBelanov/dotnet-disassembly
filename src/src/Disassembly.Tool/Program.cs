using Disassembly.Tool.Core;
using Disassembly.Tool.CodeGeneration;
using Disassembly.Tool.FileSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;

namespace Disassembly.Tool;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        string? solutionPath = null;
        string outputPath = "./NugetDisassembly";

        // Простой парсинг аргументов
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--solution" or "-s":
                    if (i + 1 < args.Length)
                    {
                        solutionPath = args[++i];
                    }
                    break;
                case "--output" or "-o":
                    if (i + 1 < args.Length)
                    {
                        outputPath = args[++i];
                    }
                    break;
                case "--help" or "-h":
                    PrintUsage();
                    return 0;
            }
        }

        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            Console.WriteLine("Error: --solution parameter is required");
            PrintUsage();
            return 1;
        }

        try
        {
            ProcessSolution(solutionPath, outputPath);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine("dotnet-disassembly [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --solution, -s <path>    Path to .sln file (required)");
        Console.WriteLine("  --output, -o <path>      Output directory (default: ./NugetDisassembly)");
        Console.WriteLine("  --help, -h               Show this help message");
    }

    static void ProcessSolution(string solutionPath, string outputPath)
    {
        Console.WriteLine($"Processing solution: {solutionPath}");
        Console.WriteLine($"Output directory: {outputPath}");

        // 1. Анализ решения
        var solutionAnalyzer = new SolutionAnalyzer();
        var projects = solutionAnalyzer.ParseSolution(solutionPath);
        Console.WriteLine($"Found {projects.Count} project(s)");

        // 2. Резолвинг NuGet пакетов
        var packageResolver = new NuGetPackageResolver();
        var packages = packageResolver.GetAllPackages(projects);
        Console.WriteLine($"Found {packages.Count} unique package(s)");

        // 3. Обработка каждого пакета
        foreach (var package in packages)
        {
            Console.WriteLine($"Processing package: {package.Name} {package.Version}");
            ProcessPackage(package, outputPath);
        }

        Console.WriteLine("Done!");
    }

    static void ProcessPackage(PackageInfo package, string outputRoot)
    {
        try
        {
            // Создаем структуру директорий
            var directoryBuilder = new DirectoryStructureBuilder();
            var packageRoot = directoryBuilder.CreatePackageDirectory(outputRoot, package);

            // Загружаем XML документацию, если доступна
            var xmlReader = new XmlDocumentationReader();
            if (!string.IsNullOrWhiteSpace(package.XmlDocPath))
            {
                xmlReader.LoadXmlDocumentation(package.XmlDocPath);
                Console.WriteLine($"  Loaded XML documentation from {Path.GetFileName(package.XmlDocPath)}");
            }

            // Читаем метаданные сборки
            var reflector = new AssemblyReflector();
            var types = reflector.ReadAssembly(package.DllPath);
            Console.WriteLine($"  Found {types.Count} public/protected type(s)");

            // Собираем XML комментарии для типов и членов
            var typeComments = new Dictionary<string, TypeComments>();
            var memberComments = new Dictionary<string, MemberComments>();

            foreach (var type in types)
            {
                if (type.OriginalType != null)
                {
                    var typeXmlId = XmlDocumentationReader.GenerateTypeXmlId(type.OriginalType);
                    var comments = xmlReader.GetTypeComments(typeXmlId);
                    if (comments != null)
                    {
                        typeComments[typeXmlId] = comments;
                    }

                    // Собираем комментарии для членов
                    foreach (var member in type.Members)
                    {
                        if (member.OriginalMember != null)
                        {
                            var memberXmlId = XmlDocumentationReader.GenerateMemberXmlId(member.OriginalMember);
                            var memberComment = xmlReader.GetMemberComments(memberXmlId);
                            if (memberComment != null)
                            {
                                memberComments[memberXmlId] = memberComment;
                            }
                        }
                    }

                    // Собираем комментарии для вложенных типов
                    CollectCommentsForNestedTypes(type, xmlReader, typeComments, memberComments);
                }
            }

            if (typeComments.Count > 0 || memberComments.Count > 0)
            {
                Console.WriteLine($"  Found {typeComments.Count} type comment(s) and {memberComments.Count} member comment(s)");
            }

            // Организуем по namespace
            var organized = directoryBuilder.OrganizeByNamespace(types);

            // Генерируем код с XML комментариями
            var codeGenerator = new RoslynCodeGenerator(typeComments, memberComments);
            var fileNameResolver = new FileNameResolver();
            var nameCounters = fileNameResolver.InitializeNameCounters(types);

            int fileCount = 0;
            foreach (var type in types)
            {
                try
                {
                    var fileName = fileNameResolver.ResolveFileName(type, nameCounters);
                    var filePath = directoryBuilder.GetFilePathForType(packageRoot, type, fileName);

                    var compilationUnit = codeGenerator.GenerateFile(type);
                    var formatted = Formatter.Format(compilationUnit, new AdhocWorkspace());

                    File.WriteAllText(filePath, formatted.ToFullString());
                    fileCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Warning: Failed to generate code for {type.Name}: {ex.Message}");
                }
            }

            Console.WriteLine($"  Generated {fileCount} file(s)");

            reflector.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error processing package {package.Name}: {ex.Message}");
        }
    }

    static void CollectCommentsForNestedTypes(
        TypeMetadata type,
        XmlDocumentationReader xmlReader,
        Dictionary<string, TypeComments> typeComments,
        Dictionary<string, MemberComments> memberComments)
    {
        foreach (var nestedType in type.NestedTypes)
        {
            if (nestedType.OriginalType != null)
            {
                var typeXmlId = XmlDocumentationReader.GenerateTypeXmlId(nestedType.OriginalType);
                var comments = xmlReader.GetTypeComments(typeXmlId);
                if (comments != null)
                {
                    typeComments[typeXmlId] = comments;
                }

                // Собираем комментарии для членов вложенного типа
                foreach (var member in nestedType.Members)
                {
                    if (member.OriginalMember != null)
                    {
                        var memberXmlId = XmlDocumentationReader.GenerateMemberXmlId(member.OriginalMember);
                        var memberComment = xmlReader.GetMemberComments(memberXmlId);
                        if (memberComment != null)
                        {
                            memberComments[memberXmlId] = memberComment;
                        }
                    }
                }

                // Рекурсивно обрабатываем вложенные типы
                CollectCommentsForNestedTypes(nestedType, xmlReader, typeComments, memberComments);
            }
        }
    }
}

