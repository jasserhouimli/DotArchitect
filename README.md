# DotArchitect

> Explore. Design. Generate .NET solutions.

DotArchitect is a web application for understanding, designing, and generating .NET solution architectures through an interactive project graph.

## Modes

- **Explore:** Upload a .NET solution ZIP, analyze project references, and explore the dependency graph
- **Design:** Create a solution structure visually, add projects and references, validate, and generate real `.sln` and `.csproj` files

## Tech

- .NET 10 with Minimal APIs
- PostgreSQL with Entity Framework Core
- JWT authentication (access + refresh tokens via HttpOnly cookies)
- FluentValidation for request validation
- Serilog for logging
- Swagger for API docs
- React + TypeScript + Cytoscape.js (Done)

## Architecture

- Modular monolith with separate project per domain
- Vertical slice architecture (one folder per feature)
- Minimal APIs (no controllers)
- CQS pattern with `Result<T>` response type
- Single PostgreSQL database with schema separation per module
- No cross-module project references
- FluentValidation on all inbound requests
- Graph algorithms kept independent of ASP.NET Core and EF Core

## Modules

| Module | Responsibility |
|--------|---------------|
| Identity | User registration, login, JWT auth, token refresh |
| Workspaces | Workspace lifecycle, ownership, access checks |
| Analysis | ZIP upload, project discovery, parsing, graph building |
| Graph | Dependency traversal, cycle detection, impact analysis |
| Design | Visual solution design, project definitions, validation |
| Generation | `.sln` and `.csproj` file generation |

## Getting Started

### Prerequisites

- .NET 10 SDK
- PostgreSQL

### Setup

1. Clone the repo:
   ```bash
   git clone https://github.com/jasserhouimli/DotArchitect.git
   cd DotArchitect
   ```

2. Copy the example config and fill in your database credentials:
   ```bash
   cp src/DotArchitect.Api/appsettings.json src/DotArchitect.Api/appsettings.Development.json
   ```
   Edit `appsettings.Development.json` with your PostgreSQL connection string and a JWT secret key (at least 32 characters).

3. Apply migrations:
   ```bash
   dotnet ef database update --context IdentityDbContext --project src/Modules/DotArchitect.Modules.Identity --startup-project src/DotArchitect.Api
   ```

4. Run the API:
   ```bash
   dotnet run --project src/DotArchitect.Api
   ```

5. Open Swagger at `https://localhost:5001/swagger`

## API

All endpoints require JWT authentication unless noted otherwise.

**Auth:** `POST /auth/register`, `POST /auth/login`, `GET /auth/me`, `POST /auth/refresh`, `POST /auth/logout`

## Project Structure

```
src/
├── DotArchitect.Api/                          # Entry point, auth config, middleware
├── DotArchitect.Infrastructure/               # Result pattern, middleware, shared code
└── Modules/
    └── DotArchitect.Modules.Identity/         # Users, auth, JWT
```
