using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ambiquality.Auth.Api.Api;
using Ambiquality.Auth.Api.Api.Contracts;

namespace Ambiquality.Auth.Api.Tests.Api;

[Collection(nameof(AuthApiCollection))]
public class AuthEndpointsTests(AuthApiFactory factory)
{
    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    /// <summary>Extracts the raw refresh-token cookie value from a response.</summary>
    private static string? GetRefreshCookie(HttpResponseMessage response)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie")
            .FirstOrDefault(h => h.StartsWith(RefreshTokenCookie.Name + "=", StringComparison.OrdinalIgnoreCase));
        if (setCookie is null) return null;

        var value = setCookie[(RefreshTokenCookie.Name.Length + 1)..];
        var end = value.IndexOf(';');
        return end >= 0 ? value[..end] : value;
    }

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
    public async Task Login_AfterConfirmation_ReturnsAccessTokenAndSetsRefreshCookie()
    {
        var client = factory.CreateClient();
        var (email, password) = await RegisterAndConfirmAsync(client);

        var response = await client.PostAsJsonAsync(
            "/v1/login", new LoginRequest(email, password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        // The refresh token is delivered as an HttpOnly cookie, never in the body.
        Assert.NotNull(GetRefreshCookie(response));
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
    public async Task Refresh_RotatesRefreshCookie_AndOldTokenIsRejected()
    {
        var client = factory.CreateClient();
        var (email, password) = await RegisterAndConfirmAsync(client);
        var login = await client.PostAsJsonAsync("/v1/login", new LoginRequest(email, password));
        var oldToken = GetRefreshCookie(login);
        Assert.NotNull(oldToken);

        // The client's cookie jar sends the refresh cookie automatically.
        var refresh = await client.PostAsJsonAsync("/v1/refresh", new { });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var refreshed = await refresh.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(refreshed);
        var newToken = GetRefreshCookie(refresh);
        Assert.NotNull(newToken);
        Assert.NotEqual(oldToken, newToken);

        // Reusing the OLD cookie value is rejected (rotation) — use a fresh client
        // so its cookie jar doesn't auto-send the new token.
        var staleClient = factory.CreateClient();
        var reuse = new HttpRequestMessage(HttpMethod.Post, "/v1/refresh");
        reuse.Headers.Add("Cookie", $"{RefreshTokenCookie.Name}={oldToken}");
        var reused = await staleClient.SendAsync(reuse);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        Assert.Equal("application/problem+json", reused.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/v1/refresh", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Logout_ClearsRefreshCookie_AndTokenStopsWorking()
    {
        var client = factory.CreateClient();
        var (email, password) = await RegisterAndConfirmAsync(client);
        var login = await client.PostAsJsonAsync("/v1/login", new LoginRequest(email, password));
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);

        var logout = await client.PostAsJsonAsync("/v1/account/logout", new { });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // The refresh cookie is cleared: a subsequent refresh is unauthorized.
        var refresh = await client.PostAsJsonAsync("/v1/refresh", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
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
    public async Task Register_DuplicateEmail_Returns409ProblemJson()
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync("/v1/register", new RegisterRequest(email, "Sup3rSecret!"));

        var response = await client.PostAsJsonAsync(
            "/v1/register", new RegisterRequest(email, "An0therSecret!"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
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
}

[CollectionDefinition(nameof(AuthApiCollection))]
public sealed class AuthApiCollection : ICollectionFixture<AuthApiFactory>;
