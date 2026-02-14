using System.Reflection;
using System.Runtime.Loader;
using Disassembly.Tool.Filters;

namespace Disassembly.Tool.Core;

/// <summary>
/// Метаданные типа
/// </summary>
public record TypeMetadata(
    string Name,
    string? Namespace,
    TypeKind Kind,
    bool IsGeneric,
    List<string> GenericParameters,
    List<MemberMetadata> Members,
    List<TypeMetadata> NestedTypes,
    Type? OriginalType,
    List<AttributeMetadata> Attributes
);

/// <summary>
/// Вид типа
/// </summary>
public enum TypeKind
{
    Class,
    Interface,
    Struct,
    Enum,
    Delegate
}

/// <summary>
/// Метаданные члена
/// </summary>
public record MemberMetadata(
    string Name,
    MemberType Type,
    string Signature,
    Type? ReturnType,
    List<ParameterMetadata> Parameters,
    MemberInfo? OriginalMember,
    List<AttributeMetadata> Attributes
);

/// <summary>
/// Тип члена
/// </summary>
public enum MemberType
{
    Method,
    Property,
    Field,
    Event,
    Constructor
}

/// <summary>
/// Метаданные параметра
/// </summary>
public record ParameterMetadata(
    string Name,
    string TypeName,
    bool IsOptional,
    object? DefaultValue
);

/// <summary>
/// Метаданные атрибута
/// </summary>
public record AttributeMetadata(
    string FullTypeName,
    List<AttributeArgumentMetadata> Arguments
);

/// <summary>
/// Метаданные аргумента атрибута
/// </summary>
public record AttributeArgumentMetadata(
    string? Name,
    string Value
);

/// <summary>
/// Рефлектор для извлечения метаданных из сборок
/// </summary>
public class AssemblyReflector
{
    private readonly AssemblyLoadContext _loadContext;

    public AssemblyReflector()
    {
        _loadContext = new AssemblyLoadContext("DisassemblyContext", isCollectible: true);
    }

    /// <summary>
    /// Читает метаданные из сборки
    /// </summary>
    public List<TypeMetadata> ReadAssembly(string dllPath)
    {
        if (!File.Exists(dllPath))
            throw new FileNotFoundException($"Assembly not found: {dllPath}");

        try
        {
            var assembly = _loadContext.LoadFromAssemblyPath(dllPath);
            return ExtractTypes(assembly);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load assembly {dllPath}: {ex.Message}", ex);
        }
    }

    private List<TypeMetadata> ExtractTypes(Assembly assembly)
    {
        var types = new List<TypeMetadata>();

        foreach (var type in assembly.GetTypes())
        {
            // Пропускаем компилятор-генерированные типы
            if (type.GetCustomAttributes(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false).Length > 0)
                continue;

            // Фильтруем только публичные и protected типы
            if (!MemberVisibilityFilter.IsPublicOrProtected(type))
                continue;

            var typeMetadata = ExtractTypeMetadata(type);
            if (typeMetadata != null)
            {
                types.Add(typeMetadata);
            }
        }

        return types;
    }

    private TypeMetadata? ExtractTypeMetadata(Type type)
    {
        var kind = GetTypeKind(type);
        var isGeneric = type.IsGenericTypeDefinition;
        var genericParameters = new List<string>();

        if (isGeneric)
        {
            genericParameters = type.GetGenericArguments()
                .Select(t => t.Name)
                .ToList();
        }

        var members = ExtractMembers(type);
        var nestedTypes = ExtractNestedTypes(type);
        var attributes = ExtractAttributes(type);

        return new TypeMetadata(
            GetTypeName(type),
            type.Namespace,
            kind,
            isGeneric,
            genericParameters,
            members,
            nestedTypes,
            type,
            attributes
        );
    }

    private TypeKind GetTypeKind(Type type)
    {
        if (type.IsClass)
        {
            if (type.IsSubclassOf(typeof(Delegate)))
                return TypeKind.Delegate;
            return TypeKind.Class;
        }
        if (type.IsInterface)
            return TypeKind.Interface;
        if (type.IsValueType && !type.IsEnum)
            return TypeKind.Struct;
        if (type.IsEnum)
            return TypeKind.Enum;

        return TypeKind.Class;
    }

    private string GetTypeName(Type type)
    {
        if (type.IsGenericTypeDefinition)
        {
            var name = type.Name;
            var backtickIndex = name.IndexOf('`');
            if (backtickIndex > 0)
            {
                name = name.Substring(0, backtickIndex);
            }
            return name;
        }

        return type.Name;
    }

