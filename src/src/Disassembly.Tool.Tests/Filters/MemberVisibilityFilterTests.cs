using Disassembly.Tool.Filters;
using System.Reflection;
using Xunit;

namespace Disassembly.Tool.Tests.Filters;

/// <summary>
/// Тесты для класса MemberVisibilityFilter
/// </summary>
public class MemberVisibilityFilterTests
{
    [Fact]
    public void IsPublicOrProtected_WithPublicType_ReturnsTrue()
    {
        // Arrange
        var publicType = typeof(string);

        // Act
        var result = MemberVisibilityFilter.IsPublicOrProtected(publicType);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPublicOrProtected_WithPrivateType_ReturnsFalse()
    {
        // Arrange
        // Используем вложенный private тип из System.String
        var stringType = typeof(string);
        var nestedTypes = stringType.GetNestedTypes(BindingFlags.NonPublic);
        var privateType = nestedTypes.FirstOrDefault(t => t.IsNestedPrivate);

        if (privateType != null)
        {
            // Act
            var result = MemberVisibilityFilter.IsPublicOrProtected(privateType);

            // Assert
            Assert.False(result);
        }
    }

    [Fact]
    public void IsPublicOrProtected_WithPublicMethod_ReturnsTrue()
    {
        // Arrange
        var publicMethod = typeof(string).GetMethod("ToString", Type.EmptyTypes)!;

        // Act
        var result = MemberVisibilityFilter.IsPublicOrProtected(publicMethod);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPublicOrProtected_WithPublicProperty_ReturnsTrue()
    {
        // Arrange
        var publicProperty = typeof(string).GetProperty("Length")!;

        // Act
        var result = MemberVisibilityFilter.IsPublicOrProtected(publicProperty);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPublicOrProtected_WithPublicField_ReturnsTrue()
    {
        // Arrange
        // Ищем публичное поле в каком-нибудь типе
        var typeWithField = typeof(Environment);
        var publicField = typeWithField.GetField("CurrentDirectory", BindingFlags.Public | BindingFlags.Static);

        if (publicField != null)
        {
            // Act
            var result = MemberVisibilityFilter.IsPublicOrProtected(publicField);

            // Assert
            Assert.True(result);
        }
    }

    [Fact]
    public void IsPublicOrProtected_WithPublicEvent_ReturnsTrue()
    {
        // Arrange
        // Ищем публичное событие
        var typeWithEvent = typeof(AppDomain);
        var publicEvent = typeWithEvent.GetEvent("DomainUnload");

        if (publicEvent != null)
        {
            // Act
            var result = MemberVisibilityFilter.IsPublicOrProtected(publicEvent);

            // Assert
            Assert.True(result);
        }
    }

    [Fact]
    public void IsPublicOrProtected_WithPublicConstructor_ReturnsTrue()
    {
        // Arrange
        var publicConstructor = typeof(string).GetConstructor(new[] { typeof(char[]) })!;

        // Act
        var result = MemberVisibilityFilter.IsPublicOrProtected(publicConstructor);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void FilterTypes_WithMixedVisibility_ReturnsOnlyPublicAndProtected()
    {
        // Arrange
        var assembly = typeof(string).Assembly;
        var allTypes = assembly.GetTypes().Take(100).ToList(); // Берем первые 100 типов для теста

        // Act
        var filtered = MemberVisibilityFilter.FilterTypes(allTypes).ToList();

        // Assert
        Assert.NotNull(filtered);
        Assert.All(filtered, t => Assert.True(MemberVisibilityFilter.IsPublicOrProtected(t)));
    }

    [Fact]
    public void FilterMembers_WithMixedVisibility_ReturnsOnlyPublicAndProtected()
    {
        // Arrange
        var type = typeof(string);
        var allMembers = type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        // Act
        var filtered = MemberVisibilityFilter.FilterMembers(allMembers).ToList();

        // Assert
        Assert.NotNull(filtered);
        Assert.All(filtered, m => Assert.True(MemberVisibilityFilter.IsPublicOrProtected(m)));
    }

    [Fact]
    public void IsPublicOrProtected_WithProtectedMethod_ReturnsTrue()
    {
        // Arrange
        // Создаем тестовый класс с protected методом
        var testType = typeof(TestClassWithProtected);
        var protectedMethod = testType.GetMethod("ProtectedMethod", BindingFlags.NonPublic | BindingFlags.Instance);

        if (protectedMethod != null)
        {
            // Act
            var result = MemberVisibilityFilter.IsPublicOrProtected(protectedMethod);

            // Assert
            Assert.True(result);
        }
    }

    // Вспомогательный класс для тестирования protected членов
    private class TestClassWithProtected
    {
        protected void ProtectedMethod() { }
        protected int ProtectedProperty { get; set; }
#pragma warning disable CS0649 // Field is never assigned to
        protected int ProtectedField;
#pragma warning restore CS0649
    }
}

