using Ambiquality.Auth.Api.Application;
using Ambiquality.Auth.Api.Application.Abstractions;
using Ambiquality.Auth.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace Ambiquality.Auth.Api.Tests.Api;

/// <summary>
/// Boots the real Auth.Api pipeline against a throwaway PostgreSQL container,
/// swapping in a <see cref="CapturingEmailSender"/> so tests can read tokens.
/// </summary>
public class AuthApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("auth")
        .WithUsername("auth_api")
        .WithPassword("auth_api")
        .Build();

    public CapturingEmailSender EmailSender { get; } = new();

    /// <summary>
    /// Per-IP login limit for the booted host. Defaults high so the shared
    /// rate-limiter partition never trips during ordinary tests; the dedicated
    /// rate-limit test overrides it to a small value.
    /// </summary>
    protected virtual int LoginIpPermitLimit => 10_000;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Repoint the DbContext at the test container.
            services.RemoveAll<DbContextOptions<AuthDbContext>>();
            services.AddDbContext<AuthDbContext>(options =>
                options.UseNpgsql(_container.GetConnectionString()));

            // Capture outbound email instead of hitting SMTP.
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            // Override throttling knobs: instant per-account backoff (no real
            // sleeping in tests) and a configurable per-IP limit.
            services.RemoveAll<AuthOptions>();
            services.AddSingleton(new AuthOptions
            {
                LoginThrottleBaseDelay = TimeSpan.Zero,
                LoginThrottleMaxDelay = TimeSpan.Zero,
                LoginIpPermitLimit = LoginIpPermitLimit,
                LoginIpWindow = TimeSpan.FromMinutes(1)
            });

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            db.Database.EnsureCreated();
        });
    }
}
