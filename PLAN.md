# План реализации инструмента для дизассемблирования NuGet пакетов

## Цель проекта

Создать инструмент, который извлекает публичные интерфейсы из .NET библиотек (NuGet пакетов) для помощи AI-агентам в работе с актуальными версиями библиотек.

## Требования

1. Анализ всех NuGet пакетов в решении (.sln)
2. Извлечение только публичных и protected членов (классы, методы, свойства, поля)
3. Извлечение XML-комментариев
4. Создание структуры папок: `NugetDisassembly/{PackageName}/{Version}/`
5. Организация C# файлов по namespace (создание поддиректорий)
6. Обработка дублирующихся имен классов через счетчик (первое появление: `TypeName.cs`, второе: `TypeName1.cs`, третье: `TypeName2.cs` и т.д.)
7. Только интерфейсы, без реализации (пустые тела методов или комментарии)

---

## Архитектура решения

**Выбранный подход: System.Reflection + Roslyn**

**Архитектура:**
1. **System.Reflection** для чтения метаданных (встроен в .NET)
2. **Roslyn** для генерации чистого C# кода
3. **Ручной парсинг XML-комментариев** из .xml файлов NuGet пакетов

**Преимущества:**
- System.Reflection встроен в .NET, не требует дополнительных зависимостей
- Roslyn позволяет генерировать синтаксически корректный C# код
- Хорошая поддержка XML-комментариев через `XmlDocumentationProvider`
- Гибкость и контроль над процессом
- Чистая генерация кода через Roslyn

**Структура:**
```
Disassembly.Tool/
  ├── Core/
  │   ├── SolutionAnalyzer.cs          // Парсинг .sln/.csproj через MSBuild API
  │   ├── NuGetPackageResolver.cs      // Поиск в NuGet кэше (~/.nuget/packages)
  │   ├── AssemblyReflector.cs         // Рефлексия сборок через System.Reflection
  │   └── XmlDocumentationReader.cs   // Чтение XML-комментариев
  ├── Filters/
  │   ├── MemberVisibilityFilter.cs   // Фильтр публичных/protected
  │   └── MemberTypeFilter.cs         // Что включать (классы, методы, свойства)
  ├── CodeGeneration/
  │   ├── RoslynCodeGenerator.cs      // Генерация через Roslyn SyntaxFactory
  │   ├── NamespaceOrganizer.cs       // Организация файлов по namespace
  │   ├── FileNameResolver.cs         // Разрешение имен файлов с учетом счетчика (TypeName.cs, TypeName1.cs, ...)
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
1. Загрузка сборки через `Assembly.LoadFrom()` или `Assembly.ReflectionOnlyLoadFrom()`
2. Извлечение всех типов (классы, интерфейсы, структуры, enum)
3. Фильтрация по видимости (public, protected)
4. Извлечение членов типов (методы, свойства, поля, события)
5. Обработка generic типов и методов
6. Обработка атрибутов (опционально)

**Технологии:**
- `System.Reflection` для анализа метаданных (встроен в .NET)
- `Assembly.LoadFrom()` для загрузки сборок
- `Type.GetMembers()` для извлечения членов типов

**Ключевые моменты:**
- Игнорировать private/internal члены
- Сохранять информацию о generic параметрах
- Сохранять информацию о параметрах методов
- Сохранять информацию о возвращаемых типах
- Обработка зависимостей сборок (может потребоваться загрузка зависимостей)

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
6. Обработка дублирующихся имен классов через счетчик (первое появление без индекса, последующие с индексом)

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
   - Первое появление класса: `TypeName.cs`
   - Второе появление (дубликат): `TypeName1.cs`
   - Третье появление: `TypeName2.cs`
   - И так далее (счетчик начинается с 1 для второго файла)
4. Копирование не-C# файлов (если есть)

**Пример структуры:**
```
NugetDisassembly/
  └── Newtonsoft.Json/
      └── 13.0.4/
          └── Newtonsoft/
              └── Json/
                  ├── JsonConverter.cs
                  ├── JsonConverter1.cs
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

## Интерфейс инструмента

**CLI инструмент**

```bash
dotnet-disassembly [options]

Options:
  --solution <path>     Путь к .sln файлу
  --output <path>       Путь для вывода (по умолчанию: ./NugetDisassembly)
  --packages <list>     Список конкретных пакетов (опционально)
  --include-private     Включить private члены (не рекомендуется)
  --include-implementation  Включить реализации методов
```

## Порядок реализации

1. Реализовать CLI инструмент
2. Реализовать базовый функционал для одного пакета
3. Добавить обработку всего решения
4. Добавить обработку edge cases
5. Оптимизировать и добавить опции

---

## Зависимости (предварительно)

```xml
<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" />
<PackageReference Include="Microsoft.Build" Version="17.8.0" />
<PackageReference Include="System.Xml.ReaderWriter" Version="4.3.1" />
```

**Примечание:** System.Reflection встроен в .NET, дополнительных зависимостей не требуется.

---

## Вопросы для уточнения

1. Нужно ли включать атрибуты в генерируемый код?
2. Как обрабатывать вложенные типы (nested types)?
3. Нужна ли поддержка старых форматов проектов (.csproj с packages.config)?
4. Нужна ли поддержка .NET Framework библиотек?
5. ~~Как обрабатывать конфликты имен файлов (кроме generic)?~~ Решено: использование счетчика (TypeName.cs, TypeName1.cs, TypeName2.cs, ...)
6. Нужна ли поддержка F# или VB.NET библиотек?