    private List<MemberMetadata> ExtractMembers(Type type)
    {
        var members = new List<MemberMetadata>();

        // Методы
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.GetCustomAttributes(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false).Length == 0)
            .ToList();

        foreach (var method in methods)
        {
            if (MemberVisibilityFilter.IsPublicOrProtected(method))
            {
                var metadata = ExtractMethodMetadata(method);
                if (metadata != null)
                {
                    members.Add(metadata);
                }
            }
        }

        // Свойства
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        foreach (var property in properties)
        {
            if (MemberVisibilityFilter.IsPublicOrProtected(property))
            {
                var metadata = ExtractPropertyMetadata(property);
                if (metadata != null)
                {
                    members.Add(metadata);
                }
            }
        }

        // Поля
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        foreach (var field in fields)
        {
            if (MemberVisibilityFilter.IsPublicOrProtected(field))
            {
                var metadata = ExtractFieldMetadata(field);
                if (metadata != null)
                {
                    members.Add(metadata);
                }
            }
        }

        // События
        var events = type.GetEvents(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        foreach (var eventInfo in events)
        {
            if (MemberVisibilityFilter.IsPublicOrProtected(eventInfo))
            {
                var metadata = ExtractEventMetadata(eventInfo);
                if (metadata != null)
                {
                    members.Add(metadata);
                }
            }
        }

        // Конструкторы
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
        foreach (var constructor in constructors)
        {
            if (MemberVisibilityFilter.IsPublicOrProtected(constructor))
            {
                var metadata = ExtractConstructorMetadata(constructor);
                if (metadata != null)
                {
                    members.Add(metadata);
                }
            }
        }

        return members;
    }

    private MemberMetadata? ExtractMethodMetadata(MethodInfo method)
    {
        var parameters = method.GetParameters()
            .Select((p, index) => new ParameterMetadata(
                p.Name ?? "param",
                GetTypeDisplayName(p.ParameterType, isNullable: NullableReferenceTypeHelper.IsParameterNullable(p), member: method, genericArgumentIndex: index),
                p.IsOptional,
                p.HasDefaultValue ? p.DefaultValue : null
            ))
            .ToList();

        var signature = BuildMethodSignature(method, parameters);
        var attributes = ExtractAttributes(method);

        return new MemberMetadata(
            method.Name,
            MemberType.Method,
            signature,
            method.ReturnType,
            parameters,
            method,
            attributes
        );
    }

    private MemberMetadata? ExtractPropertyMetadata(PropertyInfo property)
    {
        var parameters = property.GetIndexParameters()
            .Select((p, index) => new ParameterMetadata(
                p.Name ?? "index",
                GetTypeDisplayName(p.ParameterType, isNullable: NullableReferenceTypeHelper.IsParameterNullable(p), member: property, genericArgumentIndex: index),
                p.IsOptional,
                p.HasDefaultValue ? p.DefaultValue : null
            ))
            .ToList();

        var signature = BuildPropertySignature(property, parameters);
        var attributes = ExtractAttributes(property);

        return new MemberMetadata(
            property.Name,
            MemberType.Property,
            signature,
            property.PropertyType,
            parameters,
            property,
            attributes
        );
    }

    private MemberMetadata? ExtractFieldMetadata(FieldInfo field)
    {
        var typeName = GetTypeDisplayName(field.FieldType, isNullable: NullableReferenceTypeHelper.IsFieldTypeNullable(field), member: field);
        var signature = $"{typeName} {field.Name}";
        var attributes = ExtractAttributes(field);

        return new MemberMetadata(
            field.Name,
            MemberType.Field,
            signature,
            field.FieldType,
            new List<ParameterMetadata>(),
            field,
            attributes
        );
    }

    private MemberMetadata? ExtractEventMetadata(EventInfo eventInfo)
    {
        var typeName = eventInfo.EventHandlerType != null 
            ? GetTypeDisplayName(eventInfo.EventHandlerType, isNullable: NullableReferenceTypeHelper.IsEventTypeNullable(eventInfo), member: eventInfo)
            : "object";
        var signature = $"event {typeName} {eventInfo.Name}";
        var attributes = ExtractAttributes(eventInfo);

        return new MemberMetadata(
            eventInfo.Name,
            MemberType.Event,
            signature,
            eventInfo.EventHandlerType,
            new List<ParameterMetadata>(),
            eventInfo,
            attributes
        );
    }

