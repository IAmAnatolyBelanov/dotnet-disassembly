# Инструкция по использованию dotnet-disassembly

## Описание

`dotnet-disassembly` — это инструмент командной строки для извлечения публичных интерфейсов из .NET библиотек (NuGet пакетов). Инструмент анализирует решение (.sln файл), находит все используемые NuGet пакеты, и генерирует C# файлы с публичными и protected типами, методами, свойствами и полями без реализации.

Этот инструмент полезен для:
- Анализа API библиотек
- Создания документации по публичным интерфейсам
- Помощи AI-агентам в работе с актуальными версиями библиотек
- Изучения структуры зависимостей проекта

## Требования

- .NET 10.0 или выше
- NuGet пакеты должны быть восстановлены в кэше NuGet (обычно находится в `~/.nuget/packages` на Linux/Mac или `%USERPROFILE%\.nuget\packages` на Windows)

## Установка

### Сборка из исходников

1. Клонируйте репозиторий:
```bash
git clone <repository-url>
cd dotnet-disassembly
```

2. Перейдите в директорию с проектом:
```bash
cd src/src/Disassembly.Tool
```

3. Соберите проект:
```bash
dotnet build -c Release
```

4. Запустите инструмент:
```bash
dotnet run -- --solution <путь-к-решению.sln>
```

Или используйте собранный исполняемый файл:
```bash
dotnet <путь-к-bin/Release/net10.0/Disassembly.Tool.dll> --solution <путь-к-решению.sln>
```

## Использование

### Базовый синтаксис

```bash
dotnet-disassembly --solution <путь-к-файлу.sln> [--output <путь-к-директории>]
```

### Параметры

- `--solution, -s <path>` — **Обязательный параметр**. Путь к файлу решения (.sln)
- `--output, -o <path>` — **Опциональный параметр**. Путь к директории для вывода результатов. По умолчанию: `./NugetDisassembly`
- `--help, -h` — Показать справку по использованию

### Примеры использования

#### Пример 1: Базовое использование

```bash
dotnet-disassembly --solution MySolution.sln
```

Эта команда:
- Проанализирует `MySolution.sln`
- Найдет все NuGet пакеты во всех проектах решения
- Создаст директорию `./NugetDisassembly` (если её нет)
- Сгенерирует C# файлы с интерфейсами всех пакетов

#### Пример 2: Указание выходной директории

```bash
dotnet-disassembly --solution MySolution.sln --output ./output/api-interfaces
```

Результаты будут сохранены в директорию `./output/api-interfaces` вместо стандартной `./NugetDisassembly`.

#### Пример 3: Использование коротких параметров

```bash
dotnet-disassembly -s MySolution.sln -o ./api
```

#### Пример 4: Просмотр справки

```bash
dotnet-disassembly --help
```

## Структура выходных данных

Инструмент создает следующую структуру директорий:

```
NugetDisassembly/
  └── <ИмяПакета>/
      └── <Версия>/
          └── <Namespace1>/
              └── <Namespace2>/
                  ├── TypeName.cs
                  ├── TypeName1.cs  (если есть дубликат имени)
                  └── SubNamespace/
                      └── AnotherType.cs
```

### Пример реальной структуры

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

### Особенности организации файлов

1. **Структура по пакетам и версиям**: Каждый пакет получает свою директорию с именем пакета, внутри которой создается поддиректория с версией.

2. **Организация по namespace**: Файлы организованы по namespace, создавая поддиректории для каждого уровня вложенности namespace.

3. **Обработка дубликатов имен**: Если в одном namespace есть несколько типов с одинаковым именем (например, из-за generic параметров), инструмент автоматически добавляет числовой суффикс:
   - Первое вхождение: `TypeName.cs`
   - Второе вхождение: `TypeName1.cs`
   - Третье вхождение: `TypeName2.cs`
   - И так далее

## Что извлекается

Инструмент извлекает следующие элементы из публичного API библиотек:

### Типы
- ✅ Классы (classes)
- ✅ Интерфейсы (interfaces)
- ✅ Структуры (structs)
- ✅ Перечисления (enums)
- ✅ Делегаты (delegates)
- ✅ Вложенные типы (nested types)

### Члены типов
- ✅ Методы (включая перегрузки)
- ✅ Свойства (properties)
- ✅ Поля (fields)
- ✅ События (events)
- ✅ Конструкторы
- ✅ Индексаторы (indexers)

### Модификаторы доступа
- ✅ `public` — извлекаются
- ✅ `protected` — извлекаются
- ❌ `private` — игнорируются
- ❌ `internal` — игнорируются

### Дополнительные возможности
- ✅ Generic типы и методы
- ✅ Параметры методов с типами
- ✅ Возвращаемые типы
- ✅ Базовые классы и интерфейсы
- ✅ Модификаторы: `static`, `abstract`, `virtual`, `sealed`
- ✅ XML комментарии (если доступны в .xml файлах документации)

## Формат генерируемого кода

### Пример класса

```csharp
namespace Newtonsoft.Json
{
    public abstract class JsonConverter
    {
        // Implementation in original library
        
        public abstract void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer);
        
        public abstract object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer);
        
        public abstract bool CanConvert(Type objectType);
    }
}
```

### Пример интерфейса

