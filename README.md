# Reflow — Visual Workflow Orchestration Platform

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
| `data.csv.read` | `csvText`, `delimiter`, `hasHeader`, `dedupeColumns` | Parse CSV text into rows |
| `http.request` | `url`, `timeoutSeconds` | GET JSON over HTTPS (SSRF-guarded) |
| `data.validate` | `requiredColumns` | Reject rows with empty required fields |
| `data.filter` | `column`, `operator`, `value` | Keep matching rows |
| `data.transform` | `select`, `renames`, `upperColumns`, `lowerColumns` | Reshape columns |
| `data.aggregate` | `groupBy`, `operations` (count/sum/avg/min/max) | Group and summarize |
| `data.output` | `format` (json/csv) | Save downloadable artifact |
