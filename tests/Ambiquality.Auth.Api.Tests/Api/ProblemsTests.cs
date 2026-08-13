using Ambiquality.Auth.Api.Api;
using Ambiquality.Auth.Api.Application;
using Ambiquality.Auth.Api.Domain;
using Ambiquality.Auth.Api.Domain.Users;

namespace Ambiquality.Auth.Api.Tests.Api;

public class ProblemsTests
{
    [Fact]
    public void Describe_InvalidEmail_MapsTo400WithStableType()
    {
        var descriptor = Problems.Describe(new InvalidEmailException("bad email"));

        Assert.Equal(400, descriptor.StatusCode);
        Assert.Equal("urn:ambiquality:auth:invalid-email", descriptor.Type);
    }

    [Fact]
    public void Describe_EmailAlreadyRegistered_MapsTo409()
    {
        var descriptor = Problems.Describe(new EmailAlreadyRegisteredException());

        Assert.Equal(409, descriptor.StatusCode);
        Assert.Equal("urn:ambiquality:auth:email-already-registered", descriptor.Type);
    }

    [Fact]
    public void Describe_InvalidCredentials_MapsTo401WithGenericDetail()
    {
        var descriptor = Problems.Describe(new InvalidCredentialsException());

        Assert.Equal(401, descriptor.StatusCode);
        Assert.Equal("urn:ambiquality:auth:invalid-credentials", descriptor.Type);
        // Generic message: must not reveal whether the account exists.
        Assert.DoesNotContain("not found", descriptor.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Describe_EmailNotConfirmed_MapsTo401()
    {
        var descriptor = Problems.Describe(new EmailNotConfirmedException());

        Assert.Equal(401, descriptor.StatusCode);
        Assert.Equal("urn:ambiquality:auth:email-not-confirmed", descriptor.Type);
    }

    [Fact]
    public void Describe_InvalidRefreshToken_MapsTo401()
    {
        var descriptor = Problems.Describe(new InvalidRefreshTokenException());

        Assert.Equal(401, descriptor.StatusCode);
        Assert.Equal("urn:ambiquality:auth:invalid-refresh-token", descriptor.Type);
    }

    [Fact]
    public void Describe_UserNotFound_MapsTo404()
    {
        var descriptor = Problems.Describe(new UserNotFoundException());

        Assert.Equal(404, descriptor.StatusCode);
        Assert.Equal("urn:ambiquality:auth:user-not-found", descriptor.Type);
    }

    [Fact]
    public void Describe_WeakPassword_MapsTo400WithStableType()
    {
        var descriptor = Problems.Describe(new WeakPasswordException("Password must be at least 12 characters."));

        Assert.Equal(400, descriptor.StatusCode);
        Assert.Equal("urn:ambiquality:auth:weak-password", descriptor.Type);
        Assert.Equal("Password must be at least 12 characters.", descriptor.Detail);
    }

    [Fact]
    public void Describe_GenericDomainException_MapsTo400()
    {
        var descriptor = Problems.Describe(new DomainException("some invariant broke"));

        Assert.Equal(400, descriptor.StatusCode);
        Assert.Equal("urn:ambiquality:auth:domain-rule-violation", descriptor.Type);
        Assert.Equal("some invariant broke", descriptor.Detail);
    }
}
