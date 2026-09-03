using System.Text;
using Microsoft.Extensions.Configuration;

namespace OnlineJudge.Infrastructure.Auth;

public sealed class PasswordPolicy
{
    public const int MinimumLength = 8;
    public const int MaximumLength = 128;

    private static readonly string[] CommonPasswords =
    [
        "password", "password123", "123456", "123456789", "1234567890",
        "qwerty", "qwerty123", "admin", "administrator", "letmein", "welcome",
        "iloveyou", "monkey", "dragon", "football", "correcthorsebatterystaple"
    ];

    private readonly string[] siteTerms;

    public PasswordPolicy(IConfiguration? configuration = null)
    {
        siteTerms = (configuration?.GetSection("PasswordPolicy:ContextTerms").GetChildren()
                .Select(section => section.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!) ?? [])
            .Concat(["onlinejudge", "online judge", "unrealstudio"])
            .Select(Canonicalize)
            .Where(term => term.Length >= 4)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public string? Validate(string password, string? userName = null, string? email = null)
    {
        if (string.IsNullOrEmpty(password))
        {
            return "Password is required.";
        }

        if (password.Length > MaximumLength * 2)
        {
            return $"Password must not exceed {MaximumLength} Unicode characters.";
        }

        var normalized = password.Normalize(NormalizationForm.FormC);
        var length = normalized.EnumerateRunes().Count();
        if (length < MinimumLength)
        {
            return $"Password must contain at least {MinimumLength} Unicode characters.";
        }

        if (length > MaximumLength)
        {
            return $"Password must not exceed {MaximumLength} Unicode characters.";
        }

        var candidate = Canonicalize(normalized);
        var contextualTerms = siteTerms
            .Concat([Canonicalize(userName), Canonicalize(EmailLocalPart(email))])
            .Where(term => term.Length >= 4);

        if (normalized.EnumerateRunes().All(Rune.IsWhiteSpace)
            || CommonPasswords.Select(Canonicalize).Any(term => IsObviousVariant(candidate, term))
            || contextualTerms.Any(term => IsObviousVariant(candidate, term)))
        {
            return "Password is too common or too closely related to account information.";
        }

        return null;
    }

    private static bool IsObviousVariant(string candidate, string term)
    {
        if (candidate == term)
        {
            return true;
        }

        if (candidate.StartsWith(term, StringComparison.Ordinal))
        {
            return IsShortAffix(candidate[term.Length..]);
        }

        return candidate.EndsWith(term, StringComparison.Ordinal)
            && IsShortAffix(candidate[..^term.Length]);
    }

    private static bool IsShortAffix(string value) => value.Length is > 0 and <= 8;

    private static string? EmailLocalPart(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var separator = email.IndexOf('@');
        return separator > 0 ? email[..separator] : email;
    }

    private static string Canonicalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var rune in value.Normalize(NormalizationForm.FormKC).ToLowerInvariant().EnumerateRunes())
        {
            var mapped = rune.Value switch
            {
                '0' => 'o',
                '1' => 'i',
                '3' => 'e',
                '4' => 'a',
                '5' => 's',
                '7' => 't',
                '@' => 'a',
                '$' => 's',
                _ => rune.Value
            };

            if (Rune.IsLetterOrDigit(new Rune(mapped)))
            {
                builder.Append(char.ConvertFromUtf32(mapped));
            }
        }

        return builder.ToString();
    }
}
