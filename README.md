# Reflow — Visual Workflow Orchestration Platform

[![CI](https://github.com/jasserhouimli/Reflow/actions/workflows/ci.yml/badge.svg)](https://github.com/jasserhouimli/Reflow/actions/workflows/ci.yml)

> Backend-focused portfolio project with a visual frontend and data-engineering capabilities

Reflow is a visual workflow orchestration platform for defining, validating, versioning, executing, monitoring, and recovering automated workflows. A user builds a workflow from connected task nodes. The backend validates the workflow graph, stores immutable published versions, schedules work, executes tasks asynchronously, records state and logs, and handles failures and retries.

## Tech

- .NET 10 with Minimal APIs
- PostgreSQL with Entity Framework Core
- JWT authentication (access + refresh tokens via HttpOnly cookies)
- FluentValidation for request validation
- Serilog for logging
- Swagger for API docs
- React + TypeScript + Vite + Tailwind CSS + shadcn/ui

## Architecture

- Modular monolith with separate project per module
- Vertical slice architecture (one folder per feature)
- Minimal APIs (no controllers)
- `Result<T>` response type
- Single PostgreSQL database with schema separation per module
- No cross-module project references

## Modules

| Module | Responsibility | Status |
|--------|---------------|--------|
| Identity | User registration, login, JWT auth, token refresh | Done |
| WorkflowDesign | Workflow CRUD, nodes/edges, graph + config validation, publish/versioning, archive | Done |
| WorkflowExecution | Runs, task runs, attempts, logs, retry, cancel, artifacts, worker | Done |

See `Reflow_Project_Specification.md` for the full specification and roadmap.

## Getting Started

### Prerequisites

- .NET 10 SDK
- PostgreSQL
- Node.js (for frontend)

### Setup

1. Clone the repo:
   ```bash
   git clone https://github.com/jasserhouimli/Reflow.git
   cd Reflow
   ```

2. Create `src/Reflow.Api/appsettings.Development.json`:
   ```json
   {
     "ConnectionStrings": {
       "Reflow": "Host=localhost;Port=5432;Database=reflow;Username=postgres;Password=root"
     },
     "Jwt": {
       "Key": "your-32-char-secret-key-here-1234567890"
     }
   }
   ```

3. Apply migrations:
   ```bash
   dotnet ef database update --context IdentityDbContext --project src/Reflow.Api
   dotnet ef database update --context WorkflowDesignDbContext --project src/Reflow.Api
   dotnet ef database update --context WorkflowExecutionDbContext --project src/Reflow.Api
   ```

4. Run the API:
   ```bash
   dotnet run --project src/Reflow.Api --urls http://localhost:5001
   ```

5. Run the frontend:
   ```bash
   cd frontend && npm install && npm run dev
   ```
   Open http://localhost:5173

6. Open Swagger at `http://localhost:5001/swagger`

## API

| Method | Route | Description |
|--------|-------|-------------|
| POST | /api/v1/auth/register | Register |
| POST | /api/v1/auth/login | Login |
| GET | /api/v1/auth/me | Current user |
| POST | /api/v1/auth/logout | Logout |
| POST | /api/v1/workflows | Create workflow |
| GET | /api/v1/workflows | List workflows |
| GET | /api/v1/workflows/{id} | Get workflow |
| PUT | /api/v1/workflows/{id} | Update workflow (nodes/edges) |
| DELETE | /api/v1/workflows/{id} | Delete workflow |
| POST | /api/v1/workflows/{id}/validate | Validate workflow |
| POST | /api/v1/workflows/{id}/publish | Publish workflow version |
| POST | /api/v1/workflows/{id}/archive | Archive workflow |
| GET | /api/v1/workflows/{id}/versions | List published versions |
| GET | /api/v1/workflows/{id}/versions/{n} | Get version definition |
| POST | /api/v1/workflows/{id}/runs | Start a run |
| GET | /api/v1/workflows/{id}/runs | List runs |
| GET | /api/v1/runs/{id} | Get run |
| POST | /api/v1/runs/{id}/cancel | Cancel run |
| GET | /api/v1/runs/{id}/tasks | List task runs |
| GET | /api/v1/runs/{id}/logs | Run logs |
| GET | /api/v1/runs/{id}/artifacts/{node} | Download output artifact |
| GET | /api/v1/tasks/{id} | Get task run |
| GET | /api/v1/tasks/{id}/attempts | Task attempts |
| POST | /api/v1/tasks/{id}/retry | Retry failed task |

## Project Structure

```
src/
├── Reflow.Api/                          # Entry point, auth config, middleware
├── Reflow.Infrastructure/               # Result pattern, middleware, shared code
└── Modules/
    ├── Reflow.Modules.Identity/         # Users, auth, JWT
    ├── Reflow.Modules.WorkflowDesign/   # Workflows, nodes, edges, validation, versions
    └── Reflow.Modules.WorkflowExecution/# Runs, handlers, worker, artifacts
frontend/
└── src/
    ├── api/client.ts
    ├── pages/ (Login, Dashboard, WorkflowEditor)
    └── components/ui/
```

## Supported node types

| Type | Config | Description |
|------|--------|-------------|
| `data.csv.read` | `source` (text/upload), `csvText`/`fileId`, `delimiter`, `hasHeader`, `skipRows`, `trim`, `nullValues`, `maxRows`, `dedupeColumns` | Parse CSV from pasted text or an uploaded file |
| `data.json.read` | `source` (text/upload/input), `jsonText`/`fileId`/`column`, `rootPath` (e.g. `data.orders`) | Parse JSON standalone, or unpack JSON from an upstream column (objects merge, arrays explode) |
| `http.request` | `url`, `timeoutSeconds`, `headers`, `rootPath`, `pagination` (offset mode) | GET JSON over HTTPS (SSRF-guarded, custom headers allowlisted) |
| `data.validate` | `requiredColumns`, `columnTypes` (string/number/integer/boolean/date), `uniqueColumns` | Reject rows failing quality rules, with per-rule counts |
| `data.filter` | `column`, `operator` (equals/notEquals/contains/notContains/startsWith/endsWith/matches/inList/greaterThan/lessThan/isEmpty/isNotEmpty), `value` | Keep matching rows (regex is timeout-guarded) |
| `data.sort` | `orderBy` ([{column, direction}]) | Stable, numeric-aware multi-key sort |
| `data.limit` | `count`, `offset` | Take a slice of rows |
| `data.transform` | `select`/`dropColumns`, `renames`, `upperColumns`/`lowerColumns`, `fillNull`, `round`, `concat` | Reshape columns (names always refer to input columns) |
| `data.dedupe` | `columns` (empty = whole row) | Keep first of each duplicate group |
| `data.join` | `on` (or `leftOn`/`rightOn`), `how` (inner/left) | Join exactly two inputs on key columns |
| `data.aggregate` | `groupBy`, `operations` (count/countDistinct/sum/avg/min/max/median) | Group and summarize |
| `data.profile` | `columns` (empty = all) | One stats row per column (count, nulls, distinct, min/max/mean) |
| `data.output` | `format` (json/csv), `fileName`, `delimiter`, `includeHeader` | Save downloadable artifact |

## File uploads

Workflows can use uploaded files instead of pasted content (CSV/JSON/TXT, max 10 MB).
Files are immutable once uploaded, and files referenced by a published version
cannot be deleted.

| Method | Route | Description |
|--------|-------|-------------|
| POST | /api/v1/workflows/{id}/files | Upload a file (multipart `file` field) |
| GET | /api/v1/workflows/{id}/files | List uploaded files with row/column sniffing |
| DELETE | /api/v1/workflows/{id}/files/{fileId} | Delete a file (409 if published) |

## Testing

```bash
dotnet test
```

- `tests/Reflow.UnitTests` (65 tests) — graph/config validation, CSV parsing,
  dataset merge/serialization, all data handlers, SSRF guard. No infrastructure needed.
- `tests/Reflow.IntegrationTests` (19 tests) — full API via `WebApplicationFactory`:
  auth flows and per-user isolation, workflow CRUD/validate/publish/versions/archive,
  and complete run pipelines (success with quality counts, failure + manual retry,
  retry/cancel guards) against a real PostgreSQL database.

Integration tests need PostgreSQL on `localhost:5432` with a `postgres` superuser
(password `root`, same as the dev setup). They create and use a `reflow_test`
database automatically; each test registers its own user so no cleanup is needed.
