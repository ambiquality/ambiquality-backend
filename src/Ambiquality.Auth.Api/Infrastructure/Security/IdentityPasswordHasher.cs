using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace Ambiquality.Auth.Api.Infrastructure.Security;

/// <summary>
/// <see cref="IPasswordService"/> implemented on top of ASP.NET Core Identity's
/// <see cref="IPasswordHasher{TUser}"/>. This is the ONLY piece of Identity we
/// use — no UserManager, no IdentityDbContext.
/// </summary>
public sealed class IdentityPasswordHasher : IPasswordService
{
    private readonly IPasswordHasher<User> _hasher = new PasswordHasher<User>();

    public string Hash(User user, string password)
        => _hasher.HashPassword(user, password);

    public bool Verify(User user, string passwordHash, string providedPassword)
    {
        var result = _hasher.VerifyHashedPassword(user, passwordHash, providedPassword);
        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
