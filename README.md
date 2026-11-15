# TimeBlock

Everyone in your circle posts what they're doing **when**, not just what. The core value is the shared timeline: you see your study slot Tuesday 7-9pm next to your friend's gym block — which is surprisingly motivating.

## Features

- **Shared week grid** — each person's time blocks visible to the group, per-person color-coded, week-by-week navigation.
- **Commitment vs. reality** — plan a block in advance, then mark it done, partially done, or skipped when it arrives. A weekly **follow-through rate** makes slippage visible instead of forgettable.
- **Muted accountability** — when a block ends unconfirmed, the owner gets a gentle in-app nudge (15 min grace). If the owner opts in, group members see a group-visible "missed" mark after an hour.
- **Recurring rhythms** — "Gym Mon/Wed/Fri 6am" is set once as a template; a background worker materializes occurrences for the next 14 days and you just confirm each one.
- **Conflict and overload view** — the dashboard warns when planned hours exceed your honest daily capacity ("11 h planned into a day with 6 free hours").
- **Per-person privacy** — any block can be **busy only**: others see that you're busy, not what you're doing.

## Stack

- ASP.NET Core 8 minimal API + hosted Blazor WebAssembly front end
- EF Core 8 + PostgreSQL (Npgsql); SQLite provider available for tests and local play
- JWT auth (PBKDF2 password hashing) with invite codes for groups
- Recurrence via a small RRULE subset (`FREQ=DAILY|WEEKLY`, `INTERVAL`, `BYDAY`, `UNTIL`, `COUNT`)
- SignalR hub (`/hubs/timeline`) pushes live `blockChanged` events to group members
- `BackgroundService` worker: materializes template occurrences, sends block-end reminders, applies missed marks

## Configuration

The app reads standard .NET configuration. For a remote PostgreSQL, set the connection string via environment variable or a `.env` file next to the app (see `.env.example`):

```
ConnectionStrings__Default=postgresql://postgres:postgres@localhost:5432/timeblock
Jwt__Key=replace-with-a-long-random-signing-key
```

URL-form connection strings (`postgresql://user:password@host:5432/dbname`, as handed out by most managed Postgres providers) are accepted too and converted automatically.

A tiny built-in loader picks up `.env` keys (existing environment variables win). Other useful keys: `Database__Provider` (`Postgres` default, `Sqlite` for tests), `Worker__Enabled`.

## Run locally

```bash
dotnet tool install --global dotnet-ef --version 8.0.11   # once
dotnet ef database update --project src/TimeBlock.Api     # create schema (also runs automatically on startup)
dotnet run --project src/TimeBlock.Api
```

Open `http://localhost:5080`, register a couple of accounts, create a circle on one and join via its invite code from the other.

Without a Postgres handy, you can smoke-test against SQLite:

```bash
Database__Provider=Sqlite ConnectionStrings__Default="Data Source=timeblock.db" dotnet run --project src/TimeBlock.Api
```

## Podman

```bash
podman build -t timeblock .
podman run --rm -p 8080:8080 --env-file .env timeblock
```

The container applies EF migrations on startup against the `ConnectionStrings__Default` Postgres.

## Tests

```bash
dotnet test TimeBlock.sln
```

21 tests: recurrence expansion, follow-through rate math, password hashing, and API integration tests (registration, invite-code groups, shared timeline, busy-only privacy, confirmation feeding stats, overload detection, template materialization) running against an in-memory SQLite via `WebApplicationFactory`.

## API surface

| Area | Endpoints |
| --- | --- |
| Auth | `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me` |
| Groups | `POST /api/groups`, `POST /api/groups/join`, `GET /api/groups/mine`, `POST /api/groups/{id}/regenerate-invite`, `POST /api/groups/{id}/leave`, `GET /api/groups/{id}/timeline?weekStart=` |
| Blocks | `GET/POST /api/blocks`, `PATCH /api/blocks/{id}/status`, `DELETE /api/blocks/{id}`, `GET /api/blocks/overload?from=&to=` |
| Templates | `GET/POST /api/templates`, `DELETE /api/templates/{id}` |
| Stats | `GET /api/stats/follow-through?weeks=` |
| Settings | `GET/PUT /api/me` |
| Notifications | `GET /api/notifications`, `POST /api/notifications/{id}/read`, `POST /api/notifications/read-all` |
| Live | SignalR hub `/hubs/timeline` (`Subscribe(groupIds)` → `blockChanged` events) |

All authenticated endpoints take `Authorization: Bearer <jwt>`; the hub accepts the token via `access_token` query string.

## Project layout

```
src/TimeBlock.Core      entities, DbContext, RRULE engine, stats, hashing, shared DTOs
src/TimeBlock.Api       minimal API endpoints, SignalR hub, background worker, serves the SPA
src/TimeBlock.Web       Blazor WebAssembly client (week grid, rhythms, circle, notifications)
src/TimeBlock.Tests     xUnit unit + integration tests
```
