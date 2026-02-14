using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Disassembly.Tool.Core;
using Disassembly.Tool.Filters;

namespace Disassembly.Tool.CodeGeneration;

/// <summary>
/// Генератор C# кода через Roslyn
/// </summary>
public class RoslynCodeGenerator
{
    private readonly Dictionary<string, TypeComments>? _typeComments;
    private readonly Dictionary<string, MemberComments>? _memberComments;

    public RoslynCodeGenerator(
        Dictionary<string, TypeComments>? typeComments = null,
        Dictionary<string, MemberComments>? memberComments = null)
    {
        _typeComments = typeComments;
        _memberComments = memberComments;
    }

    /// <summary>
    /// Генерирует объявление типа
    /// </summary>
    public MemberDeclarationSyntax GenerateTypeDeclaration(TypeMetadata typeMetadata)
    {
        var modifiers = GetTypeModifiers(typeMetadata);
        var name = typeMetadata.Name;

        if (typeMetadata.Kind == Core.TypeKind.Enum)
        {
            var enumDecl = SyntaxFactory.EnumDeclaration(name)
                .WithModifiers(modifiers);
            
            // Добавляем базовый тип enum (например, : int, : byte)
            if (typeMetadata.OriginalType != null && typeMetadata.OriginalType.IsEnum)
            {
                var underlyingType = Enum.GetUnderlyingType(typeMetadata.OriginalType);
                if (underlyingType != typeof(int)) // int - это тип по умолчанию
                {
                    var baseTypeSyntax = SyntaxFactory.SimpleBaseType(
                        SyntaxFactory.ParseTypeName(GetTypeDisplayName(underlyingType))
                    );
                    enumDecl = enumDecl.WithBaseList(
                        SyntaxFactory.BaseList(SyntaxFactory.SingletonSeparatedList<BaseTypeSyntax>(baseTypeSyntax))
                    );
                }
            }
            
            // Генерируем enum members из полей
            var enumMembers = GenerateEnumMembers(typeMetadata);
            if (enumMembers.Count > 0)
            {
                enumDecl = enumDecl.WithMembers(SyntaxFactory.SeparatedList(enumMembers));
            }
            
            // Добавляем XML комментарии для enum типа
            if (typeMetadata.OriginalType != null)
            {
                var xmlId = XmlDocumentationReader.GenerateTypeXmlId(typeMetadata.OriginalType);
                if (_typeComments != null && _typeComments.TryGetValue(xmlId, out var typeComments))
                {
                    enumDecl = enumDecl.WithLeadingTrivia(GenerateXmlCommentTrivia(typeComments));
                }
            }
            
            // Примечание: вложенные типы в enum не поддерживаются стандартным способом в C#
            // Если они есть в метаданных, они будут обработаны как отдельные типы верхнего уровня
            
            return enumDecl;
        }

        TypeDeclarationSyntax declaration = typeMetadata.Kind switch
        {
            Core.TypeKind.Class => SyntaxFactory.ClassDeclaration(name),
            Core.TypeKind.Interface => SyntaxFactory.InterfaceDeclaration(name),
            Core.TypeKind.Struct => SyntaxFactory.StructDeclaration(name),
            Core.TypeKind.Delegate => throw new NotSupportedException("Delegates are handled separately"),
            _ => SyntaxFactory.ClassDeclaration(name)
        };

        declaration = declaration
            .WithModifiers(modifiers)
            .WithTypeParameterList(GenerateTypeParameterList(typeMetadata));

        // Добавляем базовые типы и интерфейсы
        if (typeMetadata.OriginalType != null)
        {
            var baseTypes = GetBaseTypes(typeMetadata.OriginalType);
            if (baseTypes.Count > 0)
            {
                declaration = declaration.WithBaseList(
                    SyntaxFactory.BaseList(
                        SyntaxFactory.SeparatedList<BaseTypeSyntax>(
                            baseTypes.Select(t => SyntaxFactory.SimpleBaseType(
                                SyntaxFactory.ParseTypeName(GetTypeDisplayName(t))
                            ))
                        )
                    )
                );
            }
        }

        // Добавляем члены (для enum поля-значения обрабатываются отдельно)
        var members = new List<MemberDeclarationSyntax>();

        foreach (var member in typeMetadata.Members)
        {
            // Пропускаем поля enum, которые являются значениями enum
            if (typeMetadata.Kind == Core.TypeKind.Enum && 
                member.Type == MemberType.Field && 
                member.OriginalMember is System.Reflection.FieldInfo fieldInfo &&
                fieldInfo.IsStatic && fieldInfo.IsPublic && fieldInfo.Name != "value__")
            {
                continue; // Эти поля обрабатываются в GenerateEnumMembers
            }
            
            var memberSyntax = GenerateMember(member);
            if (memberSyntax != null)
            {
                members.Add(memberSyntax);
            }
        }

        // Добавляем вложенные типы
        foreach (var nestedType in typeMetadata.NestedTypes)
        {
            var nestedDeclaration = GenerateTypeDeclaration(nestedType);
            members.Add(nestedDeclaration);
        }

        declaration = declaration.WithMembers(SyntaxFactory.List(members));

        // Добавляем XML комментарии
        if (typeMetadata.OriginalType != null)
        {
            var xmlId = XmlDocumentationReader.GenerateTypeXmlId(typeMetadata.OriginalType);
            if (_typeComments != null && _typeComments.TryGetValue(xmlId, out var typeComments))
            {
                declaration = declaration.WithLeadingTrivia(GenerateXmlCommentTrivia(typeComments));
            }
        }

        return declaration;
    }

