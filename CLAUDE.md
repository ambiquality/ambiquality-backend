# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

## About

Backend for **Ambiquality** — an IEQ (Indoor Environmental Quality) monitoring platform built as a bachelor thesis at VŠE Prague (author: Vilém Charwot, submission May 2026). The system collects sensor measurements of indoor environmental parameters (CO₂, temperature, humidity, particulate matter, VOCs, acoustics, light) and exposes them as open data.

## Solution Structure

```
ambiquality-backend.slnx        ← single solution, two solution folders
src/
  Ambiquality.Core/             ← shared library: EF Core DbContext, domain models, migrations ownership
  Ambiquality.Auth.Api/         ← authentication service (register, login, logout, token management)
  Ambiquality.Ingestion.Api/    ← write-only: receives measurements from sensors
  Ambiquality.Public.Api/       ← read-only: public API, filtering, pagination, open data
tests/
  Ambiquality.Core.Tests/
  Ambiquality.Auth.Api.Tests/
  Ambiquality.Ingestion.Api.Tests/
  Ambiquality.Public.Api.Tests/
```

## Tech Stack

- **.NET 10**, ASP.NET Core minimal APIs
- **PostgreSQL + TimescaleDB** — time-series measurements
- **Redis** — cache layer
- **Caddy** — reverse proxy / ingress
- **Podman** — container runtime (not Docker)
- **EF Core** — code-first migrations, Npgsql provider
- **xUnit** — test framework

## Database Architecture

### Two databases, one Postgres instance

| Database | Owner | Purpose |
|----------|-------|---------|
| `auth` | Auth.Api | Users, password hashes, sessions, tokens |
| `ieq` | Ingestion.Api + Public.Api | Everything else |

### Schemas in `ieq`

| Schema | Content |
|--------|---------|
| `devices` | Buildings, rooms, sensors, sensor lifecycle |
| `measurements` | TimescaleDB hypertables — the core time-series data |
| `app` | User preferences, alert configs, dashboard settings |

### Database users

| User | Access | Used by |
|------|--------|---------|
| `auth_api` | Full DML on `auth` | Auth.Api |
| `ingestion_api` | INSERT/SELECT on `measurements.*`, SELECT on `devices.*` | Ingestion.Api |
| `public_api` | SELECT on `measurements.*`, `devices.*`; full DML on `app.*` | Public.Api |
| `migrator` | DDL owner of both databases | CI/CD migration runner only |

### EF Core ownership

- `AuthDbContext` lives in **Ambiquality.Auth.Api** and owns the `auth` database
- `IeqDbContext` lives in **Ambiquality.Core**, referenced by both Ingestion and Public APIs
- **Ingestion.Api** owns and runs EF migrations for the `ieq` database
- **Public.Api** references `IeqDbContext` read-only — never runs migrations

## Key Functional Requirements (from thesis)

| ID | Responsibility | Service |
|----|---------------|---------|
| F01–F04 | User registration, login, logout, credential change | Auth.Api |
| F05–F09 | Building, room, sensor registration and lifecycle | Public.Api (write for operators) |
| F10 | Measurement validation on ingestion | Ingestion.Api |
| F11–F15 | Public read API, filtering, pagination, search, OpenAPI spec | Public.Api |
| F16 | DCAT-AP-CZ catalog metadata publication | Public.Api |
| F17 | Downloadable data archive (CSV) | Public.Api |
| F18 | (Frontend) Interactive map — not in this repo |

## Key Non-Functional Constraints

- **Availability**: Public API ≥ 99% uptime per calendar month
- **Durability**: Measurements must be persisted before HTTP 2xx is returned (no ack before write)
- **Immutability**: Published measurements must never be silently modified or deleted; invalidation via explicit flag only
- **Performance**: Read API p95 < 1 s, p99 < 3 s for pages ≤ 100 records; ingestion ≥ 100 measurements/s sustained
- **Concurrency**: Read API must handle ≥ 50 concurrent requests within latency bounds

## Running Services

```bash
dotnet run --project src/Ambiquality.Auth.Api
dotnet run --project src/Ambiquality.Ingestion.Api
dotnet run --project src/Ambiquality.Public.Api
```

Start infrastructure (Postgres, Redis, Caddy) with Podman Compose before running services:

```bash
podman compose up -d
```

## Running Tests

```bash
dotnet test
dotnet test tests/Ambiquality.Ingestion.Api.Tests   # single project
```

## Architecture Decisions

- **Ingestion/Public split**: Deliberate separation for independent scaling — ingestion is write-heavy, public is read-heavy
- **TimescaleDB**: Chosen for time-series performance on `measurements` hypertable; use TimescaleDB-specific functions (time_bucket, continuous aggregates) where appropriate
- **No cross-database foreign keys**: User identity is propagated via JWT `sub` claim (GUID), stored as plain column — no FK from `ieq` to `auth`
- **Code-first migrations**: Schema is designed conceptually first, implemented via EF Core migrations; do not use `dotnet ef dbcontext scaffold`
- **Measurement immutability**: Soft-invalidation only — add an `is_invalid` flag and `invalidated_reason`, never DELETE or UPDATE measurement values
