using System.Linq;

namespace CMS.API.Security;

/// <summary>
/// New-password complexity rule: at least 8 characters AND at least 3 of the 4 character
/// classes (uppercase, lowercase, digit, symbol). Mirrored client-side in the Angular app.
/// </summary>
public static class PasswordPolicy
{
    /// <summary>Bilingual rejection message shown to the user when a new password fails the rule.</summary>
    public const string ComplexityMessage =
        "密碼長度至少需 8 碼，且內容須至少包含四種字元的其中三種：大寫英文／小寫英文／數字／符號";

    public static bool IsValid(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8)
        {
            return false;
        }

        var classes = 0;
        if (password.Any(char.IsUpper)) classes++;
        if (password.Any(char.IsLower)) classes++;
        if (password.Any(char.IsDigit)) classes++;
        if (password.Any(c => !char.IsLetterOrDigit(c))) classes++;

        return classes >= 3;
    }
}
