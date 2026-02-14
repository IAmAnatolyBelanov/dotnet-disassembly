# План реализации инструмента для дизассемблирования NuGet пакетов

## Цель проекта

Создать инструмент, который извлекает публичные интерфейсы из .NET библиотек (NuGet пакетов) для помощи AI-агентам в работе с актуальными версиями библиотек.

## Требования

1. Анализ всех NuGet пакетов в решении (.sln)
2. Извлечение только публичных и protected членов (классы, методы, свойства, поля)
3. Извлечение XML-комментариев
4. Создание структуры папок: `NugetDisassembly/{PackageName}/{Version}/`
5. Организация C# файлов по namespace (создание поддиректорий)
6. Специальная обработка generic типов (имя файла с суффиксом `_Generic`)
7. Только интерфейсы, без реализации (пустые тела методов или комментарии)

---

## Варианты архитектуры решения

### Вариант 1: Использование System.Reflection + Roslyn

**Преимущества:**
- System.Reflection встроен в .NET, не требует дополнительных зависимостей
- Roslyn позволяет генерировать синтаксически корректные C# код
- Хорошая поддержка XML-комментариев через `XmlDocumentationProvider`

**Недостатки:**
- System.Reflection требует загрузки сборок, что может быть проблематично для некоторых библиотек
- Может не работать с обфусцированными библиотеками (хотя публичные интерфейсы обычно не обфусцируются)
- Roslyn добавляет зависимость

**Технологии:**
- `System.Reflection` для анализа метаданных
- `Microsoft.CodeAnalysis.CSharp` для генерации синтаксических деревьев
- `Microsoft.CodeAnalysis.CSharp.Syntax` для создания C# кода
- `System.Xml` для парсинга XML-комментариев

**Структура:**
```
Disassembly.Tool/
  ├── Core/
  │   ├── SolutionAnalyzer.cs          // Парсинг .sln и .csproj
  │   ├── NuGetPackageResolver.cs      // Поиск DLL в NuGet кэше
  │   ├── AssemblyReflector.cs         // Рефлексия сборок
  │   └── XmlCommentExtractor.cs       // Извлечение XML-комментариев
  ├── CodeGeneration/
  │   ├── CSharpCodeGenerator.cs       // Генерация C# кода через Roslyn
  │   ├── NamespaceOrganizer.cs        // Организация по namespace
  │   └── GenericTypeHandler.cs        // Обработка generic типов
  └── Program.cs
```

---

### Вариант 2: Использование Mono.Cecil

**Преимущества:**
- Mono.Cecil не требует загрузки сборок в домен приложения
- Работает напрямую с метаданными IL
- Легче обрабатывать зависимости между сборками
- Меньше проблем с версиями зависимостей
- Хорошо работает с обфусцированными библиотеками (на уровне метаданных)

**Недостатки:**
- Требует дополнительной зависимости (NuGet пакет)
- Нужно вручную конвертировать IL метаданные в C# синтаксис
- Может быть сложнее извлекать XML-комментарии (нужно искать отдельные XML файлы)

**Технологии:**
- `Mono.Cecil` для чтения метаданных сборок
- `Mono.Cecil.XmlDoc` для XML-комментариев (если доступен)
- Roslyn или ручная генерация C# кода

**Структура:**
```
Disassembly.Tool/
  ├── Core/
  │   ├── SolutionAnalyzer.cs
  │   ├── NuGetPackageResolver.cs
  │   ├── CecilAssemblyReader.cs       // Чтение через Mono.Cecil
  │   └── XmlCommentExtractor.cs
  ├── CodeGeneration/
  │   ├── CSharpCodeGenerator.cs
  │   ├── ILToCSharpConverter.cs       // Конвертация IL → C#
  │   ├── NamespaceOrganizer.cs
  │   └── GenericTypeHandler.cs
  └── Program.cs
```

---

### Вариант 3: Использование ILSpy API

**Преимущества:**
- ILSpy специально создан для декомпиляции .NET сборок
- Отличная поддержка всех конструкций C#
- Встроенная поддержка XML-комментариев
- Уже решает проблему конвертации IL → C#
- Хорошо обрабатывает generic типы

