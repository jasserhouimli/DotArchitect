# FieldOps

A field service management platform for companies that send technicians to customer sites. Think of it as a simplified version of ServiceTitan or Jobber — it handles customers, technicians, work orders, scheduling, inventory, and invoicing in one place.

The goal is to learn .NET backend development by building something real, not a todo app.

## Tech

- .NET 10 with Minimal APIs
- PostgreSQL with Entity Framework Core
- JWT authentication (access + refresh tokens via HttpOnly cookies)
- FluentValidation for request validation
- Serilog for logging
- Swagger for API docs

## Architecture

The project follows a **modular monolith** approach. Each business domain (Identity, Customers, Work Orders, etc.) lives in its own project with its own DbContext, but they all share a single PostgreSQL database with separate schemas per module.

Within each module, the code is organized by **vertical slices** — each feature (e.g. CreateWorkOrder) has its own Request, Handler, Endpoint, and Validator. There are no controllers, everything uses Minimal APIs.

Modules don't reference each other. When one module needs data from another, it uses plain Guid foreign keys — no navigation properties across module boundaries.

## Modules

| Module | Schema | What it does |
|--------|--------|--------------|
| Identity | `identity` | User registration, login, JWT auth, token refresh |
| Customers | `customers` | Customer CRUD with search and pagination |
| Technicians | `technicians` | Technician profiles and skills management |
| Work Orders | `work_orders` | Jobs linked to customers and technicians, with status tracking and assignment |
| Inventory | `inventory` | Parts and supplies tracking with stock transactions |
| Scheduling | `scheduling` | Technician appointments with date range filtering |
| Invoicing | `invoicing` | Invoice generation with line items, tax calculation, and status workflow |

## Getting Started

### Prerequisites

- .NET 10 SDK
- PostgreSQL

### Setup

1. Clone the repo:
   ```bash
   git clone https://github.com/jasserhouimli/FieldOps.git
   cd FieldOps
   ```

2. Copy the example config and fill in your database credentials:
   ```bash
   cp src/FieldOps.Api/appsettings.json src/FieldOps.Api/appsettings.Development.json
   ```
   Edit `appsettings.Development.json` with your PostgreSQL connection string and a JWT secret key (at least 32 characters).

3. Apply migrations:
   ```bash
   dotnet ef database update --context IdentityDbContext --project src/Modules/FieldOps.Modules.Identity --startup-project src/FieldOps.Api
   dotnet ef database update --context CustomersDbContext --project src/Modules/FieldOps.Modules.Customers --startup-project src/FieldOps.Api
   dotnet ef database update --context TechniciansDbContext --project src/Modules/FieldOps.Modules.Technicians --startup-project src/FieldOps.Api
   dotnet ef database update --context WorkOrdersDbContext --project src/Modules/FieldOps.Modules.WorkOrders --startup-project src/FieldOps.Api
   dotnet ef database update --context InventoryDbContext --project src/Modules/FieldOps.Modules.Inventory --startup-project src/FieldOps.Api
   dotnet ef database update --context SchedulingDbContext --project src/Modules/FieldOps.Modules.Scheduling --startup-project src/FieldOps.Api
   dotnet ef database update --context InvoicingDbContext --project src/Modules/FieldOps.Modules.Invoicing --startup-project src/FieldOps.Api
   ```

4. Run the API:
   ```bash
   dotnet run --project src/FieldOps.Api
   ```

5. Open Swagger at `https://localhost:5001/swagger`

## API at a Glance

All endpoints require JWT authentication unless noted otherwise. The JWT is returned in a cookie on login and sent back automatically.

**Auth:** `POST /auth/register`, `POST /auth/login`, `GET /auth/me`, `POST /auth/refresh`, `POST /auth/logout`

**Customers:** `POST /customers`, `GET /customers`, `GET /customers/{id}`, `PUT /customers/{id}`, `DELETE /customers/{id}`

**Technicians:** `POST /technicians`, `GET /technicians`, `GET /technicians/{id}`, `PUT /technicians/{id}`, `DELETE /technicians/{id}`, `POST /technicians/{id}/skills`

**Work Orders:** `POST /work-orders`, `GET /work-orders`, `GET /work-orders/{id}`, `PUT /work-orders/{id}`, `DELETE /work-orders/{id}`, `PUT /work-orders/{id}/assign-technician`

**Inventory:** `POST /inventory`, `GET /inventory`, `GET /inventory/{id}`, `PUT /inventory/{id}`, `DELETE /inventory/{id}`, `POST /inventory/transactions`

**Scheduling:** `POST /schedules`, `GET /schedules`, `GET /schedules/{id}`, `PUT /schedules/{id}`, `DELETE /schedules/{id}`

**Invoicing:** `POST /invoices`, `GET /invoices`, `GET /invoices/{id}`, `PUT /invoices/{id}`, `DELETE /invoices/{id}`, `POST /invoices/{id}/items`

## Project Structure

```
src/
├── FieldOps.Api/                          # Entry point, auth config, middleware
├── FieldOps.Infrastructure/               # Result pattern, middleware, shared code
└── Modules/
    ├── FieldOps.Modules.Identity/         # Users, auth, JWT
    ├── FieldOps.Modules.Customers/        # Customer management
    ├── FieldOps.Modules.Technicians/      # Technician profiles & skills
    ├── FieldOps.Modules.WorkOrders/       # Jobs & assignment
    ├── FieldOps.Modules.Inventory/        # Parts & stock tracking
    ├── FieldOps.Modules.Scheduling/       # Appointments
    └── FieldOps.Modules.Invoicing/        # Billing & line items
```
