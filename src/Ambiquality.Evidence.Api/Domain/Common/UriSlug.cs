using System.Text.RegularExpressions;

namespace Ambiquality.Evidence.Api.Domain.Common;

/// <summary>
/// Kebab-case identifier exposed in public URIs and as a stable handle for
/// linked-data references. Lowercase alphanumerics plus internal hyphens,
/// must start with an alphanumeric, at most 64 characters.
/// </summary>
public sealed class UriSlug : IEquatable<UriSlug>
{
    private const int MaxLength = 64;
    private static readonly Regex Pattern = new(
        @"^[a-z0-9][a-z0-9-]*[a-z0-9]$|^[a-z0-9]$",
        RegexOptions.Compiled);

    private UriSlug(string value) => Value = value;

    public string Value { get; }

    public static UriSlug Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidUriSlugException("URI slug cannot be empty.");

        if (value.Length > MaxLength)
            throw new InvalidUriSlugException(
                $"URI slug must be at most {MaxLength} characters long.");

        if (!Pattern.IsMatch(value))
            throw new InvalidUriSlugException(
                "URI slug must be kebab-case: lowercase alphanumerics and internal hyphens only.");

        return new UriSlug(value);
    }

    public bool Equals(UriSlug? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as UriSlug);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value;
}

/// <summary>Raised when a string cannot be parsed as a <see cref="UriSlug"/>.</summary>
public sealed class InvalidUriSlugException : DomainException
{
    public InvalidUriSlugException(string message) : base(message) { }
}
