# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

A CMS scaffolded from an **existing** SQL Server schema. The database is the source of truth —
it already exists and is populated. Never recreate or migrate it; read `database/*.sql` to learn
a table's shape before writing code against it.

| Path | What |
|------|------|
| `database/*.sql` | The schema (auth, admin, course, promotion). Reference only — already deployed. |
| `spec/code-gen.convention.md` | **The** code-generation convention. Read it before adding any feature. |
| `spec/sample1.spec.md`, `sample2.spec.md` | Worked examples of a feature spec (Course, SkillTrain). |
| `spec/feature-spec.template.md` | Template for new feature specs. |
| `spec/ui-sample-*.png` | UI style reference only — not actual content. |
| `src/CMS.API` | .NET 9 Web API, Dapper, Swagger. Port 5000. |
| `src/CMS.API.Tests` | xUnit. |
| `src/CMS.NG` | Angular 20 standalone + PrimeNG 20. Port 4200. |

## Commands

Node is installed but **not on PATH** — prepend it in every shell:
`$env:Path = "C:\Program Files\nodejs;$env:Path"`

```powershell
dotnet run --project src\CMS.API      # API + Swagger UI at http://localhost:5000/swagger
dotnet test                           # xUnit
cd src\CMS.NG; npm start              # http://localhost:4200
cd src\CMS.NG; npm test               # Karma + Jasmine (needs CHROME_BIN for headless)
```

`ng test` headless: `$env:CHROME_BIN = "C:\Program Files\Google\Chrome\Application\chrome.exe"`
then `npx ng test --watch=false --browsers=ChromeHeadless`.

## Architecture

**Backend.** Per table: `Models/{Table}.cs` (response, with nav objects / subquery counts),
`{Table}Request.cs` (write DTO), `{Table}Query.cs` (search DTO); `Repositories/I{Table}Repository.cs`
+ Dapper implementation; `Controllers/{TablePlural}Controller.cs`. Routes are kebab-case
(`/api/app-roles`). `PUT` takes the pkid **from the body**, never a route param. Repositories take
`IDbConnectionFactory` and open a connection per call — Dapper only, no EF.
`DateOnly`/`TimeOnly` Dapper type handlers are registered in `Program.cs`.

**Frontend.** Per table: `features/{table-plural}/{table}-list|-detail|-form/` plus
`{table}.model.ts` and `{table}.service.ts`. Standalone components, lazy-loaded in `app.routes.ts`.
List pages use `p-table` (sortable, paginated) with a `p-drawer` filter, and persist state to
session storage under `{table}-list-filters` / `-sort` / `-page`. Forms use Reactive Forms.
Sidebar entries live in `app.ts` (`navGroups`) and render via `app.html`.

**API base URL** comes from `environment.ts` / `environment.development.ts` (aliases `@env/*`,
`@app/*`). There is **no dev-server proxy** — the frontend calls `http://localhost:5000/api`
directly, and the API's CORS policy allows any localhost origin.

## Gotchas

- **`AppRole.RoleId` is immutable.** The table has a `pkid` IDENTITY column *and* a clustered PK on
  `RoleId`, which `AppUserRole` foreign-keys to. `pkid` is the API identifier; `RoleId` is never
  written by the UPDATE statement and the edit form disables the field. Expect this shape on other
  auth tables (`AppUser.UserId`).
- **Deletes that would orphan children return 409**, not a FK exception — check the child count first.
- **`nchar(n)` columns need `RTRIM()`** in every SELECT (see the convention).
- **PrimeNG major version tracks Angular's.** Angular 20 → PrimeNG 20. Installing `primeng@latest`
  pulls v21 and fails peer resolution.
- **UTF-8 through the shell.** Chinese characters in a JSON body passed inline to `curl` via Bash get
  mangled and the API rejects the request. Write the payload to a file and use `--data-binary @file`,
  or use Swagger UI.
- **The database is real.** Test writes land in the live CMS database. Use a throwaway row and delete
  it, or exercise the endpoints through `CMS.API.Tests`, which swaps in an in-memory repository via
  `WebApplicationFactory` and needs no SQL Server.

## Adding a feature

1. Read the table in `database/*.sql` and write a spec from `spec/feature-spec.template.md`.
2. Backend: model trio → repository → controller (+ a `/api/lookups/{plural}` endpoint if the table
   is an FK target).
3. Frontend: model → service → list / detail / form → route → sidebar entry in `app.ts`.
4. Tests both sides: xUnit for the endpoints (list/filter, view, add, edit), Karma for the
   components and the data service.