    private MemberMetadata? ExtractConstructorMetadata(ConstructorInfo constructor)
    {
        var parameters = constructor.GetParameters()
            .Select((p, index) => new ParameterMetadata(
                p.Name ?? "param",
                GetTypeDisplayName(p.ParameterType, isNullable: NullableReferenceTypeHelper.IsParameterNullable(p), member: constructor, genericArgumentIndex: index),
                p.IsOptional,
                p.HasDefaultValue ? p.DefaultValue : null
            ))
            .ToList();

        var signature = BuildConstructorSignature(constructor, parameters);
        var attributes = ExtractAttributes(constructor);

        return new MemberMetadata(
            constructor.Name,
            MemberType.Constructor,
            signature,
            null,
            parameters,
            constructor,
            attributes
        );
    }

    private List<TypeMetadata> ExtractNestedTypes(Type type)
    {
        var nestedTypes = type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Where(t => t.GetCustomAttributes(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false).Length == 0 && MemberVisibilityFilter.IsPublicOrProtected(t))
            .Select(ExtractTypeMetadata)
            .Where(t => t != null)
            .Cast<TypeMetadata>()
            .ToList();

        return nestedTypes;
    }

    private string BuildMethodSignature(MethodInfo method, List<ParameterMetadata> parameters)
    {
        var returnType = GetTypeDisplayName(method.ReturnType, isNullable: NullableReferenceTypeHelper.IsReturnTypeNullable(method), member: method);
        var paramStr = string.Join(", ", parameters.Select(p => $"{p.TypeName} {p.Name}"));
        return $"{returnType} {method.Name}({paramStr})";
    }

    private string BuildPropertySignature(PropertyInfo property, List<ParameterMetadata> parameters)
    {
        var typeName = GetTypeDisplayName(property.PropertyType, isNullable: NullableReferenceTypeHelper.IsPropertyTypeNullable(property), member: property);
        if (parameters.Count > 0)
        {
            var paramStr = string.Join(", ", parameters.Select(p => $"{p.TypeName} {p.Name}"));
            return $"{typeName} this[{paramStr}]";
        }
        return $"{typeName} {property.Name}";
    }

    private string BuildConstructorSignature(ConstructorInfo constructor, List<ParameterMetadata> parameters)
    {
        var paramStr = string.Join(", ", parameters.Select(p => $"{p.TypeName} {p.Name}"));
        return $"{constructor.DeclaringType?.Name}({paramStr})";
    }

    private string GetTypeDisplayName(Type type, bool isNullable = false, MemberInfo? member = null, int genericArgumentIndex = -1)
    {
        if (type.IsGenericParameter)
            return type.Name;

        if (type.IsArray)
        {
            var elementType = GetTypeDisplayName(type.GetElementType()!, isNullable: false, member: member);
            return $"{elementType}[]";
        }

        if (type.IsGenericType)
        {
            var name = type.Name;
            var backtickIndex = name.IndexOf('`');
            if (backtickIndex > 0)
            {
                name = name.Substring(0, backtickIndex);
            }

            var genericArgs = type.GetGenericArguments()
                .Select((arg, index) => GetTypeDisplayName(
                    arg, 
                    isNullable: member != null && NullableReferenceTypeHelper.IsGenericArgumentNullable(arg, index, member),
                    member: member,
                    genericArgumentIndex: index))
                .ToList();

            var result = $"{name}<{string.Join(", ", genericArgs)}>";
            
            // Добавляем ? для nullable reference types
            if (isNullable && !type.IsValueType)
            {
                result += "?";
            }
            
            return result;
        }

        if (type.IsByRef)
        {
            return GetTypeDisplayName(type.GetElementType()!, isNullable: false, member: member) + "&";
        }

        // Простые типы
        if (type == typeof(void))
            return "void";
        if (type == typeof(int))
            return "int";
        if (type == typeof(long))
            return "long";
        if (type == typeof(short))
            return "short";
        if (type == typeof(byte))
            return "byte";
        if (type == typeof(bool))
            return "bool";
        if (type == typeof(char))
            return "char";
        if (type == typeof(float))
            return "float";
        if (type == typeof(double))
            return "double";
        if (type == typeof(decimal))
            return "decimal";
        if (type == typeof(string))
            return isNullable ? "string?" : "string";
        if (type == typeof(object))
            return isNullable ? "object?" : "object";

        var resultName = type.Name;
        
        // Добавляем ? для nullable reference types
        if (isNullable && !type.IsValueType)
        {
            resultName += "?";
        }
        
        return resultName;
    }