    /// <summary>
    /// Генерирует enum members из метаданных
    /// </summary>
    private List<EnumMemberDeclarationSyntax> GenerateEnumMembers(TypeMetadata typeMetadata)
    {
        var enumMembers = new List<EnumMemberDeclarationSyntax>();
        
        if (typeMetadata.OriginalType == null || !typeMetadata.OriginalType.IsEnum)
            return enumMembers;
        
        // Получаем все статические публичные поля, которые являются значениями enum
        var enumFields = typeMetadata.Members
            .Where(m => m.Type == MemberType.Field && m.OriginalMember is System.Reflection.FieldInfo fieldInfo)
            .Select(m => m.OriginalMember as System.Reflection.FieldInfo)
            .Where(f => f != null && f.IsStatic && f.IsPublic && f.Name != "value__")
            .OrderBy(f => 
            {
                var value = f!.GetRawConstantValue();
                if (value == null) return 0;
                try
                {
                    return Convert.ToInt64(value);
                }
                catch
                {
                    return 0;
                }
            })
            .ToList();
        
        foreach (var field in enumFields)
        {
            if (field == null) continue;
            
            var enumMember = SyntaxFactory.EnumMemberDeclaration(SyntaxFactory.Identifier(field.Name));
            
            // Получаем значение enum member
            var value = field.GetRawConstantValue();
            if (value != null)
            {
                var underlyingType = Enum.GetUnderlyingType(typeMetadata.OriginalType);
                var numericValue = Convert.ChangeType(value, underlyingType);
                
                // Всегда указываем явные значения для всех enum members
                // Генерируем выражение для значения
                ExpressionSyntax valueExpression = numericValue switch
                {
                    int intVal => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(intVal)),
                    byte byteVal => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(byteVal)),
                    sbyte sbyteVal => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(sbyteVal)),
                    short shortVal => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(shortVal)),
                    ushort ushortVal => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(ushortVal)),
                    uint uintVal => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(uintVal)),
                    long longVal => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(longVal)),
                    ulong ulongVal => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(ulongVal)),
                    _ => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(Convert.ToInt64(numericValue)))
                };
                
                enumMember = enumMember.WithEqualsValue(
                    SyntaxFactory.EqualsValueClause(valueExpression)
                );
            }
            
            // Добавляем XML комментарии для enum member
            // Enum members в XML документации имеют формат F:Namespace.EnumType.MemberName
            if (_memberComments != null)
            {
                var fieldXmlId = XmlDocumentationReader.GenerateMemberXmlId(field);
                if (_memberComments.TryGetValue(fieldXmlId, out var memberComment) && 
                    !string.IsNullOrWhiteSpace(memberComment.Summary))
                {
                    // Для enum members используем только Summary
                    var xmlBuilder = new System.Text.StringBuilder();
                    xmlBuilder.AppendLine("/// <summary>");
                    foreach (var line in memberComment.Summary.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
                    {
                        xmlBuilder.AppendLine($"/// {line}");
                    }
                    xmlBuilder.AppendLine("/// </summary>");
                    
                    var xmlText = xmlBuilder.ToString();
                    var trivia = SyntaxFactory.ParseLeadingTrivia(xmlText);
                    enumMember = enumMember.WithLeadingTrivia(trivia);
                }
            }
            
            enumMembers.Add(enumMember);
        }
        
        return enumMembers;
    }

    /// <summary>
    /// Генерирует член типа
    /// </summary>
    public MemberDeclarationSyntax? GenerateMember(MemberMetadata memberMetadata)
    {
        MemberDeclarationSyntax? result = memberMetadata.Type switch
        {
            MemberType.Method => GenerateMethod(memberMetadata),
            MemberType.Property => GenerateProperty(memberMetadata),
            MemberType.Field => GenerateField(memberMetadata),
            MemberType.Event => GenerateEvent(memberMetadata),
            MemberType.Constructor => GenerateConstructor(memberMetadata),
            _ => null
        };

        // Добавляем XML комментарии
        if (result != null && memberMetadata.OriginalMember != null)
        {
            var xmlId = XmlDocumentationReader.GenerateMemberXmlId(memberMetadata.OriginalMember);
            if (_memberComments != null && _memberComments.TryGetValue(xmlId, out var memberComments))
            {
                result = result.WithLeadingTrivia(GenerateXmlCommentTrivia(memberComments, memberMetadata));
            }
        }

        return result;
    }

    private MethodDeclarationSyntax GenerateMethod(MemberMetadata memberMetadata)
    {
        if (memberMetadata.OriginalMember is not System.Reflection.MethodInfo methodInfo)
            throw new InvalidOperationException("Method metadata must have OriginalMember");

        var returnType = SyntaxFactory.ParseTypeName(memberMetadata.ReturnType != null 
            ? GetTypeDisplayName(memberMetadata.ReturnType) 
            : "void");

        var method = SyntaxFactory.MethodDeclaration(returnType, memberMetadata.Name)
            .WithModifiers(GetMemberModifiers(methodInfo))
            .WithParameterList(GenerateParameterList(memberMetadata.Parameters))
            .WithBody(GenerateMethodBody());

        if (methodInfo.IsGenericMethodDefinition)
        {
            var typeParams = methodInfo.GetGenericArguments()
                .Select(t => SyntaxFactory.TypeParameter(t.Name))
                .ToArray();
            method = method.WithTypeParameterList(
                SyntaxFactory.TypeParameterList(
                    SyntaxFactory.SeparatedList(typeParams)
                )
            );
        }

        return method;
    }

    private MemberDeclarationSyntax GenerateProperty(MemberMetadata memberMetadata)
    {
        if (memberMetadata.OriginalMember is not System.Reflection.PropertyInfo propertyInfo)
            throw new InvalidOperationException("Property metadata must have OriginalMember");

        var type = SyntaxFactory.ParseTypeName(GetTypeDisplayName(propertyInfo.PropertyType));

        // Генерируем аксессоры
        var accessors = new List<AccessorDeclarationSyntax>();

        if (propertyInfo.CanRead)
        {
            var getter = propertyInfo.GetGetMethod(true);
            if (getter != null && MemberVisibilityFilter.IsPublicOrProtected(getter))
            {
                accessors.Add(SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithModifiers(GetAccessorModifiers(getter))
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
            }
        }

        if (propertyInfo.CanWrite)
        {
            var setter = propertyInfo.GetSetMethod(true);
            if (setter != null && MemberVisibilityFilter.IsPublicOrProtected(setter))
            {
                accessors.Add(SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithModifiers(GetAccessorModifiers(setter))
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
            }
        }

        if (memberMetadata.Parameters.Count > 0)
        {
            // Индексатор
            var paramList = GenerateParameterList(memberMetadata.Parameters);
            var bracketedParams = SyntaxFactory.BracketedParameterList(
                SyntaxFactory.SeparatedList(paramList.Parameters)
            );
            var indexer = SyntaxFactory.IndexerDeclaration(type)
                .WithModifiers(GetMemberModifiers(propertyInfo))
                .WithParameterList(bracketedParams)
                .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(accessors)));
            return indexer;
        }

        // Обычное свойство
        var property = SyntaxFactory.PropertyDeclaration(type, propertyInfo.Name)
            .WithModifiers(GetMemberModifiers(propertyInfo))
            .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(accessors)));

        return property;
    }

    private FieldDeclarationSyntax GenerateField(MemberMetadata memberMetadata)
    {
        if (memberMetadata.OriginalMember is not System.Reflection.FieldInfo fieldInfo)
            throw new InvalidOperationException("Field metadata must have OriginalMember");

        // Пропускаем поля enum, которые являются значениями enum (они обрабатываются отдельно)
        if (fieldInfo.DeclaringType != null && fieldInfo.DeclaringType.IsEnum && 
            fieldInfo.IsStatic && fieldInfo.IsPublic && fieldInfo.Name != "value__")
        {
            throw new InvalidOperationException("Enum fields should be handled by GenerateEnumMembers");
        }

        var type = SyntaxFactory.ParseTypeName(GetTypeDisplayName(fieldInfo.FieldType));
        var variable = SyntaxFactory.VariableDeclarator(fieldInfo.Name);
        var declaration = SyntaxFactory.VariableDeclaration(type)
            .WithVariables(SyntaxFactory.SingletonSeparatedList(variable));

        return SyntaxFactory.FieldDeclaration(declaration)
            .WithModifiers(GetMemberModifiers(fieldInfo));
    }

    private EventFieldDeclarationSyntax GenerateEvent(MemberMetadata memberMetadata)
    {
        if (memberMetadata.OriginalMember is not System.Reflection.EventInfo eventInfo)
            throw new InvalidOperationException("Event metadata must have OriginalMember");

        var type = SyntaxFactory.ParseTypeName(GetTypeDisplayName(eventInfo.EventHandlerType!));
        var variable = SyntaxFactory.VariableDeclarator(eventInfo.Name);
        var declaration = SyntaxFactory.VariableDeclaration(type)
            .WithVariables(SyntaxFactory.SingletonSeparatedList(variable));

        return SyntaxFactory.EventFieldDeclaration(declaration)
            .WithModifiers(GetMemberModifiers(eventInfo));
    }

    private ConstructorDeclarationSyntax GenerateConstructor(MemberMetadata memberMetadata)
    {
        if (memberMetadata.OriginalMember is not System.Reflection.ConstructorInfo constructorInfo)
            throw new InvalidOperationException("Constructor metadata must have OriginalMember");

        var name = constructorInfo.DeclaringType?.Name ?? "Unknown";

        return SyntaxFactory.ConstructorDeclaration(name)
            .WithModifiers(GetMemberModifiers(constructorInfo))
            .WithParameterList(GenerateParameterList(memberMetadata.Parameters))
            .WithBody(GenerateMethodBody());
    }

    private BlockSyntax GenerateMethodBody()
    {
        // Создаем пустой блок с комментарием
        return SyntaxFactory.Block()
            .WithLeadingTrivia(SyntaxFactory.TriviaList(
                SyntaxFactory.Comment("// Implementation in original library"),
                SyntaxFactory.EndOfLine("\n")
            ));
    }

    private ParameterListSyntax GenerateParameterList(List<ParameterMetadata> parameters)
    {
        var paramList = parameters.Select(p =>
        {
            var param = SyntaxFactory.Parameter(SyntaxFactory.Identifier(p.Name))
                .WithType(SyntaxFactory.ParseTypeName(p.TypeName));

            if (p.IsOptional)
            {
                param = param.WithDefault(SyntaxFactory.EqualsValueClause(
                    SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)
                ));
            }

            return param;
        }).ToArray();

        return SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(paramList));
    }

    private TypeParameterListSyntax? GenerateTypeParameterList(TypeMetadata typeMetadata)
    {
        if (!typeMetadata.IsGeneric || typeMetadata.GenericParameters.Count == 0)
            return null;

        var typeParams = typeMetadata.GenericParameters
            .Select(p => SyntaxFactory.TypeParameter(SyntaxFactory.Identifier(p)))
            .ToArray();

        return SyntaxFactory.TypeParameterList(
            SyntaxFactory.SeparatedList(typeParams)
        );
    }

    private SyntaxTokenList GetTypeModifiers(TypeMetadata typeMetadata)
    {
        var modifiers = new List<SyntaxKind>();

        if (typeMetadata.OriginalType != null)
        {
            if (typeMetadata.OriginalType.IsPublic || (typeMetadata.OriginalType.IsNested && typeMetadata.OriginalType.IsNestedPublic))
                modifiers.Add(SyntaxKind.PublicKeyword);
            else if (typeMetadata.OriginalType.IsNestedFamily)
                modifiers.Add(SyntaxKind.ProtectedKeyword);

            if (typeMetadata.OriginalType.IsAbstract && !typeMetadata.OriginalType.IsInterface)
                modifiers.Add(SyntaxKind.AbstractKeyword);
            if (typeMetadata.OriginalType.IsSealed && !typeMetadata.OriginalType.IsValueType)
                modifiers.Add(SyntaxKind.SealedKeyword);
            // Статический класс - это класс, который одновременно abstract и sealed
            if (typeMetadata.OriginalType.IsAbstract && typeMetadata.OriginalType.IsSealed && typeMetadata.OriginalType.IsClass)
                modifiers.Add(SyntaxKind.StaticKeyword);
        }
        else
        {
            modifiers.Add(SyntaxKind.PublicKeyword);
        }

        return SyntaxFactory.TokenList(modifiers.Select(SyntaxFactory.Token));
    }

    private SyntaxTokenList GetMemberModifiers(System.Reflection.MemberInfo memberInfo)
    {
        var modifiers = new List<SyntaxKind>();

        if (memberInfo is System.Reflection.MethodBase methodBase)
        {
            if (methodBase.IsPublic)
                modifiers.Add(SyntaxKind.PublicKeyword);
            else if (methodBase.IsFamily)
                modifiers.Add(SyntaxKind.ProtectedKeyword);
            else if (methodBase.IsFamilyOrAssembly)
            {
                modifiers.Add(SyntaxKind.ProtectedKeyword);
                modifiers.Add(SyntaxKind.InternalKeyword);
            }

            if (methodBase.IsStatic)
                modifiers.Add(SyntaxKind.StaticKeyword);
            if (methodBase.IsAbstract)
                modifiers.Add(SyntaxKind.AbstractKeyword);
            if (methodBase.IsVirtual && !methodBase.IsAbstract)
                modifiers.Add(SyntaxKind.VirtualKeyword);
            if (methodBase.IsFinal)
                modifiers.Add(SyntaxKind.SealedKeyword);
        }
        else if (memberInfo is System.Reflection.FieldInfo fieldInfo)
        {
            if (fieldInfo.IsPublic)
                modifiers.Add(SyntaxKind.PublicKeyword);
            else if (fieldInfo.IsFamily)
                modifiers.Add(SyntaxKind.ProtectedKeyword);
            else if (fieldInfo.IsFamilyOrAssembly)
            {
                modifiers.Add(SyntaxKind.ProtectedKeyword);
                modifiers.Add(SyntaxKind.InternalKeyword);
            }

            if (fieldInfo.IsStatic)
                modifiers.Add(SyntaxKind.StaticKeyword);
            if (fieldInfo.IsInitOnly)
                modifiers.Add(SyntaxKind.ReadOnlyKeyword);
        }
        else if (memberInfo is System.Reflection.PropertyInfo propertyInfo)
        {
            var getter = propertyInfo.GetGetMethod(true);
            var setter = propertyInfo.GetSetMethod(true);
            var accessor = getter ?? setter;

            if (accessor != null)
            {
                if (accessor.IsPublic)
                    modifiers.Add(SyntaxKind.PublicKeyword);
                else if (accessor.IsFamily)
                    modifiers.Add(SyntaxKind.ProtectedKeyword);
                else if (accessor.IsFamilyOrAssembly)
                {
                    modifiers.Add(SyntaxKind.ProtectedKeyword);
                    modifiers.Add(SyntaxKind.InternalKeyword);
                }

                if (accessor.IsStatic)
                    modifiers.Add(SyntaxKind.StaticKeyword);
                if (accessor.IsAbstract)
                    modifiers.Add(SyntaxKind.AbstractKeyword);
                if (accessor.IsVirtual && !accessor.IsAbstract)
                    modifiers.Add(SyntaxKind.VirtualKeyword);
            }
        }
        else if (memberInfo is System.Reflection.EventInfo eventInfo)
        {
            var addMethod = eventInfo.GetAddMethod(true);
            if (addMethod != null)
            {
                if (addMethod.IsPublic)
                    modifiers.Add(SyntaxKind.PublicKeyword);
                else if (addMethod.IsFamily)
                    modifiers.Add(SyntaxKind.ProtectedKeyword);
                else if (addMethod.IsFamilyOrAssembly)
                {
                    modifiers.Add(SyntaxKind.ProtectedKeyword);
                    modifiers.Add(SyntaxKind.InternalKeyword);
                }

                if (addMethod.IsStatic)
                    modifiers.Add(SyntaxKind.StaticKeyword);
            }
        }

        return SyntaxFactory.TokenList(modifiers.Select(SyntaxFactory.Token));
    }

    private SyntaxTokenList GetAccessorModifiers(System.Reflection.MethodInfo methodInfo)
    {
        var modifiers = new List<SyntaxKind>();

        if (methodInfo.IsFamily)
            modifiers.Add(SyntaxKind.ProtectedKeyword);
        else if (methodInfo.IsFamilyOrAssembly)
        {
            modifiers.Add(SyntaxKind.ProtectedKeyword);
            modifiers.Add(SyntaxKind.InternalKeyword);
        }

        return SyntaxFactory.TokenList(modifiers.Select(SyntaxFactory.Token));
    }

    private List<Type> GetBaseTypes(Type type)
    {
        var baseTypes = new List<Type>();

        if (type.BaseType != null && type.BaseType != typeof(object) && type.BaseType != typeof(ValueType))
        {
            baseTypes.Add(type.BaseType);
        }

        baseTypes.AddRange(type.GetInterfaces());

        return baseTypes;
    }

    private string GetTypeDisplayName(Type type)
    {
        if (type.IsGenericParameter)
            return type.Name;

        if (type.IsArray)
        {
            var elementType = GetTypeDisplayName(type.GetElementType()!);
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

            // Для отображения используем namespace если он есть
            if (!string.IsNullOrEmpty(type.Namespace))
            {
                name = $"{type.Namespace}.{name}";
            }

            var genericArgs = type.GetGenericArguments()
                .Select(GetTypeDisplayName)
                .ToList();

            return $"{name}<{string.Join(", ", genericArgs)}>";
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
            return "string";
        if (type == typeof(object))
            return "object";

        // Используем полное имя с namespace
        if (!string.IsNullOrEmpty(type.Namespace))
        {
            return $"{type.Namespace}.{type.Name}";
        }

        return type.Name;
    }

    /// <summary>
    /// Генерирует полный файл с namespace и using
    /// </summary>
    public CompilationUnitSyntax GenerateFile(TypeMetadata typeMetadata)
    {
        var typeDeclaration = GenerateTypeDeclaration(typeMetadata);
        
        var namespaceName = typeMetadata.Namespace ?? "Global";
        var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(
            SyntaxFactory.ParseName(namespaceName)
        ).WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(typeDeclaration));

        var compilationUnit = SyntaxFactory.CompilationUnit()
            .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(namespaceDeclaration));

        return compilationUnit;
    }

    /// <summary>
    /// Генерирует XML комментарии для типа
    /// </summary>
    private SyntaxTriviaList GenerateXmlCommentTrivia(TypeComments comments)
    {
        var xmlBuilder = new System.Text.StringBuilder();
        xmlBuilder.AppendLine("/// <summary>");
        
        if (!string.IsNullOrWhiteSpace(comments.Summary))
        {
            foreach (var line in comments.Summary.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
            {
                xmlBuilder.AppendLine($"/// {line}");
            }
        }
        
        xmlBuilder.AppendLine("/// </summary>");

        if (!string.IsNullOrWhiteSpace(comments.Remarks))
        {
            xmlBuilder.AppendLine("/// <remarks>");
            foreach (var line in comments.Remarks.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
            {
                xmlBuilder.AppendLine($"/// {line}");
            }
            xmlBuilder.AppendLine("/// </remarks>");
        }

        foreach (var example in comments.Examples)
        {
            if (!string.IsNullOrWhiteSpace(example))
            {
                xmlBuilder.AppendLine("/// <example>");
                foreach (var line in example.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
                {
                    xmlBuilder.AppendLine($"/// {line}");
                }
                xmlBuilder.AppendLine("/// </example>");
            }
        }

        // Парсим XML комментарии из строки
        var xmlText = xmlBuilder.ToString();
        var trivia = SyntaxFactory.ParseLeadingTrivia(xmlText);
        return trivia;
    }

    /// <summary>
    /// Генерирует XML комментарии для члена
    /// </summary>
    private SyntaxTriviaList GenerateXmlCommentTrivia(MemberComments comments, MemberMetadata memberMetadata)
    {
        var xmlBuilder = new System.Text.StringBuilder();
        xmlBuilder.AppendLine("/// <summary>");
        
        if (!string.IsNullOrWhiteSpace(comments.Summary))
        {
            foreach (var line in comments.Summary.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
            {
                xmlBuilder.AppendLine($"/// {line}");
            }
        }
        
        xmlBuilder.AppendLine("/// </summary>");

        // Добавляем param комментарии
        foreach (var param in memberMetadata.Parameters)
        {
            if (comments.Parameters.TryGetValue(param.Name, out var paramComment))
            {
                xmlBuilder.AppendLine($"/// <param name=\"{param.Name}\">");
                foreach (var line in paramComment.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
                {
                    xmlBuilder.AppendLine($"/// {line}");
                }
                xmlBuilder.AppendLine("/// </param>");
            }
        }

        if (!string.IsNullOrWhiteSpace(comments.Returns))
        {
            xmlBuilder.AppendLine("/// <returns>");
            foreach (var line in comments.Returns.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
            {
                xmlBuilder.AppendLine($"/// {line}");
            }
            xmlBuilder.AppendLine("/// </returns>");
        }

        // Добавляем exception комментарии
        foreach (var exception in comments.Exceptions)
        {
            xmlBuilder.AppendLine($"/// <exception cref=\"{exception.Key}\">");
            foreach (var line in exception.Value.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
            {
                xmlBuilder.AppendLine($"/// {line}");
            }
            xmlBuilder.AppendLine("/// </exception>");
        }

        if (!string.IsNullOrWhiteSpace(comments.Remarks))
        {
            xmlBuilder.AppendLine("/// <remarks>");
            foreach (var line in comments.Remarks.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
            {
                xmlBuilder.AppendLine($"/// {line}");
            }
            xmlBuilder.AppendLine("/// </remarks>");
        }

        foreach (var example in comments.Examples)
        {
            if (!string.IsNullOrWhiteSpace(example))
            {
                xmlBuilder.AppendLine("/// <example>");
                foreach (var line in example.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
                {
                    xmlBuilder.AppendLine($"/// {line}");
                }
                xmlBuilder.AppendLine("/// </example>");
            }
        }

        // Парсим XML комментарии из строки
        var xmlText = xmlBuilder.ToString();
        var trivia = SyntaxFactory.ParseLeadingTrivia(xmlText);
        return trivia;
    }
}

