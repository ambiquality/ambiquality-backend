using System.Text;
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
using Microsoft.AspNetCore.Identity;
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
builder.Services.AddScoped<DeleteAccountHandler>();

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

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapAuthEndpoints();
app.MapAccountEndpoints();

app.Run();

/// <summary>Exposed so WebApplicationFactory-based integration tests can boot the API.</summary>
public partial class Program;
