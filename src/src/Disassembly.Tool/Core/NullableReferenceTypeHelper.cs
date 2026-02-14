using System.Reflection;
using System.Linq;

namespace Disassembly.Tool.Core;

/// <summary>
/// Вспомогательный класс для определения nullable reference types из метаданных сборок
/// </summary>
internal static class NullableReferenceTypeHelper
{
    // NullableAttribute содержит массив байтов, где:
    // 0 = oblivious (не аннотировано)
    // 1 = not nullable
    // 2 = nullable
    private const byte NullableAttributeValue_Nullable = 2;
    private const string NullableAttributeFullName = "System.Runtime.CompilerServices.NullableAttribute";
    private const string NullableContextAttributeFullName = "System.Runtime.CompilerServices.NullableContextAttribute";

    /// <summary>
    /// Определяет, является ли параметр nullable reference type
    /// </summary>
    public static bool IsParameterNullable(ParameterInfo parameter)
    {
        if (!IsReferenceType(parameter.ParameterType))
            return false;

        // Проверяем NullableAttribute на параметре
        var nullableFlags = GetNullableFlags(parameter);
        if (nullableFlags != null && nullableFlags.Length > 0)
        {
            // Для параметров первый элемент массива соответствует самому параметру
            // Но если массив имеет только один элемент, он применяется ко всем
            if (nullableFlags.Length == 1)
            {
                return nullableFlags[0] == NullableAttributeValue_Nullable;
            }
            // Для методов: [0] = return type, [1] = первый параметр, [2] = второй параметр и т.д.
            // Но для параметров напрямую первый элемент - это сам параметр
            return nullableFlags[0] == NullableAttributeValue_Nullable;
        }

        // Проверяем NullableContextAttribute на методе или типе
        var context = GetNullableContext(parameter.Member);
        return context == NullableAttributeValue_Nullable;
    }

    /// <summary>
    /// Определяет, является ли возвращаемый тип метода nullable reference type
    /// </summary>
    public static bool IsReturnTypeNullable(MethodInfo method)
    {
        if (method.ReturnType == typeof(void))
            return false;

        if (!IsReferenceType(method.ReturnType))
            return false;

        // Проверяем NullableAttribute на ReturnParameter или методе
        byte[]? nullableFlags = null;
        
        var returnParameter = method.ReturnParameter;
        if (returnParameter != null)
        {
            nullableFlags = GetNullableFlags(returnParameter);
        }
        
        // Если не нашли на return parameter, проверяем на методе
        if (nullableFlags == null || nullableFlags.Length == 0)
        {
            nullableFlags = GetNullableFlags(method);
        }
        
        if (nullableFlags != null && nullableFlags.Length > 0)
        {
            // Для методов: [0] = return type, [1] = первый параметр, [2] = второй параметр и т.д.
            return nullableFlags[0] == NullableAttributeValue_Nullable;
        }

        // Проверяем NullableContextAttribute на методе или типе
        var context = GetNullableContext(method);
        return context == NullableAttributeValue_Nullable;
    }

    /// <summary>
    /// Определяет, является ли тип свойства nullable reference type
    /// </summary>
    public static bool IsPropertyTypeNullable(PropertyInfo property)
    {
        if (!IsReferenceType(property.PropertyType))
            return false;

        // Проверяем NullableAttribute на свойстве
        var nullableFlags = GetNullableFlags(property);
        if (nullableFlags != null && nullableFlags.Length > 0)
        {
            return nullableFlags[0] == NullableAttributeValue_Nullable;
        }

        // Проверяем NullableContextAttribute на типе
        var context = GetNullableContext(property.DeclaringType);
        return context == NullableAttributeValue_Nullable;
    }

    /// <summary>
    /// Определяет, является ли тип поля nullable reference type
    /// </summary>
    public static bool IsFieldTypeNullable(FieldInfo field)
    {
        if (!IsReferenceType(field.FieldType))
            return false;

        // Проверяем NullableAttribute на поле
        var nullableFlags = GetNullableFlags(field);
        if (nullableFlags != null && nullableFlags.Length > 0)
        {
            return nullableFlags[0] == NullableAttributeValue_Nullable;
        }

        // Проверяем NullableContextAttribute на типе
        var context = GetNullableContext(field.DeclaringType);
        return context == NullableAttributeValue_Nullable;
    }

    /// <summary>
    /// Определяет, является ли тип события nullable reference type
    /// </summary>
    public static bool IsEventTypeNullable(EventInfo eventInfo)
    {
        if (eventInfo.EventHandlerType == null)
            return false;

        if (!IsReferenceType(eventInfo.EventHandlerType))
            return false;

        // Проверяем NullableAttribute на событии
        var nullableFlags = GetNullableFlags(eventInfo);
        if (nullableFlags != null && nullableFlags.Length > 0)
        {
            return nullableFlags[0] == NullableAttributeValue_Nullable;
        }

        // Проверяем NullableContextAttribute на типе
        var context = GetNullableContext(eventInfo.DeclaringType);
        return context == NullableAttributeValue_Nullable;
    }

