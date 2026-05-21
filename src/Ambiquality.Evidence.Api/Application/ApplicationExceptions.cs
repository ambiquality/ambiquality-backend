using Ambiquality.Evidence.Api.Domain;

namespace Ambiquality.Evidence.Api.Application;

/// <summary>Raised when a referenced building does not exist.</summary>
public sealed class BuildingNotFoundException : DomainException
{
    public BuildingNotFoundException()
        : base("The requested building could not be found.") { }
}

/// <summary>
/// Raised when the current authenticated user is not allowed to operate on
/// the targeted aggregate (typically: not the owner).
/// </summary>
public sealed class ForbiddenException : DomainException
{
    public ForbiddenException()
        : base("You are not allowed to perform this operation.") { }
}

/// <summary>Raised when an attempted insert would collide with an existing URI slug.</summary>
public sealed class DuplicateUriSlugException : DomainException
{
    public DuplicateUriSlugException()
        : base("A resource with this URI slug already exists.") { }
}

/// <summary>
/// Raised when an attempted history-row insert would create an overlapping
/// validity range — the database GiST exclusion fired (SQLSTATE 23P01).
/// </summary>
public sealed class OverlappingValidityRangeException : DomainException
{
    public OverlappingValidityRangeException()
        : base("The attempted change would overlap an existing validity range.") { }
}

/// <summary>
/// Raised when a user-supplied codelist code does not appear in the
/// referenced vocabulary (e.g. an unknown anonymization level).
/// </summary>
public sealed class UnknownCodelistCodeException : DomainException
{
    public UnknownCodelistCodeException(string codelist, string code)
        : base($"The code '{code}' is not part of the '{codelist}' codelist.") { }
}
