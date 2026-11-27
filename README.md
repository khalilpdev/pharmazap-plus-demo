# PharmazapPlus

Customer-service and messaging platform for pharmacies, with WhatsApp and Instagram channels, CRM, real-time queues and background processing.

> This repository is a demo/portfolio build: some back-office and integration areas are disabled.

## Stack

.NET 11 · ASP.NET Core · Blazor Server + Fluent UI · Entity Framework Core · PostgreSQL 16 · Redis 7 · SignalR · xUnit

## Solution

```text
src/
├── PharmazapPlus.API                # REST API and integrations
├── PharmazapPlus.Web                # Blazor Server UI
├── PharmazapPlus.Shared             # Shared models and contracts
├── PharmazapPlus.Worker             # Background processing
├── PharmazapPlus.API.Tests          # Automated tests
├── PharmazapPlus.DemoApresentacao   # Playwright presentation script
└── PharmazapPlus.slnx
```

## Requirements

- .NET 11 SDK
- PostgreSQL 16+
- Redis 7+ (recommended)
- Docker / Docker Compose (optional, for local infrastructure)

## Running locally

```bash
docker compose up -d postgres redis
dotnet restore src/PharmazapPlus.slnx
dotnet build src/PharmazapPlus.slnx
dotnet test src/PharmazapPlus.slnx --no-build
./start-dev.sh
```

Default development ports: API `5051`, Web `5160` / `7160`.

## Configuration

All credentials and environment-specific values come from environment variables or from local, git-ignored settings files:

- `.env` (copy from `.env.example`)
- `src/PharmazapPlus.API/appsettings.Development.json` (copy from `appsettings.Development.example.json`)
- `src/PharmazapPlus.API/appsettings.Local.json`

No passwords, API keys or tokens are stored in the repository.

## Documentation

- [src/PharmazapPlus.API/README.md](src/PharmazapPlus.API/README.md)
- [src/PharmazapPlus.Web/README.md](src/PharmazapPlus.Web/README.md)