**Недостатки:**
- Большая зависимость (ILSpy довольно тяжелый)
- Может пытаться декомпилировать больше, чем нужно (нужно фильтровать)
- Может быть избыточным для задачи

**Технологии:**
- `ICSharpCode.Decompiler` (ILSpy библиотека)
- Встроенные возможности ILSpy для фильтрации публичных членов

**Структура:**
```
Disassembly.Tool/
  ├── Core/
  │   ├── SolutionAnalyzer.cs
  │   ├── NuGetPackageResolver.cs
  │   └── ILSpyDecompiler.cs           // Использование ILSpy API
  ├── Filters/
  │   └── PublicMembersFilter.cs       // Фильтрация только публичных
  ├── CodeGeneration/
  │   ├── NamespaceOrganizer.cs
  │   └── GenericTypeHandler.cs
  └── Program.cs
```

---

### Вариант 4: Гибридный подход (Рекомендуемый)

**Идея:** Комбинировать лучшие части разных подходов

**Архитектура:**
1. **Mono.Cecil** для чтения метаданных (не требует загрузки сборок)
2. **Roslyn** для генерации чистого C# кода
3. **Ручной парсинг XML-комментариев** из .xml файлов NuGet пакетов

**Преимущества:**
- Гибкость и контроль над процессом
- Не требует загрузки сборок
- Чистая генерация кода через Roslyn
- Можно точно контролировать, что включать/исключать

**Структура:**
```
Disassembly.Tool/
  ├── Core/
  │   ├── SolutionAnalyzer.cs          // Парсинг .sln/.csproj через MSBuild API
  │   ├── NuGetPackageResolver.cs      // Поиск в NuGet кэше (~/.nuget/packages)
  │   ├── CecilMetadataReader.cs       // Чтение метаданных через Mono.Cecil
  │   └── XmlDocumentationReader.cs   // Чтение XML-комментариев
  ├── Filters/
  │   ├── MemberVisibilityFilter.cs   // Фильтр публичных/protected
  │   └── MemberTypeFilter.cs         // Что включать (классы, методы, свойства)
  ├── CodeGeneration/
  │   ├── RoslynCodeGenerator.cs      // Генерация через Roslyn SyntaxFactory
  │   ├── NamespaceOrganizer.cs       // Организация файлов по namespace
  │   ├── GenericTypeNaming.cs        // Обработка generic (ClassName_Generic.cs)
  │   └── MethodBodyGenerator.cs     // Генерация пустых тел или комментариев
  ├── FileSystem/
  │   └── DirectoryStructureBuilder.cs // Создание структуры папок
  └── Program.cs
```

---

## Детальный план реализации

### Этап 1: Анализ решения и извлечение NuGet пакетов

**Задачи:**
1. Парсинг .sln файла для получения списка проектов
2. Парсинг каждого .csproj для извлечения `<PackageReference>`
3. Поиск DLL файлов в NuGet кэше (`~/.nuget/packages` или `%USERPROFILE%\.nuget\packages`)
4. Обработка версий пакетов (может быть несколько версий одного пакета)

**Технологии:**
- `Microsoft.Build` API для парсинга .csproj
- Или ручной XML парсинг
- `System.IO` для работы с файловой системой

**Выходные данные:**
- Список кортежей: `(PackageName, Version, DllPath, XmlDocPath)`

---

### Этап 2: Чтение метаданных сборок

**Задачи:**
1. Загрузка сборки через Mono.Cecil (без выполнения кода)
2. Извлечение всех типов (классы, интерфейсы, структуры, enum)
3. Фильтрация по видимости (public, protected)
4. Извлечение членов типов (методы, свойства, поля, события)
5. Обработка generic типов и методов
6. Обработка атрибутов (опционально)

**Ключевые моменты:**
- Игнорировать private/internal члены
- Сохранять информацию о generic параметрах
- Сохранять информацию о параметрах методов
- Сохранять информацию о возвращаемых типах

---

### Этап 3: Извлечение XML-комментариев

**Задачи:**
1. Поиск соответствующих .xml файлов документации в NuGet пакете
2. Парсинг XML документации
3. Сопоставление комментариев с типами и членами через XML ID
4. Извлечение `<summary>`, `<param>`, `<returns>`, `<remarks>`, `<exception>`