    /// <summary>
    /// Извлекает атрибуты из типа или члена
    /// </summary>
    private List<AttributeMetadata> ExtractAttributes(ICustomAttributeProvider attributeProvider)
    {
        var attributes = new List<AttributeMetadata>();

        try
        {
            IList<CustomAttributeData> customAttributes;
            
            // Используем правильную перегрузку в зависимости от типа
            if (attributeProvider is Assembly assembly)
            {
                customAttributes = CustomAttributeData.GetCustomAttributes(assembly);
            }
            else if (attributeProvider is MemberInfo memberInfo)
            {
                customAttributes = CustomAttributeData.GetCustomAttributes(memberInfo);
            }
            else if (attributeProvider is ParameterInfo parameterInfo)
            {
                customAttributes = CustomAttributeData.GetCustomAttributes(parameterInfo);
            }
            else if (attributeProvider is Type type)
            {
                customAttributes = CustomAttributeData.GetCustomAttributes(type);
            }
            else
            {
                return attributes;
            }
            
            foreach (var attr in customAttributes)
            {
                // Пропускаем компилятор-генерированные атрибуты
                if (attr.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute")
                    continue;
                
                // Пропускаем NullableAttribute и NullableContextAttribute (они обрабатываются отдельно)
                if (attr.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute" ||
                    attr.AttributeType.FullName == "System.Runtime.CompilerServices.NullableContextAttribute")
                    continue;

                // Используем полное имя атрибута с namespace
                var fullTypeName = attr.AttributeType.FullName ?? attr.AttributeType.Name;
                
                // Извлекаем аргументы атрибута
                var arguments = new List<AttributeArgumentMetadata>();
                
                // Конструкторные аргументы (позиционные)
                foreach (var arg in attr.ConstructorArguments)
                {
                    var value = FormatAttributeArgumentValue(arg);
                    arguments.Add(new AttributeArgumentMetadata(null, value));
                }
                
                // Именованные аргументы (свойства и поля)
                foreach (var arg in attr.NamedArguments)
                {
                    var value = FormatAttributeArgumentValue(arg.TypedValue);
                    arguments.Add(new AttributeArgumentMetadata(arg.MemberName, value));
                }
                
                attributes.Add(new AttributeMetadata(fullTypeName, arguments));
            }
        }
        catch
        {
            // Игнорируем ошибки при извлечении атрибутов
        }

        return attributes;
    }

    /// <summary>
    /// Форматирует значение аргумента атрибута в строку
    /// </summary>
    private string FormatAttributeArgumentValue(CustomAttributeTypedArgument argument)
    {
        if (argument.Value == null)
            return "null";

        var valueType = argument.ArgumentType;

        // Обработка массивов
        if (valueType.IsArray)
        {
            if (argument.Value is System.Collections.ICollection collection)
            {
                var items = new List<string>();
                foreach (var item in collection)
                {
                    items.Add(FormatSingleValue(item));
                }
                return $"new {GetTypeDisplayName(valueType.GetElementType()!, false)}[] {{ {string.Join(", ", items)} }}";
            }
        }

        // Обработка типов (Type)
        if (valueType == typeof(Type) && argument.Value is Type typeValue)
        {
            return $"typeof({GetTypeDisplayName(typeValue, false)})";
        }

        // Обработка строк
        if (valueType == typeof(string))
        {
            return $"\"{argument.Value}\"";
        }

        // Обработка char
        if (valueType == typeof(char))
        {
            return $"'{argument.Value}'";
        }

        // Обработка enum
        if (valueType.IsEnum)
        {
            var enumValue = Enum.GetName(valueType, argument.Value);
            if (enumValue != null)
            {
                return $"{GetTypeDisplayName(valueType, false)}.{enumValue}";
            }
        }

        // Обработка bool
        if (valueType == typeof(bool))
        {
            return argument.Value.ToString()!.ToLower();
        }

        // Для остальных типов просто используем ToString()
        return argument.Value.ToString() ?? "null";
    }

    /// <summary>
    /// Форматирует одиночное значение для массива
    /// </summary>
    private string FormatSingleValue(object? value)
    {
        if (value == null)
            return "null";

        var valueType = value.GetType();

        if (valueType == typeof(string))
            return $"\"{value}\"";

        if (valueType == typeof(char))
            return $"'{value}'";

        if (valueType.IsEnum)
        {
            var enumValue = Enum.GetName(valueType, value);
            if (enumValue != null)
            {
                return $"{GetTypeDisplayName(valueType, false)}.{enumValue}";
            }
        }

        if (valueType == typeof(bool))
            return value.ToString()!.ToLower();

        return value.ToString() ?? "null";
    }

    public void Dispose()
    {
        _loadContext.Unload();
    }
}

