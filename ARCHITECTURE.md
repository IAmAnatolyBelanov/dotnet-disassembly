# Архитектура инструмента дизассемблирования

## Общая схема работы

```
┌─────────────────────────────────────────────────────────────┐
│                    Входные данные                            │
│  • .sln файл                                                 │
│  • .csproj файлы                                             │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│          1. Solution Analyzer                               │
│  • Парсинг .sln                                             │
│  • Парсинг .csproj                                          │
│  • Извлечение PackageReference                              │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│          2. NuGet Package Resolver                            │
│  • Поиск в NuGet кэше (~/.nuget/packages)                    │
│  • Поиск DLL файлов                                          │
│  • Поиск XML документации                                    │
│  • Обработка версий                                          │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│          3. Assembly Metadata Reader (System.Reflection)    │
│  • Загрузка сборки через Assembly.LoadFrom()               │
│  • Извлечение типов (классы, интерфейсы, структуры)         │
│  • Извлечение членов (методы, свойства, поля)               │
│  • Обработка generic типов                                   │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│          4. Visibility Filter                                │
│  • Фильтрация public/protected                               │
│  • Исключение private/internal                               │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│          5. XML Documentation Reader                          │
│  • Парсинг XML файлов документации                           │
│  • Сопоставление с типами/членами через XML ID               │
│  • Извлечение summary, param, returns, remarks               │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│          6. C# Code Generator (Roslyn)                       │
│  • Генерация синтаксических деревьев                         │
│  • Создание объявлений типов                                 │
│  • Создание сигнатур методов (без реализации)                │
│  • Добавление XML-комментариев                               │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│          7. Namespace Organizer                              │
│  • Организация по namespace (поддиректории)                  │
│  • Обработка generic (ClassName_Generic.cs)                  │
│  • Разрешение конфликтов имен                                │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│          8. File System Writer                               │
│  • Создание структуры: NugetDisassembly/{Package}/{Version} │
│  • Запись C# файлов                                          │
│  • Копирование дополнительных файлов                         │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│                    Выходные данные                           │
│  NugetDisassembly/                                           │
│    └── PackageName/                                          │
│        └── Version/                                          │
│            └── Namespace/                                    │
│                └── TypeName.cs                               │
└─────────────────────────────────────────────────────────────┘
```

## Детальная структура компонентов

### 1. Solution Analyzer

```
SolutionAnalyzer
├── ParseSolution(string slnPath)
│   └── List<ProjectInfo>
├── ParseProject(string csprojPath)
│   └── ProjectInfo
│       ├── PackageReferences: List<(Name, Version)>
│       └── TargetFramework
└── ResolveProjectReferences()
    └── List<ProjectInfo>
```

### 2. NuGet Package Resolver

```
NuGetPackageResolver
├── FindNuGetCachePath()
│   └── string (путь к кэшу)
├── ResolvePackage(string name, string version)
│   └── PackageInfo
│       ├── DllPath: string
│       ├── XmlDocPath: string?
│       └── Dependencies: List<PackageInfo>
└── GetAllPackages(SolutionInfo)
    └── List<PackageInfo>
```

### 3. Assembly Metadata Reader (System.Reflection)

```
AssemblyReflector
├── LoadAssembly(string dllPath)
│   └── Assembly
├── ReadAssembly(string dllPath)
│   └── AssemblyMetadata
│       ├── Types: List<TypeMetadata>
│       └── References: List<string>
├── ExtractTypeMetadata(Type)
│   └── TypeMetadata
│       ├── Name: string
│       ├── Namespace: string
│       ├── IsGeneric: bool
│       ├── GenericParameters: List<string>
│       ├── Members: List<MemberMetadata>
│       └── Visibility: MemberVisibility
└── ExtractMemberMetadata(MemberInfo)
    └── MemberMetadata
        ├── Name: string
        ├── Type: MemberType (Method, Property, Field, Event)
        ├── Signature: string
        └── Visibility: MemberVisibility
```

### 4. Visibility Filter

```
MemberVisibilityFilter
├── IsPublicOrProtected(MemberMetadata)
│   └── bool
├── FilterTypes(List<TypeMetadata>)
│   └── List<TypeMetadata> (только public/protected)
└── FilterMembers(List<MemberMetadata>)
    └── List<MemberMetadata> (только public/protected)
```

### 5. XML Documentation Reader

