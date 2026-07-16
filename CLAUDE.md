# CLAUDE.md

Guidance for Claude Code when working in this repository.

> **How to read this file.** Everything above the **Deep reference** table is *always-needed*.
> The detail lives in `docs/*.md` — the table says which file to open before working in an area.
> Read the doc, don't reconstruct it.

## What this is

A CMS scaffolded from an **existing** SQL Server schema. The database is the source of truth —
already deployed and populated. Never recreate or migrate it; read `database/*.sql` for a table's
shape before writing code against it.

| Path | What |
|------|------|
| `database/*.sql` | The schema. Reference only. |
| `spec/code-gen.convention.md` | **The** code-gen convention — read before adding any feature. |
| `spec/{sub-system}/{Table}.md` | Built feature specs (the real ones, e.g. `spec/course/Course.md`). |
| `spec/sample1.spec.md`, `sample2.spec.md` | **Aspirational** worked examples — richer than what shipped; don't conflate with the built specs. |
| `spec/feature-spec.template.md` | Template for new specs. `spec/ui-sample-*.png` = style reference only. |
| `docs/*.md` | Deep-dive references — see the **Deep reference** table. |
| `src/CMS.API` / `src/CMS.API.Tests` | .NET 9 Web API, Dapper, Swagger, port 5000 / xUnit. |
| `src/CMS.NG` | Angular 20 standalone + PrimeNG 20, port 4200. |

## Commands

Node is **not on PATH** — prepend it: PowerShell `$env:Path = "C:\Program Files\nodejs;$env:Path"`
(Bash `export PATH="/c/Program Files/nodejs:$PATH"`).

```powershell
dotnet run --project src\CMS.API      # API + Swagger at http://localhost:5000/swagger
dotnet test                           # xUnit — stop the API first: Get-Process CMS.API | Stop-Process -Force
cd src\CMS.NG; npm start              # http://localhost:4200
cd src\CMS.NG; npm test               # Karma + Jasmine (headless needs CHROME_BIN)
```

A running API locks the dll `dotnet test` rebuilds → confusing `MSB3021`. That and the other
Windows traps (`.ps1` policy on `npm`/`ng`, headless Chrome, UTF-8 curl) → **`docs/environment.md`**.

## Architecture

**Backend, per table:** `Models/{Table}.cs` (response + nav objects/counts), `{Table}Request.cs`,
`{Table}Query.cs`; `Repositories/I{Table}Repository.cs` + Dapper impl (no EF, connection per call,
takes `IDbConnectionFactory` + `RowAuditWriter` where it writes); `Controllers/{TablePlural}Controller.cs`.
Routes kebab-case (`/api/app-roles`); `PUT` takes pkid **from the body**, never the route.
`DateOnly`/`TimeOnly` handlers are registered in `Program.cs`.

**Frontend, per table:** `features/{table-plural}/{table}-list|-detail|-form/` + `{table}.model.ts`
+ `{table}.service.ts`. Standalone components, lazy-loaded in `app.routes.ts` under a token-guarded
pathless parent (`authGuard`) — never top-level. Lists: `p-table` + `p-drawer` filter, state in
session storage (`{table}-list-filters` / `-sort` / `-page`). Forms: Reactive Forms. Sidebar:
`navGroups` in `app.ts`. Auth plumbing is `features/auth/` — there is **no** `core/`.

**API base URL** from `environment*.ts` (aliases `@env/*`, `@app/*`). **No dev proxy** — the
frontend calls `http://localhost:5000/api` directly; API CORS allows any localhost origin.

## Cross-Cutting MUSTs

- **Every repository write is audited** via the shared `RowAuditWriter`, on the **same
  connection + transaction** as the change. Exempt: lookup-only repos (`TrainingCenter`/`Promotion`)
  and `FeaturedPromoItem.MoveAsync`. Writer mechanics, ActionDesc/UserName rules, test seam →
  **`docs/row-audit.md`**.
- **Every detail + edit-form page** carries `RowAuditBadgeComponent` in `.page-actions`
  (create-mode renders nothing). Host specs must stub `RowAuditService` → **`docs/row-audit.md`**.
- **No per-controller `try/catch`** for unexpected errors — the global middleware returns one safe
  500; leave 401/403/validation-400 alone. Frontend: `authInterceptor` is the only error seam
  (5xx toast, 401 → `/login`); specs wiring it must provide `MessageService` →
  **`docs/exception-handling.md`**.

## Gotchas — always-true rules

- **The database is real** — test writes land in the live DB. Test via `CMS.API.Tests` (in-memory
  fakes) or portable-SQL SQLite → **`docs/schema-and-testing.md`**.
- **New repositories must be swapped in `CmsApiFactory`** — miss one and tests hit real SQL Server.
- **Auth is global.** Only `POST /api/auth/login` is anonymous; a bare `CreateClient()` 401s — use
  `CmsApiFactory.CreateAuthenticatedClient()` / `CreateToken(role)` → **`docs/auth.md`**.
