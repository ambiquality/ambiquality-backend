using System.Text.RegularExpressions;

namespace Ambiquality.Auth.Api.Domain.Users;

public sealed class Email : IEquatable<Email>
{
    private Email(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Email Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidEmailException("Email address cannot be empty.");

        var normalized = value.Trim().ToLowerInvariant();

        if (!IsValidEmail(normalized))
            throw new InvalidEmailException("Email address format is invalid.");

        return new Email(normalized);
    }

    private static bool IsValidEmail(string email)
    {
        var pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(email, pattern);
    }

    public bool Equals(Email? other)
    {
        return other is not null && Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Email);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public override string ToString()
    {
        return Value;
    }
}
