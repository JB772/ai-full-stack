# CMS

Full-stack CMS scaffolded from the existing SQL Server schema in `database/`.

| Part | Stack | Local URL |
|------|-------|-----------|
| `src/CMS.API` | .NET 9 Web API, Dapper, Swashbuckle 7.2.0 | http://localhost:5000 (Swagger UI at `/swagger`) |
| `src/CMS.API.Tests` | xUnit + `WebApplicationFactory` | `dotnet test` |
| `src/CMS.NG` | Angular 20 standalone + PrimeNG 20 | http://localhost:4200 |

The API reads `ConnectionStrings:CMS` from `appsettings.json`
(`Server=.\SQLEXPRESS;Database=CMS;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False`).

## Run

```powershell
# backend — http://localhost:5000/swagger
dotnet run --project src\CMS.API

# frontend — http://localhost:4200 (calls the API directly via environment.ts, no proxy)
cd src\CMS.NG
npm start
```

## Test

```powershell
dotnet test                                  # xUnit — AppRole endpoints
cd src\CMS.NG; npm test                      # Karma + Jasmine — AppRole components & service
```

## Features

### AppRole (角色) — `系統管理 Admin \ 角色 AppRole`

CRUD over `dbo.AppRole` (`database/auth.sql`): list with query filter, view, add, edit, delete.

| Method | Route | Notes |
|--------|-------|-------|
| GET | `/api/app-roles` | All roles |
| POST | `/api/app-roles/query` | Filter by keyword (RoleId/RoleName/Description) and PermissionLevel range |
| GET | `/api/app-roles/{id}` | By `pkid` |
| POST | `/api/app-roles` | Create — 409 if `RoleId` already exists |
| PUT | `/api/app-roles` | Update — `pkid` from body |
| DELETE | `/api/app-roles/{id}` | 409 if the role still has users |
| GET | `/api/lookups/app-roles` | Slim lookup list |

`AppRole.pkid` is the API identifier, but `RoleId` is the table's clustered primary key and the
column `AppUserRole` points at — so `RoleId` is **immutable after creation**: the update SQL never
writes it, and the edit form disables the field. `UserCount` is a subquery count over `AppUserRole`.

## Conventions

Backend and frontend follow `spec/code-gen.convention.md` (model / request / query DTO trio,
Dapper-only repositories, `p-table` + `p-drawer` list pages, session-storage keys
`app-role-list-filters` / `-sort` / `-page`). `DateOnly` and `TimeOnly` Dapper type handlers are
registered in `Program.cs` for the tables still to come.