    /// <summary>
    /// Определяет, является ли тип generic аргумента nullable reference type
    /// </summary>
    public static bool IsGenericArgumentNullable(Type genericType, int genericArgumentIndex, MemberInfo? member = null)
    {
        if (!IsReferenceType(genericType))
            return false;

        // Проверяем NullableAttribute на члене или типе
        if (member != null)
        {
            var nullableFlags = GetNullableFlags(member);
            if (nullableFlags != null && nullableFlags.Length > 0)
            {
                // Для методов: [0] = return type, [1] = первый параметр, [2] = второй параметр и т.д.
                // Для generic аргументов нужно учитывать смещение
                var index = genericArgumentIndex + 1; // +1 потому что первый элемент - это возвращаемое значение
                if (index < nullableFlags.Length)
                {
                    return nullableFlags[index] == NullableAttributeValue_Nullable;
                }
                // Если индекс выходит за границы, используем последний элемент
                if (nullableFlags.Length > 0)
                {
                    return nullableFlags[nullableFlags.Length - 1] == NullableAttributeValue_Nullable;
                }
            }
        }

        // Проверяем NullableContextAttribute
        var context = member != null ? GetNullableContext(member) : GetNullableContext(genericType.DeclaringType);
        return context == NullableAttributeValue_Nullable;
    }

    /// <summary>
    /// Получает массив флагов nullable из CustomAttributeData
    /// </summary>
    private static byte[]? GetNullableFlags(ICustomAttributeProvider attributeProvider)
    {
        try
        {
            IList<CustomAttributeData> customAttributes;
            
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
            else
            {
                return null;
            }
            
            foreach (var attr in customAttributes)
            {
                if (attr.AttributeType.FullName == NullableAttributeFullName)
                {
                    if (attr.ConstructorArguments.Count > 0)
                    {
                        var arg = attr.ConstructorArguments[0];
                        if (arg.ArgumentType == typeof(byte))
                        {
                            // Один байт
                            return new[] { (byte)arg.Value! };
                        }
                        else if (arg.ArgumentType == typeof(byte[]))
                        {
                            // Массив байтов
                            var values = (System.Collections.IList)arg.Value!;
                            return values.Cast<byte>().ToArray();
                        }
                    }
                }
            }
        }
        catch
        {
            // Игнорируем ошибки при чтении атрибутов
        }

        return null;
    }

    private static byte GetNullableContext(MemberInfo? member)
    {
        if (member == null)
            return 0;

        // Проверяем NullableContextAttribute на члене
        var context = GetNullableContextFromAttribute(member);
        if (context.HasValue)
        {
            return context.Value;
        }

        // Проверяем на типе
        if (member.DeclaringType != null)
        {
            return GetNullableContext(member.DeclaringType);
        }

        return 0;
    }

    private static byte GetNullableContext(Type? type)
    {
        if (type == null)
            return 0;

        var context = GetNullableContextFromAttribute(type);
        if (context.HasValue)
        {
            return context.Value;
        }

        // Проверяем на родительском типе
        if (type.DeclaringType != null)
        {
            return GetNullableContext(type.DeclaringType);
        }

        return 0;
    }

    private static byte? GetNullableContextFromAttribute(ICustomAttributeProvider attributeProvider)
    {
        try
        {
            IList<CustomAttributeData> customAttributes;
            
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
            else
            {
                return null;
            }
            
            foreach (var attr in customAttributes)
            {
                if (attr.AttributeType.FullName == NullableContextAttributeFullName)
                {
                    if (attr.ConstructorArguments.Count > 0)
                    {
                        var arg = attr.ConstructorArguments[0];
                        if (arg.ArgumentType == typeof(byte))
                        {
                            return (byte)arg.Value!;
                        }
                    }
                }
            }
        }
        catch
        {
            // Игнорируем ошибки при чтении атрибутов
        }

        return null;
    }

    private static bool IsReferenceType(Type type)
    {
        // Проверяем, является ли тип reference type (не value type)
        // Исключаем уже nullable value types (например, int?)
        if (type.IsValueType)
        {
            // Nullable<T> - это value type, но мы его не обрабатываем как nullable reference type
            return false;
        }

        // Проверяем, что это не generic параметр без ограничений
        if (type.IsGenericParameter)
        {
            // Для generic параметров нужно проверять ограничения
            // Но для простоты считаем, что если это не value type, то может быть nullable
            return true;
        }

        return true;
    }
}


