using System.Xml.Linq;
using System.Reflection;

namespace Disassembly.Tool.Core;

/// <summary>
/// Комментарии для типа
/// </summary>
public record TypeComments(
    string? Summary,
    string? Remarks,
    List<string> Examples
);

/// <summary>
/// Комментарии для члена
/// </summary>
public record MemberComments(
    string? Summary,
    string? Remarks,
    Dictionary<string, string> Parameters,
    string? Returns,
    Dictionary<string, string> Exceptions,
    List<string> Examples
);

/// <summary>
/// Читатель XML документации
/// </summary>
public class XmlDocumentationReader
{
    private readonly Dictionary<string, XElement> _documentation = new();

    /// <summary>
    /// Загружает XML документацию из файла
    /// </summary>
    public void LoadXmlDocumentation(string? xmlPath)
    {
        if (string.IsNullOrWhiteSpace(xmlPath) || !File.Exists(xmlPath))
            return;

        try
        {
            var doc = XDocument.Load(xmlPath);
            var root = doc.Root;
            if (root == null)
                return;

            var members = root.Element("members");
            if (members == null)
                return;

            foreach (var member in members.Elements("member"))
            {
                var nameAttr = member.Attribute("name");
                if (nameAttr != null && !string.IsNullOrWhiteSpace(nameAttr.Value))
                {
                    _documentation[nameAttr.Value] = member;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to load XML documentation from {xmlPath}: {ex.Message}");
        }
    }

    /// <summary>
    /// Получает комментарии для типа
    /// </summary>
    public TypeComments? GetTypeComments(Type type)
    {
        var xmlId = GenerateTypeXmlId(type);
        return GetTypeComments(xmlId);
    }

    /// <summary>
    /// Получает комментарии для типа по XML ID
    /// </summary>
    public TypeComments? GetTypeComments(string xmlId)
    {
        if (!_documentation.TryGetValue(xmlId, out var memberElement))
            return null;

        var summary = GetElementText(memberElement, "summary");
        var remarks = GetElementText(memberElement, "remarks");
        var examples = GetElementsText(memberElement, "example");

        if (summary == null && remarks == null && examples.Count == 0)
            return null;

        return new TypeComments(summary, remarks, examples);
    }

    /// <summary>
    /// Получает комментарии для члена
    /// </summary>
    public MemberComments? GetMemberComments(MemberInfo memberInfo)
    {
        var xmlId = GenerateMemberXmlId(memberInfo);
        return GetMemberComments(xmlId);
    }

    /// <summary>
    /// Получает комментарии для члена по XML ID
    /// </summary>
    public MemberComments? GetMemberComments(string xmlId)
    {
        if (!_documentation.TryGetValue(xmlId, out var memberElement))
            return null;

        var summary = GetElementText(memberElement, "summary");
        var remarks = GetElementText(memberElement, "remarks");
        var returns = GetElementText(memberElement, "returns");
        var examples = GetElementsText(memberElement, "example");

        var parameters = new Dictionary<string, string>();
        foreach (var param in memberElement.Elements("param"))
        {
            var nameAttr = param.Attribute("name");
            if (nameAttr != null && !string.IsNullOrWhiteSpace(nameAttr.Value))
            {
                var paramText = param.Value.Trim();
                if (!string.IsNullOrWhiteSpace(paramText))
                {
                    parameters[nameAttr.Value] = paramText;
                }
            }
        }

        var exceptions = new Dictionary<string, string>();
        foreach (var exception in memberElement.Elements("exception"))
        {
            var crefAttr = exception.Attribute("cref");
            if (crefAttr != null && !string.IsNullOrWhiteSpace(crefAttr.Value))
            {
                var exceptionType = crefAttr.Value.TrimStart('T', ':');
                var exceptionText = exception.Value.Trim();
                if (!string.IsNullOrWhiteSpace(exceptionText))
                {
                    exceptions[exceptionType] = exceptionText;
                }
            }
        }

        if (summary == null && remarks == null && returns == null && 
            parameters.Count == 0 && exceptions.Count == 0 && examples.Count == 0)
            return null;

        return new MemberComments(summary, remarks, parameters, returns, exceptions, examples);
    }

    /// <summary>
    /// Генерирует XML ID для типа
    /// </summary>
    public static string GenerateTypeXmlId(Type type)
    {
        var fullName = GetFullTypeName(type);
        return $"T:{fullName}";
    }

    /// <summary>
    /// Генерирует XML ID для члена
    /// </summary>
    public static string GenerateMemberXmlId(MemberInfo memberInfo)
    {
        return memberInfo switch
        {
            MethodInfo method => GenerateMethodXmlId(method),
            PropertyInfo property => GeneratePropertyXmlId(property),
            FieldInfo field => GenerateFieldXmlId(field),
            EventInfo eventInfo => GenerateEventXmlId(eventInfo),
            ConstructorInfo constructor => GenerateConstructorXmlId(constructor),
            _ => string.Empty
        };
    }

    private static string GenerateMethodXmlId(MethodInfo method)
    {
        var declaringType = method.DeclaringType;
        if (declaringType == null)
            return string.Empty;

        var fullTypeName = GetFullTypeName(declaringType);
        var methodName = method.Name;

        // Обработка явных реализаций интерфейсов
        if (methodName.Contains('.') && method.IsPrivate)
        {
            // Для явных реализаций используем полное имя
            methodName = methodName.Replace('.', '#');
        }

        // Обработка generic методов
        if (method.IsGenericMethodDefinition)
        {
            var genericParams = method.GetGenericArguments();
            if (genericParams.Length > 0)
            {
                methodName = $"{methodName}``{genericParams.Length}";
            }
        }

        var parameters = method.GetParameters();
        var paramTypes = parameters.Select(p => GetTypeNameForXml(p.ParameterType)).ToArray();
        var paramString = string.Join(",", paramTypes);

        return $"M:{fullTypeName}.{methodName}({paramString})";
    }

    private static string GeneratePropertyXmlId(PropertyInfo property)
    {
        var declaringType = property.DeclaringType;
        if (declaringType == null)
            return string.Empty;

        var fullTypeName = GetFullTypeName(declaringType);
        return $"P:{fullTypeName}.{property.Name}";
    }

    private static string GenerateFieldXmlId(FieldInfo field)
    {
        var declaringType = field.DeclaringType;
        if (declaringType == null)
            return string.Empty;

        var fullTypeName = GetFullTypeName(declaringType);
        return $"F:{fullTypeName}.{field.Name}";
    }

    private static string GenerateEventXmlId(EventInfo eventInfo)
    {
        var declaringType = eventInfo.DeclaringType;
        if (declaringType == null)
            return string.Empty;

        var fullTypeName = GetFullTypeName(declaringType);
        return $"E:{fullTypeName}.{eventInfo.Name}";
    }

    private static string GenerateConstructorXmlId(ConstructorInfo constructor)
    {
        var declaringType = constructor.DeclaringType;
        if (declaringType == null)
            return string.Empty;

        var fullTypeName = GetFullTypeName(declaringType);
        var parameters = constructor.GetParameters();
        var paramTypes = parameters.Select(p => GetTypeNameForXml(p.ParameterType)).ToArray();
        var paramString = string.Join(",", paramTypes);

        return $"M:{fullTypeName}.#ctor({paramString})";
    }

    private static string GetFullTypeName(Type type)
    {
        if (type.IsGenericTypeDefinition)
        {
            var name = type.Name;
            var backtickIndex = name.IndexOf('`');
            if (backtickIndex > 0)
            {
                name = name.Substring(0, backtickIndex);
            }

            var ns = type.Namespace ?? string.Empty;
            var fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";

            // Добавляем количество generic параметров
            var genericCount = type.GetGenericArguments().Length;
            return $"{fullName}`{genericCount}";
        }

        if (type.IsNested)
        {
            var declaringType = type.DeclaringType;
            if (declaringType != null)
            {
                var parentName = GetFullTypeName(declaringType);
                return $"{parentName}.{type.Name}";
            }
        }

        var namespaceName = type.Namespace ?? string.Empty;
        return string.IsNullOrEmpty(namespaceName) ? type.Name : $"{namespaceName}.{type.Name}";
    }

    private static string GetTypeNameForXml(Type type)
    {
        if (type.IsGenericParameter)
            return type.Name;

        if (type.IsArray)
        {
            var elementType = GetTypeNameForXml(type.GetElementType()!);
            var rank = type.GetArrayRank();
            if (rank == 1)
                return $"{elementType}[]";
            return $"{elementType}[{new string(',', rank - 1)}]";
        }

        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            var name = genericTypeDef.Name;
            var backtickIndex = name.IndexOf('`');
            if (backtickIndex > 0)
            {
                name = name.Substring(0, backtickIndex);
            }

            var ns = genericTypeDef.Namespace ?? string.Empty;
            var fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";

            var genericArgs = type.GetGenericArguments()
                .Select(GetTypeNameForXml)
                .ToArray();

            return $"{fullName}{{{string.Join(",", genericArgs)}}}";
        }

        if (type.IsByRef)
        {
            return GetTypeNameForXml(type.GetElementType()!) + "@";
        }

        // Простые типы
        var typeName = type.FullName ?? type.Name;
        
        // Заменяем специальные символы для XML ID
        typeName = typeName.Replace('+', '.'); // Nested types
        
        return typeName;
    }

    private static string? GetElementText(XElement parent, string elementName)
    {
        var element = parent.Element(elementName);
        if (element == null)
            return null;

        var text = element.Value.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static List<string> GetElementsText(XElement parent, string elementName)
    {
        var elements = parent.Elements(elementName)
            .Select(e => e.Value.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        return elements;
    }
}