```csharp
namespace System.Collections.Generic
{
    public interface IEnumerable<out T> : IEnumerable
    {
        // Implementation in original library
        
        IEnumerator<T> GetEnumerator();
    }
}
```

### Пример enum

```csharp
namespace System
{
    public enum StringComparison
    {
        CurrentCulture = 0,
        CurrentCultureIgnoreCase = 1,
        InvariantCulture = 2,
        InvariantCultureIgnoreCase = 3,
        Ordinal = 4,
        OrdinalIgnoreCase = 5
    }
}
```

### Пример generic типа

```csharp
namespace System.Collections.Generic
{
    public class List<T> : IList<T>, ICollection<T>, IEnumerable<T>, IEnumerable, IList, ICollection, IReadOnlyList<T>, IReadOnlyCollection<T>
    {
        // Implementation in original library
        
        public List();
        
        public List(int capacity);
        
        public List(IEnumerable<T> collection);
        
        public int Capacity { get; set; }
        
        public int Count { get; }
        
        public T this[int index] { get; set; }
        
        public void Add(T item);
        
        public void AddRange(IEnumerable<T> collection);
        
        // ... и так далее
    }
}
```

## Обработка ошибок

Инструмент обрабатывает ошибки следующим образом:

1. **Ошибки при обработке пакета**: Если при обработке одного пакета возникает ошибка, инструмент выводит предупреждение и продолжает обработку остальных пакетов.

2. **Ошибки при генерации файла**: Если не удается сгенерировать код для конкретного типа, выводится предупреждение, но обработка продолжается.

3. **Критические ошибки**: Если возникает критическая ошибка (например, не найден файл решения), инструмент завершает работу с кодом возврата 1.

### Коды возврата

- `0` — успешное выполнение
- `1` — ошибка выполнения

## Ограничения и известные проблемы

### Что не извлекается

- ❌ Реализация методов (только сигнатуры)
- ❌ Private и internal члены
- ❌ Атрибуты (пока не реализовано)
- ❌ Partial классы обрабатываются как обычные классы

### Требования к NuGet кэшу

Инструмент ищет пакеты в стандартном NuGet кэше:
- **Linux/Mac**: `~/.nuget/packages`
- **Windows**: `%USERPROFILE%\.nuget\packages`

Убедитесь, что пакеты были восстановлены через `dotnet restore` или `nuget restore` перед использованием инструмента.

### Обработка нескольких версий одного пакета

Если в решении используются разные версии одного и того же пакета, инструмент обработает каждую версию отдельно и создаст для каждой версии свою директорию.

## Примеры сценариев использования

### Сценарий 1: Анализ зависимостей проекта

```bash
# Проанализировать все зависимости проекта
dotnet-disassembly --solution MyProject.sln --output ./api-docs

# Просмотреть структуру
tree ./api-docs
```

### Сценарий 2: Создание документации API

```bash
# Извлечь интерфейсы всех пакетов
dotnet-disassembly -s MySolution.sln -o ./documentation/api

# Использовать сгенерированные файлы для создания документации
```

### Сценарий 3: Интеграция в CI/CD

```bash
#!/bin/bash
# Скрипт для автоматического обновления API интерфейсов

dotnet restore MySolution.sln
dotnet-disassembly --solution MySolution.sln --output ./generated-api

# Коммит изменений (если нужно)
git add ./generated-api
git commit -m "Update API interfaces"
```

## Советы и рекомендации

1. **Восстановление пакетов**: Всегда выполняйте `dotnet restore` перед использованием инструмента, чтобы убедиться, что все пакеты находятся в кэше.

2. **Выходная директория**: Используйте отдельную директорию для выходных данных, чтобы не смешивать их с исходным кодом проекта.

3. **Версионирование**: Результаты зависят от версий пакетов в решении. При обновлении пакетов перезапустите инструмент.

4. **Большие решения**: Для решений с большим количеством пакетов процесс может занять некоторое время. Будьте терпеливы.

5. **Git ignore**: Рекомендуется добавить выходную директорию в `.gitignore`, если вы не планируете коммитить сгенерированные файлы.

## Устранение неполадок

### Проблема: "Package not found in NuGet cache"

**Решение**: Выполните восстановление пакетов:
```bash
dotnet restore <путь-к-решению.sln>
```

### Проблема: "Error loading assembly"

**Решение**: Убедитесь, что:
- Пакет совместим с вашей версией .NET
- DLL файл не поврежден
- У вас есть права на чтение файлов в NuGet кэше

### Проблема: "Solution file not found"

**Решение**: Проверьте путь к файлу решения. Используйте абсолютный путь, если относительный не работает:
```bash
dotnet-disassembly --solution /полный/путь/к/MySolution.sln
```

### Проблема: Пустые файлы или отсутствие типов

**Решение**: 
- Проверьте, что в пакете действительно есть публичные типы
- Убедитесь, что пакет не содержит только internal/private API
- Проверьте логи инструмента на наличие предупреждений

## Дополнительная информация

Для получения дополнительной информации о реализации инструмента см. файл `PLAN.md` в корне репозитория.

## Поддержка

При возникновении проблем или вопросов:
1. Проверьте раздел "Устранение неполадок" выше
2. Изучите логи выполнения инструмента
3. Создайте issue в репозитории проекта с описанием проблемы

