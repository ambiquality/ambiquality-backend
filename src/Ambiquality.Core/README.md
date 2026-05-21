# Ambiquality.Core

> **Status: not yet implemented.** This project is currently an empty class library (default
> `.csproj`, no source files).

## Intended responsibility

A shared library for code that the measurement-facing services (`Ingestion.Api` and
`Public.Api`) both need. As planned:

- the `IeqDbContext` EF Core context and entity configurations for the `ieq` time-series data,
- the shared domain models for measurements,
- ownership of the EF migrations for the measurement database.

Ingestion would own and run those migrations; Public would reference the context read-only.

This is forward-looking design carried over from the thesis; the building/room evidence model
that *was* planned to live here ended up as a dedicated, self-contained
[`Evidence.Api`](../Ambiquality.Evidence.Api/README.md) instead.

See the [root README](../../README.md) for the overall project map.
