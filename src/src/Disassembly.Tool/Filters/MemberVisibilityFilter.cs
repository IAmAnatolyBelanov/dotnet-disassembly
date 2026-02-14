using System.Reflection;

namespace Disassembly.Tool.Filters;

/// <summary>
/// Фильтр для проверки видимости членов (public/protected)
/// </summary>
public static class MemberVisibilityFilter
{
    /// <summary>
    /// Проверяет, является ли тип публичным или protected
    /// </summary>
    public static bool IsPublicOrProtected(Type type)
    {
        if (type.IsPublic)
            return true;

        if (type.IsNested)
        {
            return type.IsNestedPublic || type.IsNestedFamily || type.IsNestedFamORAssem;
        }

        return false;
    }

    /// <summary>
    /// Проверяет, является ли член публичным или protected
    /// </summary>
    public static bool IsPublicOrProtected(MemberInfo member)
    {
        return member switch
        {
            MethodInfo method => IsPublicOrProtectedMethod(method),
            PropertyInfo property => IsPublicOrProtectedProperty(property),
            FieldInfo field => IsPublicOrProtectedField(field),
            EventInfo eventInfo => IsPublicOrProtectedEvent(eventInfo),
            ConstructorInfo constructor => IsPublicOrProtectedConstructor(constructor),
            Type type => IsPublicOrProtected(type),
            _ => false
        };
    }

    private static bool IsPublicOrProtectedMethod(MethodInfo method)
    {
        if (method.IsPublic)
            return true;

        if (method.IsFamily || method.IsFamilyOrAssembly)
            return true;

        return false;
    }

    private static bool IsPublicOrProtectedProperty(PropertyInfo property)
    {
        var getter = property.GetGetMethod(true);
        var setter = property.GetSetMethod(true);

        if (getter != null && IsPublicOrProtectedMethod(getter))
            return true;

        if (setter != null && IsPublicOrProtectedMethod(setter))
            return true;

        return false;
    }

    private static bool IsPublicOrProtectedField(FieldInfo field)
    {
        if (field.IsPublic)
            return true;

        if (field.IsFamily || field.IsFamilyOrAssembly)
            return true;

        return false;
    }

    private static bool IsPublicOrProtectedEvent(EventInfo eventInfo)
    {
        var addMethod = eventInfo.GetAddMethod(true);
        var removeMethod = eventInfo.GetRemoveMethod(true);

        if (addMethod != null && IsPublicOrProtectedMethod(addMethod))
            return true;

        if (removeMethod != null && IsPublicOrProtectedMethod(removeMethod))
            return true;

        return false;
    }

    private static bool IsPublicOrProtectedConstructor(ConstructorInfo constructor)
    {
        if (constructor.IsPublic)
            return true;

        if (constructor.IsFamily || constructor.IsFamilyOrAssembly)
            return true;

        return false;
    }

    /// <summary>
    /// Фильтрует типы, оставляя только публичные и protected
    /// </summary>
    public static IEnumerable<Type> FilterTypes(IEnumerable<Type> types)
    {
        return types.Where(IsPublicOrProtected);
    }

    /// <summary>
    /// Фильтрует члены, оставляя только публичные и protected
    /// </summary>
    public static IEnumerable<MemberInfo> FilterMembers(IEnumerable<MemberInfo> members)
    {
        return members.Where(IsPublicOrProtected);
    }
}

