using CMS.API.Security;

namespace CMS.API.Tests;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Str0ng!Pwd")]   // upper+lower+digit+symbol (4 classes)
    [InlineData("Abcdefg1")]      // upper+lower+digit (3 classes), length 8
    [InlineData("abcdefg1!")]     // lower+digit+symbol (3 classes)
    [InlineData("ABCDEFG1!")]     // upper+digit+symbol (3 classes)
    public void IsValid_True_ForLongEnoughAndThreeClasses(string password)
    {
        Assert.True(PasswordPolicy.IsValid(password));
    }

    [Theory]
    [InlineData("")]              // empty
    [InlineData("Ab1!")]          // too short
    [InlineData("abcdefghij")]    // 1 class
    [InlineData("abcdefgh1")]     // 2 classes
    [InlineData("ABCDEFGH1")]     // 2 classes
    [InlineData(null)]            // null
    public void IsValid_False_ForShortOrTooFewClasses(string? password)
    {
        Assert.False(PasswordPolicy.IsValid(password));
    }
}
