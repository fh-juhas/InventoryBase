# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```powershell
# Build the solution
dotnet build

# Run the web app (dev server at https://localhost:7408)
dotnet run --project InventoryBase.Web

# Apply EF Core migrations manually (auto-runs on startup too)
dotnet ef database update --project InventoryBase.Infrastructure --startup-project InventoryBase.Web

# Add a new migration
dotnet ef migrations add <MigrationName> --project InventoryBase.Infrastructure --startup-project InventoryBase.Web
```

There are no automated tests in this project.

## Architecture

Three-project clean architecture:

- **InventoryBase.Core** — Entities and interfaces only. No dependencies on other projects.
- **InventoryBase.Infrastructure** — EF Core `AppDbContext`, generic `Repository<T>`, `UnitOfWork`, and service implementations. References Core.
- **InventoryBase.Web** — ASP.NET Core MVC controllers and Razor views. References both Core and Infrastructure.

## Key Patterns

### Repository + Unit of Work
Controllers and services never use `AppDbContext` directly. All data access goes through `IUnitOfWork`, which exposes one typed `IRepository<T>` per entity (e.g., `_uow.Products`, `_uow.Purchases`). Always call `_uow.SaveChangesAsync()` to commit.

`IRepository<T>` key methods:
- `Query()` — returns `IQueryable<T>`; use `.Include()` chains here
- `FindAsync(predicate)` — async where clause returning a list
- `AddAsync`, `Update`, `Remove`, `RemoveRange`

### Service Layer
Complex business logic (categories, products, expenses, company settings, users) is abstracted behind `IXxxService` interfaces in Core and implemented in Infrastructure. Controllers for Purchase and Sale do **not** use a service layer — they call `_uow` directly.

### ID Obfuscation
Integer database IDs are never exposed in URLs. `IHashService.Encode(id)` / `Decode(hash)` (Hashids.net, salt in `appsettings.json`) converts them. Always decode at the start of any action that receives an `id` route parameter.

### StockLedger
`StockLedger.ReferenceId` is a plain `int` — **not a foreign key**. EF Core will not cascade-delete ledger entries. When deleting or editing a Purchase or Sale, manually query and `RemoveRange` the matching ledger entries using `MovementType` + `ReferenceId`. Positive quantity = stock in (Purchase), negative = stock out (Sale).

### Two-SaveChanges Pattern for Edit
When editing a transactional record (Purchase/Sale): first `SaveChangesAsync()` atomically removes old line items + old ledger entries + updates the header + adds new line items; then second `SaveChangesAsync()` adds new ledger entries. This mirrors how Create works.

## Frontend

Views use plain Razor (no React/Blazor). Key JS libraries loaded globally in `_Layout.cshtml`:

- **Tabulator 6.2.1** — all data tables. Shared defaults in `window.tabulatorDefaults` (44px row height, 20 rows/page, server-side AJAX). Each table's data endpoint is a `GET` action returning `Json(list)`.
- **Select2 4.1.0** — all dropdowns. Product search dropdowns use AJAX (`/Purchase/ProductSearch`, `/Sale/ProductSearch`). Supplier/Customer dropdowns are static `<select>` elements populated from `ViewBag` server-side, with `templateResult` for two-line display (name + phone).
- **Toastr** — `toastr.success()` / `toastr.error()` for notifications.
- **`apiPost(url, body)`** — global helper in `_Layout.cshtml` for CSRF-protected `fetch` POST calls. Use this instead of raw `fetch` for all AJAX mutations.
- **`<dialog>`** element — used for modals (no Bootstrap modals). The project uses custom CSS variables for theming (`--bg`, `--surface`, `--border`, `--text`, `--danger`, `--success`, `--radius`, etc.).

## Database

- **SQL Server** hosted at `InventoryBase.mssql.somee.com` (credentials in `appsettings.json`).
- Migrations live in `InventoryBase.Infrastructure/Migrations/`. `MigrateAsync()` runs automatically on startup.
- Decimal precision: `(18,2)` for money fields, `(18,3)` for quantities.
- `ExpenseStatus` enum: `Draft` or `Confirmed` — only Draft expenses can be edited or deleted.
- `StockMovementType` enum: `Purchase`, `Sale`, `Adjustment`.

## Authentication

ASP.NET Identity with roles `Admin` and `User`. Cookie auth, 8-hour session. Default roles and the first admin account are created at startup if missing. Password policy is relaxed (6-char minimum, no complexity) for development.

## Currency & Locale

All monetary values are Bangladeshi Taka (৳). Use `.toLocaleString('en-BD', { minimumFractionDigits: 2 })` for JS formatting and `.ToString("N2")` / `.ToString("N3")` in Razor.
