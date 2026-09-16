# FieldOps

AI-powered field service management platform built with .NET 10, ASP.NET Core, and PostgreSQL.

## Architecture

- **Modular Monolith** — each domain is a separate project with its own DbContext
- **Vertical Slices** — one folder per feature (Request, Handler, Endpoint)
- **Minimal APIs** — no controllers
- **Single Database** — PostgreSQL with schema separation per module

## Tech Stack

- .NET 10
- ASP.NET Core Minimal APIs
- PostgreSQL + EF Core
- JWT Authentication
- BCrypt password hashing
- Swagger / OpenAPI

## Project Structure

```
src/
├── FieldOps.Api/                      ← Entry point
├── FieldOps.Infrastructure/           ← Shared interfaces
└── Modules/
    └── FieldOps.Modules.Identity/     ← Auth, users, roles
```

## Getting Started

### Prerequisites

- .NET 10 SDK
- PostgreSQL

### Setup

1. Clone the repo:
   ```bash
   git clone https://github.com/yourusername/fieldops.git
   cd fieldops
   ```

2. Update connection string in `src/FieldOps.Api/appsettings.json`:
   ```json
   "ConnectionStrings": {
     "FieldOps": "Host=localhost;Database=fieldops;Username=postgres;Password=postgres"
   }
   ```

3. Run:
   ```bash
   dotnet run --project src/FieldOps.Api
   ```

4. Open Swagger: `https://localhost:5001/swagger`

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/auth/register` | Register a new user |
| POST | `/auth/login` | Login and get JWT token |
| GET | `/health` | Health check |

## Modules

| Module | Status | Description |
|--------|--------|-------------|
| Identity | ✅ | Register, login, JWT auth |
| Customers | 🔲 | Coming soon |
| Technicians | 🔲 | Coming soon |
| Service Requests | 🔲 | Coming soon |
| Work Orders | 🔲 | Coming soon |

## What I'm Learning

- Modular monolith architecture
- Vertical slice architecture
- CQRS / CQS patterns
- Domain-Driven Design
- JWT authentication
- PostgreSQL with EF Core