**Формат XML ID в .NET:**
- Тип: `T:Namespace.TypeName`
- Метод: `M:Namespace.TypeName.MethodName(ParamType1,ParamType2)`
- Свойство: `P:Namespace.TypeName.PropertyName`
- Поле: `F:Namespace.TypeName.FieldName`

---

### Этап 4: Генерация C# кода

**Задачи:**
1. Создание синтаксических деревьев через Roslyn `SyntaxFactory`
2. Генерация объявлений типов с правильными модификаторами
3. Генерация методов с сигнатурами (без реализации или с комментарием)
4. Генерация свойств, полей, событий
5. Добавление XML-комментариев как тривиальных комментариев
6. Обработка generic типов (создание файлов с суффиксом `_Generic`)

**Пример генерации метода:**
```csharp
/// <summary>
/// Method description
/// </summary>
/// <param name="param1">Parameter description</param>
/// <returns>Return description</returns>
public ReturnType MethodName(ParamType param1)
{
    // Implementation in original library
}
```

---

### Этап 5: Организация файлов по структуре

**Задачи:**
1. Создание структуры папок: `NugetDisassembly/{PackageName}/{Version}/`
2. Разделение по namespace (создание поддиректорий)
3. Именование файлов:
   - Обычный тип: `TypeName.cs`
   - Generic тип: `TypeName_Generic.cs`
   - Обработка конфликтов имен
4. Копирование не-C# файлов (если есть)

**Пример структуры:**
```
NugetDisassembly/
  └── Newtonsoft.Json/
      └── 13.0.4/
          └── Newtonsoft/
              └── Json/
                  ├── JsonConverter.cs
                  ├── JsonConverter_Generic.cs
                  └── Converters/
                      └── StringEnumConverter.cs
```

---

### Этап 6: Обработка edge cases

**Задачи:**
1. Обработка вложенных типов (nested types)
2. Обработка partial классов
3. Обработка атрибутов (включить или исключить?)
4. Обработка перегрузок методов
5. Обработка явных реализаций интерфейсов
6. Обработка статических членов
7. Обработка extension методов
8. Обработка nullable reference types

---

## Варианты интерфейса инструмента

### Вариант A: CLI инструмент

```bash
dotnet-disassembly [options]

Options:
  --solution <path>     Путь к .sln файлу
  --output <path>       Путь для вывода (по умолчанию: ./NugetDisassembly)
  --packages <list>     Список конкретных пакетов (опционально)
  --include-private     Включить private члены (не рекомендуется)
  --include-implementation  Включить реализации методов
```

### Вариант B: MSBuild Target

Интеграция в процесс сборки, автоматическое обновление при изменении пакетов.

### Вариант C: Visual Studio Extension

GUI интерфейс для выбора пакетов и настройки параметров.

---

## Рекомендации

**Рекомендуемый подход: Вариант 4 (Гибридный)**

**Обоснование:**
1. Mono.Cecil дает контроль без загрузки сборок
2. Roslyn обеспечивает качественную генерацию кода
3. Гибкость в настройке фильтров
4. Можно начать с простой реализации и расширять

**Порядок реализации:**
1. Начать с CLI инструмента (Вариант A)
2. Реализовать базовый функционал для одного пакета
3. Добавить обработку всего решения
4. Добавить обработку edge cases
5. Оптимизировать и добавить опции

---

## Зависимости (предварительно)

```xml
<PackageReference Include="Mono.Cecil" Version="0.11.5" />
<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" />
<PackageReference Include="Microsoft.Build" Version="17.8.0" />
<PackageReference Include="System.Xml.ReaderWriter" Version="4.3.1" />
```

---

## Вопросы для уточнения

1. Нужно ли включать атрибуты в генерируемый код?
2. Как обрабатывать вложенные типы (nested types)?
3. Нужна ли поддержка старых форматов проектов (.csproj с packages.config)?
4. Нужна ли поддержка .NET Framework библиотек?
5. Как обрабатывать конфликты имен файлов (кроме generic)?
6. Нужна ли поддержка F# или VB.NET библиотек?

