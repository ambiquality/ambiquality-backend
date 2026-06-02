using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using Ambiquality.Auth.Api.Api;
using Ambiquality.Auth.Api.Application;
using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Application.Users;
using Ambiquality.Auth.Api.Domain.Users;
using Ambiquality.Auth.Api.Infrastructure.Messaging;
using Ambiquality.Auth.Api.Infrastructure.Persistence;
using Ambiquality.Auth.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration / options -------------------------------------------------
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
var smtpOptions = builder.Configuration.GetSection("Smtp").Get<SmtpOptions>() ?? new SmtpOptions();
var authOptions = new AuthOptions
{
    ConfirmationTokenLifetime = TimeSpan.FromHours(
        builder.Configuration.GetValue("Jwt:ConfirmationTokenHours", 24)),
    EmailChangeTokenLifetime = TimeSpan.FromHours(
        builder.Configuration.GetValue("Jwt:ConfirmationTokenHours", 24)),
    RefreshTokenLifetime = TimeSpan.FromDays(
        builder.Configuration.GetValue("Jwt:RefreshTokenDays", 30)),
    FrontendBaseUrl = builder.Configuration.GetValue("App:FrontendBaseUrl", "https://localhost")
};

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton(smtpOptions);
builder.Services.AddSingleton(authOptions);

// --- Persistence -------------------------------------------------------------
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("AuthDb"),
        o => o.MigrationsHistoryTable("__EFMigrationsHistory", "auth")));

builder.Services.AddScoped<IUserRepository, UserRepository>();

// --- Security / infrastructure adapters -------------------------------------
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<IPasswordService, IdentityPasswordHasher>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ITokenGenerator, TokenGenerator>();
builder.Services.AddSingleton<IJwtIssuer, JwtIssuer>();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<IThrottleDelayer, TaskDelayThrottleDelayer>();

// --- Application handlers ----------------------------------------------------
builder.Services.AddScoped<RegisterUserHandler>();
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<RefreshTokenHandler>();
builder.Services.AddScoped<ConfirmEmailHandler>();
builder.Services.AddScoped<ResendConfirmationHandler>();
builder.Services.AddScoped<ChangePasswordHandler>();
builder.Services.AddScoped<ChangeEmailHandler>();
builder.Services.AddScoped<ConfirmEmailChangeHandler>();
builder.Services.AddScoped<LogoutHandler>();

// --- AuthN / AuthZ -----------------------------------------------------------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();

// --- Brute-force rate limiting ----------------------------------------------
// Per-IP fixed window on /login. Complements the per-account backoff in
// LoginHandler: this stops volumetric guessing from one source; the backoff
// slows low-and-slow guessing against a single account. Neither locks an
// account, so an attacker cannot deny service to a legitimate user (OWASP).
const string loginRateLimitPolicy = "login";
builder.Services.AddRateLimiter(rateLimiter =>
{
    rateLimiter.AddPolicy(loginRateLimitPolicy, httpContext =>
    {
        // Resolve options per-request so integration tests can override the limit.
        var opts = httpContext.RequestServices.GetRequiredService<AuthOptions>();
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = opts.LoginIpPermitLimit,
            Window = opts.LoginIpWindow,
            QueueLimit = 0
        });
    });

    rateLimiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    rateLimiter.OnRejected = async (context, cancellationToken) =>
    {
        // Advertise when the window resets so clients know how long to wait.
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Type = "urn:ambiquality:auth:too-many-login-attempts",
            Title = "Too many login attempts",
            Detail = "Too many login attempts from your network. Please wait and try again."
        }, options: null, contentType: "application/problem+json", cancellationToken: cancellationToken);
    };
});

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Info = new OpenApiInfo
        {
            Title = "Ambiquality Auth API",
            Version = "v1",
            Description = "Authentication and account management API for the Ambiquality platform."
        };
        var components = document.Components ??= new OpenApiComponents();
        components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>();
        components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT access token issued by POST /login or POST /refresh."
        };
        return Task.CompletedTask;
    });

    // Attach Bearer security requirement to every endpoint that requires authorization.
    options.AddOperationTransformer((operation, context, ct) =>
    {
        if (context.Description.ActionDescriptor.EndpointMetadata
            .OfType<IAuthorizeData>().Any())
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer")] = []
            });
        }
        return Task.CompletedTask;
    });
});

var app = builder.Build();

// Behind Caddy the connecting peer is the proxy, so honour X-Forwarded-For to
// recover the real client IP for the per-IP login rate limiter. KnownProxies/
// Networks are cleared because the API is only reachable through the trusted
// reverse proxy in this deployment; do not expose it directly without revisiting.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor
};
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapAuthEndpoints();
app.MapAccountEndpoints();

app.Run();

/// <summary>Exposed so WebApplicationFactory-based integration tests can boot the API.</summary>
public partial class Program;
