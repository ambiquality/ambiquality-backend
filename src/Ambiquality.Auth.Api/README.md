# Ambiquality.Auth.Api

Authentication and account management service for the Ambiquality platform. It owns user
identity: registration, login, token issuance/refresh, email confirmation, and credential
changes. Covers thesis requirements **F01–F04**.

It is the only service that talks to the **`auth`** database; every other service treats
identity as opaque and trusts the JWT this service issues (the `sub` claim is a user GUID).

## Architecture

The project follows a Domain-Driven layering. Dependencies point inward — `Domain` knows
nothing about ASP.NET or EF Core; `Infrastructure` provides the adapters.

```
Api/             Minimal-API endpoint groups + request/response contracts + ProblemDetails mapping
  AuthEndpoints.cs        /register /login /refresh /confirm-email /resend-confirmation
  AccountEndpoints.cs     /account/* (Bearer-secured)
  Problems.cs             DomainException -> RFC 9457 ProblemDetails
Application/     Use-case handlers + ports (interfaces) the domain needs
  Users/*Handler.cs       one handler per use case
  Abstractions/           IClock, IEmailSender, IJwtIssuer, IPasswordService, ITokenGenerator
Domain/          Entities + value objects + invariants, no framework dependencies
  Users/                  User, Email, RefreshToken, VerificationToken, VerificationPurpose
Infrastructure/  Adapters that implement the Application ports
  Security/               JwtIssuer, IdentityPasswordHasher, TokenGenerator, SystemClock
  Messaging/              SmtpEmailSender
  Persistence/            AuthDbContext, UserRepository, EF migrations (auth schema)
```

## Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/register` | — | Register a new user |
| `POST` | `/login` | — | Login, returns access + refresh tokens |
| `POST` | `/refresh` | — | Refresh access token |
| `GET` | `/confirm-email` | — | Confirm email address |
| `POST` | `/resend-confirmation` | — | Resend confirmation email |
| `GET` | `/account/me` | Bearer | Get current user profile |
| `POST` | `/account/logout` | Bearer | Revoke refresh token |
| `POST` | `/account/change-password` | Bearer | Change password |
| `POST` | `/account/change-email` | Bearer | Request email change |
| `GET` | `/account/confirm-email-change` | — | Confirm email change |

The interactive OpenAPI reference (Scalar) is served at **`/scalar/v1`** and the raw document
at **`/openapi/v1.json`**. Behind the dev Caddy proxy that is
<http://localhost:8080/scalar/v1> (the Auth API is the proxy's default upstream).

## Authentication

JWT bearer tokens, validated with `ValidateIssuer`/`ValidateAudience`/`ValidateLifetime`/
`ValidateIssuerSigningKey` and a 30-second clock skew (`Program.cs`). The OpenAPI document
declares a `Bearer` security scheme and attaches it to every endpoint that requires
authorization, so the Scalar UI offers an "Authorize" box.

Error responses are RFC 9457 ProblemDetails with stable `urn:ambiquality:auth:*` type URIs so
clients can branch on the type. Authentication-failure details are deliberately generic
(`invalid-credentials`, `email-not-confirmed`) to avoid account enumeration — see `Problems.cs`.

## Configuration

All values can be supplied via environment variables (`__` is the section separator). In
Compose they are set on the `auth-api` service in `podman-compose.yml`.

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Set to `Development` to enable dev features |
| `ConnectionStrings__AuthDb` | — | PostgreSQL connection string for the `auth` database |
| `Jwt__Issuer` | `ambiquality-auth` | JWT issuer claim |
| `Jwt__Audience` | `ambiquality` | JWT audience claim |
| `Jwt__Secret` | — | **Required.** Signing key, minimum 32 characters |
| `Jwt__AccessTokenMinutes` | `15` | Access token lifetime in minutes |
| `Jwt__RefreshTokenDays` | `30` | Refresh token lifetime in days |
| `Jwt__ConfirmationTokenHours` | `24` | Email confirmation / email-change token lifetime in hours |
| `Smtp__Host` | — | SMTP server hostname |
| `Smtp__Port` | `1025` | SMTP server port |
| `Smtp__UseStartTls` | `false` | Enable STARTTLS |
| `Smtp__FromAddress` | — | Sender email address |
| `Smtp__FromName` | — | Sender display name |
| `App__FrontendBaseUrl` | — | Base URL used in email confirmation links |

In local development, outgoing emails are caught by [Mailpit](http://localhost:8025) rather
than being delivered.

## Database & migrations

Owns the **`auth`** database (schema `auth`) via `AuthDbContext`. Migrations are applied
automatically at startup by the `migrate` container before `auth-api` starts.

To create a new migration after changing the domain model:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Ambiquality.Auth.Api \
  --startup-project src/Ambiquality.Auth.Api
```

Then rebuild the images so the migrations bundle is up to date: `./dev-build.sh`.

## Running & testing

```bash
dotnet run --project src/Ambiquality.Auth.Api          # run standalone
dotnet test tests/Ambiquality.Auth.Api.Tests           # run this project's tests
```

See the [root README](../../README.md) for full container setup.
