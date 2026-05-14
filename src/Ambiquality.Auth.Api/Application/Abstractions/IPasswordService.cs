using Ambiquality.Auth.Api.Domain.Users;

namespace Ambiquality.Auth.Api.Application.Abstractions;

/// <summary>Hashes and verifies user passwords.</summary>
public interface IPasswordService
{
    string Hash(User user, string password);

    bool Verify(User user, string passwordHash, string providedPassword);
}
