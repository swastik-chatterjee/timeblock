# Contributing to TimeBlock

Thanks for your interest in contributing. This document covers how to get a
development environment running, the project conventions, and how to submit
changes. By participating you agree to abide by the
[Code of Conduct](CODE_OF_CONDUCT.md).

## License

TimeBlock is licensed under the [Eclipse Public License 2.0](LICENSE). By
submitting a pull request, you agree that your contribution is licensed under
EPL-2.0 and that you have the right to license it that way. Third-party
dependencies are listed in [THIRD_PARTY.md](THIRD_PARTY.md).

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- EF Core tooling: `dotnet tool install --global dotnet-ef` (only needed when
  changing the data model)
- PostgreSQL (any reachable instance) for local development, or nothing extra
  for tests — the test suite runs on SQLite in-memory

## Getting started

```bash
git clone https://github.com/swastik-chatterjee/timeblock.git
cd timeblock
dotnet restore TimeBlock.sln
```

Configuration is read from environment variables; a `.env` file at the repo
root is loaded automatically if present (see `.env.example`):

```bash
cp .env.example .env
# then set ConnectionStrings__Default and Jwt__Key
```

## Running

```bash
dotnet run --project src/TimeBlock.Api
```

The API and Blazor WebAssembly UI are served together at the address printed
in the console (default `http://localhost:5080`).

## Testing

```bash
dotnet test TimeBlock.sln
```

The integration tests host the full API with `WebApplicationFactory` against
an in-memory SQLite database and disable the background worker. All tests must
pass before a PR can be merged.

## Database migrations

The `DbContext` lives in `src/TimeBlock.Core`, so migrations are generated into
the API project:

```bash
dotnet ef migrations add <Name> \
  --project src/TimeBlock.Api \
  --startup-project src/TimeBlock.Api \
  -- --provider Postgres
```

Postgres applies migrations automatically on startup
(`Database.Migrate()`); SQLite (tests) uses `EnsureCreated()`.

## Project layout

| Path | Purpose |
|---|---|
| `src/TimeBlock.Core` | Entities, `DbContext`, RRULE engine, stats, shared DTOs |
| `src/TimeBlock.Api` | Minimal API endpoints, SignalR hub, background worker, EF migrations |
| `src/TimeBlock.Web` | Blazor WebAssembly client |
| `src/TimeBlock.Tests` | xUnit unit + API integration tests |

## Conventions

- Keep changes focused; one logical change per PR.
- Match the existing style: file-scoped namespaces, minimal APIs with typed
  `Results<>` returns, records for DTOs in `TimeBlock.Core.Contracts`.
- Recurrence logic belongs in `TimeBlock.Core/Recurrence` — add unit tests
  there rather than testing through endpoints.
- Privacy rules (busy-only blocks) are enforced server-side in
  `BlockMapper`; never rely on the client to filter.
- Update `README.md` when adding a feature that changes setup, config, or the
  API surface.

## Submitting changes

1. Fork the repo (or create a feature branch).
2. Make your change with tests.
3. Run `dotnet test TimeBlock.sln` and make sure the full suite passes.
4. Open a pull request with a short description of what changed and why.

Bug reports and feature requests are welcome as GitHub issues — include steps
to reproduce for bugs.
