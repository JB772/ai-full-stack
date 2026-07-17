# Architecture & the add-a-feature workflow

The per-table code shapes and the standard workflow for adding a feature. Read this before
scaffolding or modifying any table's backend or frontend.

## Backend, per table

- `Models/{Table}.cs` — response model + nav objects/counts; `{Table}Request.cs` (create/update
  DTO); `{Table}Query.cs` (filter DTO).
- `Repositories/I{Table}Repository.cs` + Dapper implementation — **no EF**, one connection per
  call via `IDbConnectionFactory`; repos that write also take `RowAuditWriter` (see
  `docs/row-audit.md`).
- `Controllers/{TablePlural}Controller.cs` — routes are kebab-case plural (`/api/app-roles`);
  `PUT` takes `pkid` **from the body**, never the route.
- `DateOnly`/`TimeOnly` Dapper handlers are registered in `Program.cs`; register new
  repositories there too (and swap them in `CmsApiFactory` for tests — see
  `docs/schema-and-testing.md`).

## Frontend, per table

- `features/{table-plural}/{table}-list|-detail|-form/` + `{table}.model.ts` + `{table}.service.ts`.
- Standalone components, lazy-loaded in `app.routes.ts` under the token-guarded **pathless
  parent** (`authGuard`) — never top-level. `/new` must precede `/:id`.
- Lists: `p-table` + `p-drawer` filter; state persists to **session** storage
  (`{table}-list-filters` / `-sort` / `-page`).
- Forms: Reactive Forms. Sidebar entries: `navGroups` in `app.ts`.
- Auth plumbing lives in `features/auth/` — there is **no** `core/` directory.

## API base URL

From `environment*.ts` (path aliases `@env/*`, `@app/*`). **No dev proxy** — the frontend calls
`http://localhost:5000/api` directly; API CORS allows any localhost origin.

## Adding a feature

Built references: **AppRole**, **AppUser**, **PublishStatus**, **Partner**, **CourseGroup**,
**Course**, **FeaturedPromoItem**. Pick by PK shape, then read its row in
`docs/reference-features.md` for the secondary patterns it demonstrates:

| Reference | PK shape | Copy it when… |
|-----------|----------|---------------|
| `AppRole` | `int` IDENTITY + immutable natural key | the table has a business key others FK to |
| `PublishStatus` | `tinyint`, no IDENTITY | the *user* supplies the key |
| `Partner` | `smallint` IDENTITY | the DB generates the key (common case) |
| `Course` | `int` IDENTITY (plain) | outbound FKs (multi-map nav) or in-place cell editing |
| `FeaturedPromoItem` | `int` IDENTITY (plain) | non-triad UI, multi-column UNIQUE 409, positional swap |
| `AppUser` role editor | N-N junction (`AppUserRole`) | assign/remove junction membership — **edit-form only**, immediate audited API writes → `docs/relationships-and-nav.md` |

### Workflow

1. Read the table in `database/*.sql`; write a spec from `spec/feature-spec.template.md` to
   `spec/{sub-system}/{Table}.md` (`spec/ui-sample-*.png` are style reference only). Confirm
   whether `pkid` is really IDENTITY (see `docs/pk-shapes.md`).
2. Backend: model trio → repository → controller → register in `Program.cs`
   (+ `/api/lookups/{plural}` if it's an FK target — lookup-only targets need no full feature).
   Wire RowAudit; let errors reach the middleware.
3. Frontend: model → service → list/detail/form → route (`/new` before `/:id`, under the
   guarded parent) → sidebar entry. RowAudit badge on detail + form. A custom UI can collapse
   to one component/route (see `FeaturedPromoItem`).
4. Tests both sides: xUnit endpoints via `CreateAuthenticatedClient()` + in-memory fake swapped
   into `CmsApiFactory`; Karma for components + service (stub `RowAuditService` /
   `MessageService`).