- **Frontend auth**: profile in **session** storage (`auth-profile`); Admin-only nav group; seed
  `auth-profile` before creating components in specs → **`docs/auth.md`**.
- **`nchar(n)` columns need `RTRIM()`** in every SELECT.
- **Read the constraints before assuming** — keys may lack UNIQUE indexes (`Partner.AppKey`), a
  two-FK table may not be a junction, one table spans several `.sql` files →
  **`docs/delete-guards.md`**, **`docs/relationships-and-nav.md`**, **`docs/schema-and-testing.md`**.
- **Frontend UI patterns are load-bearing** — sticky toolbar, QR (`qrcode`, not `angularx-qrcode`),
  inline cell editing (`(dblclick)`, not `pEditableColumn`), overlay-editor blur trap →
  **`docs/ui-patterns.md`** before reaching for any of them.
- **`/crud` skill layout is wrong for this repo** (assumes `core/` + `AuditHelper`) — follow the
  code, not the skill.
- **PrimeNG major tracks Angular's** (20 → 20); `primeng@latest` pulls v21 and fails peer resolution.

### Deep reference — read the relevant file before working in that area

| Doc | Read it before… |
|-----|-----------------|
| `docs/auth.md` | anything auth: login/JWT flows, `PasswordPolicy`, Admin reset, test seam, frontend plumbing |
| `docs/row-audit.md` | touching audit: `RowAuditWriter` rules + test seam, read path, `RowAuditBadgeComponent` |
| `docs/exception-handling.md` | touching error handling: global 500 middleware, `authInterceptor`, spec requirements |
| `docs/pk-shapes.md` | adding a table: the four PK shapes; `AppUser`'s backend-only `PasswordHash` |
| `docs/delete-guards.md` | writing any DELETE: 409 guards, multi-child messages, `ON DELETE CASCADE` checks |
| `docs/relationships-and-nav.md` | FKs or N-N: multi-map nav objects, nullable-FK `LEFT JOIN`, `forkJoin` lookups, date ⇄ `p-datepicker`, the `AppUserRole` N-N editor |
| `docs/reference-features.md` | adding a feature: the secondary pattern each built feature demonstrates |
| `docs/ui-patterns.md` | building a frontend page: toolbar, QR, inline editing, overlay blur trap |
| `docs/schema-and-testing.md` | reading schema / writing tests: constraint traps, multi-file `.sql`, `p-table` sort gotchas |
| `docs/environment.md` | any Windows/PowerShell dev trap |

## Adding a feature

Built references: **AppRole**, **AppUser**, **PublishStatus**, **Partner**, **CourseGroup**,
**Course**, **FeaturedPromoItem** (routes = kebab-case plural). Pick by PK shape, then read its
row in `docs/reference-features.md`:

| Reference | PK shape | Copy it when… |
|-----------|----------|---------------|
| `AppRole` | `int` IDENTITY + immutable natural key | the table has a business key others FK to |
| `PublishStatus` | `tinyint`, no IDENTITY | the *user* supplies the key |
| `Partner` | `smallint` IDENTITY | the DB generates the key (common case) |
| `Course` | `int` IDENTITY (plain) | outbound FKs (multi-map nav) or in-place cell editing |
| `FeaturedPromoItem` | `int` IDENTITY (plain) | non-triad UI, multi-column UNIQUE 409, positional swap |
| `AppUser` role editor | N-N junction (`AppUserRole`) | assign/remove junction membership — **edit-form only**, immediate audited API writes → `docs/relationships-and-nav.md` |

1. Read the table in `database/*.sql`; write a spec from the template to
   `spec/{sub-system}/{Table}.md`. Confirm whether `pkid` is really IDENTITY.
2. Backend: model trio → repository → controller → register in `Program.cs` (+ `/api/lookups/{plural}`
   if it's an FK target — lookup-only targets need no full feature). Wire RowAudit; let errors reach
   the middleware.
3. Frontend: model → service → list/detail/form → route (`/new` before `/:id`, under the guarded
   parent) → sidebar entry. RowAudit badge on detail + form. A custom UI can collapse to one
   component/route (see `FeaturedPromoItem`).
4. Tests both sides: xUnit endpoints via `CreateAuthenticatedClient()` + in-memory fake swapped
   into `CmsApiFactory`; Karma for components + service (stub `RowAuditService` / `MessageService`).

## gstack

The [gstack](https://github.com/garrytan/gstack) skill suite is installed (`~/.claude/skills/gstack`);
skills appear in the session's skill listing.

- **Use `/browse` for all web browsing** (fast headless Chromium).
- **Never use `mcp__claude-in-chrome__*` tools** — route browsing through `/browse`.