```
XmlDocumentationReader
├── LoadXmlDocumentation(string xmlPath)
│   └── XmlDocumentation
├── GetTypeComments(string typeXmlId)
│   └── TypeComments
│       └── Summary: string
└── GetMemberComments(string memberXmlId)
    └── MemberComments
        ├── Summary: string
        ├── Parameters: Dictionary<string, string>
        ├── Returns: string
        └── Remarks: string
```

### 6. C# Code Generator (Roslyn)

```
RoslynCodeGenerator
├── GenerateTypeDeclaration(TypeMetadata, TypeComments?)
│   └── TypeDeclarationSyntax
├── GenerateMethodSignature(MethodMetadata, MemberComments?)
│   └── MethodDeclarationSyntax
│       └── Body: BlockSyntax (пустой или с комментарием)
├── GenerateProperty(PropertyMetadata, MemberComments?)
│   └── PropertyDeclarationSyntax
├── GenerateField(FieldMetadata, MemberComments?)
│   └── FieldDeclarationSyntax
└── AddXmlComments(SyntaxNode, Comments)
    └── SyntaxNode (с тривиальными комментариями)
```

### 7. Namespace Organizer

```
NamespaceOrganizer
├── OrganizeByNamespace(List<TypeMetadata>)
│   └── Dictionary<string, List<TypeMetadata>>
│       └── Key: namespace, Value: типы в этом namespace
├── GenerateFileName(TypeMetadata)
│   └── string
│       ├── Обычный: "TypeName.cs"
│       └── Generic: "TypeName_Generic.cs"
└── ResolveNameConflicts(List<string>)
    └── Dictionary<string, string> (оригинальное имя → уникальное имя)
```

### 8. File System Writer

```
FileSystemWriter
├── CreateDirectoryStructure(PackageInfo)
│   └── string (путь к корню пакета)
├── WriteCSharpFile(string path, SyntaxNode)
│   └── void
└── CopyAdditionalFiles(PackageInfo, string outputPath)
    └── void
```

## Поток данных

```
Solution (.sln)
    │
    ├─→ Project 1 (.csproj)
    │       │
    │       └─→ PackageReference: Newtonsoft.Json 13.0.4
    │               │
    │               ├─→ DLL: ~/.nuget/packages/newtonsoft.json/13.0.4/lib/netstandard2.0/Newtonsoft.Json.dll
    │               └─→ XML: ~/.nuget/packages/newtonsoft.json/13.0.4/lib/netstandard2.0/Newtonsoft.Json.xml
    │                       │
    │                       ├─→ System.Reflection: Загрузка и чтение метаданных
    │                       │       │
    │                       │       └─→ TypeMetadata[]
    │                       │               │
    │                       │               ├─→ Filter (public/protected)
    │                       │               │
    │                       │               └─→ TypeMetadata[] (отфильтрованные)
    │                       │
    │                       └─→ XML Reader: Чтение комментариев
    │                               │
    │                               └─→ Comments Dictionary
    │
    └─→ Project 2 (.csproj)
            └─→ ...


TypeMetadata + Comments
    │
    └─→ Roslyn Code Generator
            │
            └─→ SyntaxNode (C# код)
                    │
                    └─→ Namespace Organizer
                            │
                            └─→ File Path: NugetDisassembly/Newtonsoft.Json/13.0.4/Newtonsoft/Json/TypeName.cs
                                    │
                                    └─→ File System Writer
                                            │
                                            └─→ Файл записан
```

## Пример обработки generic типа

**Входные данные:**
- Тип: `JsonConverter<T>` в namespace `Newtonsoft.Json`

**Обработка:**
1. System.Reflection извлекает: `TypeName = "JsonConverter", IsGeneric = true, GenericParameters = ["T"]`
2. Visibility Filter: `IsPublic = true` ✓
3. Code Generator создает:
   ```csharp
   public abstract class JsonConverter<T> : JsonConverter
   {
       // Implementation in original library
   }
   ```
4. Namespace Organizer: `FileName = "JsonConverter_Generic.cs"`
5. File System Writer: `NugetDisassembly/Newtonsoft.Json/13.0.4/Newtonsoft/Json/JsonConverter_Generic.cs`

## Обработка конфликтов имен

**Проблема:** Два разных generic типа с одинаковым именем, но разным количеством параметров:
- `JsonConverter<T>`
- `JsonConverter<T1, T2>`

**Решение:** Использовать количество generic параметров в имени файла:
- `JsonConverter_Generic1.cs` (1 параметр)
- `JsonConverter_Generic2.cs` (2 параметра)

Или более явно:
- `JsonConverter_T.cs`
- `JsonConverter_T1_T2.cs`

