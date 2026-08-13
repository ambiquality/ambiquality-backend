using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ambiquality.Auth.Api.Api.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Ambiquality.Auth.Api.Tests.Api;

[Collection(nameof(AuthApiCollection))]
public class AuthEndpointsTests(AuthApiFactory factory)
{
    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private async Task<(string Email, string Password)> RegisterAndConfirmAsync(HttpClient client)
    {
        var email = UniqueEmail();
        const string password = "Sup3rSecret!";

        var register = await client.PostAsJsonAsync(
            "/v1/register", new RegisterRequest(email, password));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var sent = factory.EmailSender.LastTo(email);
        Assert.NotNull(sent);
        var token = CapturingEmailSender.ExtractToken(sent.Body);

        var userId = await GetUserIdFromConfirmLink(sent.Body);
        var confirm = await client.GetAsync($"/v1/confirm-email?userId={userId}&token={token}");
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        return (email, password);
    }

    private static Task<string> GetUserIdFromConfirmLink(string body)
    {
        var marker = "userId=";
        var start = body.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = body.IndexOf('&', start);
        return Task.FromResult(body[start..end]);
    }

    [Fact]
    public async Task Register_ReturnsCreatedAndSendsConfirmationEmail()
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();

        var response = await client.PostAsJsonAsync(
            "/v1/register", new RegisterRequest(email, "Sup3rSecret!"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(factory.EmailSender.LastTo(email));
    }

    [Fact]
    public async Task Login_BeforeConfirmation_Returns401ProblemJson()
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync("/v1/register", new RegisterRequest(email, "Sup3rSecret!"));

        var response = await client.PostAsJsonAsync(
            "/v1/login", new LoginRequest(email, "Sup3rSecret!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Login_AfterConfirmation_ReturnsTokens()
    {
        var client = factory.CreateClient();
        var (email, password) = await RegisterAndConfirmAsync(client);

        var response = await client.PostAsJsonAsync(
            "/v1/login", new LoginRequest(email, password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
    }

    [Fact]
    public async Task Me_WithValidJwt_ReturnsIdentity()
    {
        var client = factory.CreateClient();
        var (email, password) = await RegisterAndConfirmAsync(client);
        var login = await client.PostAsJsonAsync("/v1/login", new LoginRequest(email, password));
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var response = await client.GetAsync("/v1/account/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.NotNull(me);
        Assert.Equal(email, me.Email);
        Assert.True(me.EmailConfirmed);
    }

    [Fact]
    public async Task Me_WithoutJwt_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/account/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_ThenReloginWithNewPassword_Succeeds()
    {
        var client = factory.CreateClient();
        var (email, password) = await RegisterAndConfirmAsync(client);
        var login = await client.PostAsJsonAsync("/v1/login", new LoginRequest(email, password));
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        const string newPassword = "An0therSecret!";
        var change = await client.PostAsJsonAsync(
            "/v1/account/change-password", new ChangePasswordRequest(password, newPassword));
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var relogin = await client.PostAsJsonAsync(
            "/v1/login", new LoginRequest(email, newPassword));
        Assert.Equal(HttpStatusCode.OK, relogin.StatusCode);

        var oldLogin = await client.PostAsJsonAsync(
            "/v1/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesTokens_AndOldTokenIsRejected()
    {
        var client = factory.CreateClient();
        var (email, password) = await RegisterAndConfirmAsync(client);
        var login = await client.PostAsJsonAsync("/v1/login", new LoginRequest(email, password));
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponse>();

        var refresh = await client.PostAsJsonAsync(
            "/v1/refresh", new RefreshRequest(tokens!.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var refreshed = await refresh.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(refreshed);
        Assert.NotEqual(tokens.RefreshToken, refreshed.RefreshToken);

        var reuse = await client.PostAsJsonAsync(
            "/v1/refresh", new RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal("application/problem+json", reuse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ChangeEmail_ConfirmsNewAddress()
    {
        var client = factory.CreateClient();
        var (email, password) = await RegisterAndConfirmAsync(client);
        var login = await client.PostAsJsonAsync("/v1/login", new LoginRequest(email, password));
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var newEmail = UniqueEmail();
        var change = await client.PostAsJsonAsync(
            "/v1/account/change-email", new ChangeEmailRequest(password, newEmail));
        Assert.Equal(HttpStatusCode.Accepted, change.StatusCode);

        var sent = factory.EmailSender.LastTo(newEmail);
        Assert.NotNull(sent);
        var token = CapturingEmailSender.ExtractToken(sent.Body);

        var confirm = await client.GetAsync($"/v1/account/confirm-email-change?token={token}");
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var me = await client.GetFromJsonAsync<MeResponse>("/v1/account/me");
        Assert.Equal(newEmail, me!.Email);
    }

    [Fact]
    public async Task DeleteAccount_WithCorrectPassword_Returns204_AndLoginStopsWorking()
    {
        var client = factory.CreateClient();
        var (email, password) = await RegisterAndConfirmAsync(client);
        var login = await client.PostAsJsonAsync("/v1/login", new LoginRequest(email, password));
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var me = await client.GetFromJsonAsync<MeResponse>("/v1/account/me");

        var delete = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/v1/account/{me!.Id}")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(password))
        });
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var relogin = await client.PostAsJsonAsync("/v1/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.Unauthorized, relogin.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_OfAnotherUser_Returns403_AndDoesNotDelete()
    {
        var client = factory.CreateClient();
        var (email, password) = await RegisterAndConfirmAsync(client);
        var login = await client.PostAsJsonAsync("/v1/login", new LoginRequest(email, password));
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var delete = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/v1/account/{Guid.NewGuid()}")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(password))
        });
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Equal("application/problem+json", delete.Content.Headers.ContentType?.MediaType);

        // The caller's own account is untouched.
        var me = await client.GetAsync("/v1/account/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_WithWrongPassword_Returns401_AndDoesNotDelete()
    {
        var client = factory.CreateClient();
        var (email, password) = await RegisterAndConfirmAsync(client);
        var login = await client.PostAsJsonAsync("/v1/login", new LoginRequest(email, password));
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var me = await client.GetFromJsonAsync<MeResponse>("/v1/account/me");

        var delete = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/v1/account/{me!.Id}")
        {
            Content = JsonContent.Create(new DeleteAccountRequest("wrong-password"))
        });
        Assert.Equal(HttpStatusCode.Unauthorized, delete.StatusCode);
        Assert.Equal("application/problem+json", delete.Content.Headers.ContentType?.MediaType);

        var stillThere = await client.PostAsJsonAsync("/v1/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_WithoutJwt_Returns401()
    {
        var client = factory.CreateClient();

        var delete = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/v1/account/{Guid.NewGuid()}")
        {
            Content = JsonContent.Create(new DeleteAccountRequest("Sup3rSecret!"))
        });

        Assert.Equal(HttpStatusCode.Unauthorized, delete.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_StillReturns201_AndSendsNoEmail()
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync("/v1/register", new RegisterRequest(email, "Sup3rSecret!"));

        // Anti-enumeration: an existing address gets the SAME 201 as a fresh one,
        // and no second confirmation email is sent (no 409, no distinct body).
        var response = await client.PostAsJsonAsync(
            "/v1/register", new RegisterRequest(email, "An0therSecret!"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400ProblemJson()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/v1/register", new RegisterRequest("not-an-email", "Sup3rSecret!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Register_TooShortPassword_Returns400WeakPassword_AndSendsNoEmail()
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();

        var response = await client.PostAsJsonAsync(
            "/v1/register", new RegisterRequest(email, "a"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("urn:ambiquality:auth:weak-password", problem.Type);
        Assert.Null(factory.EmailSender.LastTo(email));

        // No account was created — the same email can still be registered properly.
        var retry = await client.PostAsJsonAsync(
            "/v1/register", new RegisterRequest(email, "Sup3rSecret!"));
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithTooShortNewPassword_Returns400WeakPassword()
    {
        var client = factory.CreateClient();
        var (email, password) = await RegisterAndConfirmAsync(client);
        var login = await client.PostAsJsonAsync("/v1/login", new LoginRequest(email, password));
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var change = await client.PostAsJsonAsync(
            "/v1/account/change-password", new ChangePasswordRequest(password, "short"));

        Assert.Equal(HttpStatusCode.BadRequest, change.StatusCode);
        var problem = await change.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("urn:ambiquality:auth:weak-password", problem!.Type);

        // The old password still works.
        client.DefaultRequestHeaders.Authorization = null;
        var relogin = await client.PostAsJsonAsync("/v1/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, relogin.StatusCode);
    }
}

[CollectionDefinition(nameof(AuthApiCollection))]
public sealed class AuthApiCollection : ICollectionFixture<AuthApiFactory>;
