# ambiquality-backend

Backend services for Ambiquality — an open-source platform for collecting, storing, and sharing Indoor Environment Quality (IEQ) measurements from IoT sensors.

## Prerequisites

- [Podman](https://podman.io/) with the Docker Compose CLI plugin (`docker-compose`)
- [.NET SDK 10](https://dotnet.microsoft.com/download) (for local development and running tests)
- [dotnet-ef](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) global tool (for creating migrations)

```bash
dotnet tool install --global dotnet-ef
```

## Running with containers

All services run via Podman Compose. A `.env` file at the repo root is used to supply secrets (it is gitignored).

### 1. Create `.env`

```bash
cat > .env <<'EOF'
JWT_SECRET=your-secret-key-at-least-32-characters-long
EOF
```

### 2. Start / stop

```bash
./dev.sh up       # start all services (includes mailpit for email catching)
./dev.sh down     # stop all services and remove volumes

./dev-build.sh    # rebuild container images, then start (use after code changes)
```

### Services

| Service | URL | Notes |
|---------|-----|-------|
| Auth API | http://localhost:8080 | Proxied through Caddy |
| Mailpit (email UI) | http://localhost:8025 | Catches all outgoing emails |
| PostgreSQL | localhost:5432 | Internal only; exposed randomly for debugging |

## Configuration

All `appsettings.json` values can be overridden via environment variables using `__` as the section separator.

### Environment variables

| Variable | Default | Description |
|----------|---------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Set to `Development` to enable dev features |
| `ConnectionStrings__AuthDb` | — | PostgreSQL connection string |
| `Jwt__Issuer` | `ambiquality-auth` | JWT issuer claim |
| `Jwt__Audience` | `ambiquality` | JWT audience claim |
| `Jwt__Secret` | — | **Required.** Signing key, minimum 32 characters |
| `Jwt__AccessTokenMinutes` | `15` | Access token lifetime in minutes |
| `Jwt__RefreshTokenDays` | `30` | Refresh token lifetime in days |
| `Jwt__ConfirmationTokenHours` | `24` | Email confirmation token lifetime in hours |
| `Smtp__Host` | — | SMTP server hostname |
| `Smtp__Port` | `1025` | SMTP server port |
| `Smtp__UseStartTls` | `false` | Enable STARTTLS |
| `Smtp__FromAddress` | — | Sender email address |
| `Smtp__FromName` | — | Sender display name |
| `App__FrontendBaseUrl` | — | Base URL used in email confirmation links |

In Compose, these are set in `podman-compose.yml`. Override any value locally by adding it to `.env`:

```bash
# .env
JWT_SECRET=your-secret-key-at-least-32-characters-long
App__FrontendBaseUrl=http://localhost:3000
```

## Database migrations

Migrations are applied automatically at startup by the `migrate` container (runs the EF Core migrations bundle before `auth-api` starts).

To create a new migration after changing the domain model:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Ambiquality.Auth.Api \
  --startup-project src/Ambiquality.Auth.Api
```

Then rebuild the images to include the updated bundle:

```bash
./dev-build.sh
```

## Running tests

```bash
dotnet test
```

## API endpoints

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
