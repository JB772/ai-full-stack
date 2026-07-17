# Row audit (`RowAuditWriter`)

A cross-cutting service that writes **one** `RowAudit` row per change to any business table. It is
**wired into every CRUD repository** (backend): each Create/Update/Delete writes an audit row on the
same connection+transaction as the operation. The audit trail is also **surfaced**: a read endpoint
(`GET /api/rowaudit`) and a reusable frontend badge/dialog — see **The read side (viewer)** below. The
`AuditHelper` from the sample specs is still fictional — don't invent it.

## The table

`dbo.RowAudit` already exists and is populated in the live DB (schema, reference only):

```sql
pkid             int IDENTITY  -- generated; NEVER inserted
TableName        varchar(50)   NOT NULL
UserName         nvarchar(100) NOT NULL
PrimaryKeyValues nvarchar(100) NOT NULL
ActionType       varchar(20)   NOT NULL   -- "Insert" | "Update" | "Delete"
ActionDesc       varchar(1000) NULL
[DateTime]       datetime      NOT NULL   -- bracket it; DateTime is a keyword
```

## The writer — `src/CMS.API/Auditing/RowAuditWriter.cs`

Registered **scoped** in `Program.cs` (`AddScoped<RowAuditWriter>()`), which also calls
`AddHttpContextAccessor()` so the writer can read the current request's JWT. Concrete class, **no
interface** (matches "single reusable service"). Dapper only. It does **not** own a connection — the
caller passes its **open `IDbConnection` + optional `IDbTransaction`**, so the audit insert lands on the
same connection/transaction as the operation. It inserts every column except `pkid`.

Public API (async — inserts go through Dapper, so repositories `await` them):

| Method | ActionType | ActionDesc |
|--------|-----------|-----------|
| `LogInsertAsync(tableName, entity, conn, tx)` | `Insert` | value of the entity's **first string property, declaration order** |
| `LogDeleteAsync(tableName, entity, conn, tx)` | `Delete` | same first-string-property rule, on the deleted entity |
| `LogUpdateAsync(tableName, before, after, conn, tx)` | `Update` | comma-separated **names of scalar columns whose value changed** between `before`/`after` |

Column population:

- **UserName** — the `userName` JWT claim (`JwtTokenService.UserNameClaimType`, falls back to the `name`
  claim) via `IHttpContextAccessor`; **`"system"`** when there is no authenticated user.
- **PrimaryKeyValues** — the `pkid` property (case-insensitive reflection lookup) as a string; `""` if absent.
- **DateTime** — `DateTime.Now`.
- **ActionDesc** — truncated to `MaxActionDescLength` (1000) to fit `varchar(1000)`.

Behavioral notes:

- **`LogUpdateAsync` is a no-op when nothing changed** (empty changed-names list ⇒ no row written).
- **Only scalar columns are compared for Update.** `ChangedPropertyNames` skips class-typed nav objects
  (e.g. `Course.Partner`) and collections — each reload produces a fresh nav instance, so comparing them by
  reference would report a false change on every update. Scalar FK-id columns (`PartnerPkid`, …) *are*
  tracked. Count columns are scalar too but are identical across a before/after reload, so they never appear.
- **Declaration order is real**, not incidental: reflected properties are ordered by `MetadataToken`,
  because `Type.GetProperties()` order is not CLR-guaranteed. So "first string property" and the Update
  name list are deterministic.
- The reflection logic is pure `static` methods — `BuildInsert` / `BuildUpdate` / `BuildDelete`,
  `FindPkidValue`, `FirstStringPropertyValue`, `ChangedPropertyNames`, `Truncate` — with **no DB or
  HttpContext dependency**. That is the unit-test seam (`RowAuditWriterTests`); the tests never touch SQL
  Server. `CurrentUserName()` is the one instance method that reads the JWT.

## How it is wired into repositories

Every write repository (**AppRole, AppUser, PublishStatus, Partner, CourseGroup, Course,
FeaturedPromoItem**) takes `RowAuditWriter` in its primary constructor and follows one shape per verb,
all inside a transaction so the audit row shares the operation's fate:

```csharp
using var conn = connectionFactory.CreateConnection();
conn.Open();
using var tx = conn.BeginTransaction();

// Insert: run the INSERT, reload by the new pkid, LogInsert(created), commit.
// Update: reload before, run the UPDATE, reload after, LogUpdate(before, after), commit.
// Delete: reload the row, run the DELETE, LogDelete(row), commit.
```

- Each repo has a private `LoadByPkidAsync(conn, pkid, tx)` that reuses its `SelectColumns` on the **same**
  open connection/transaction — a standalone `GetByPkidAsync` opens its own connection and cannot see the
  in-flight change. Multi-map repos (`Course`, `FeaturedPromoItem`) reload with their nav-object mapper.
- Update/Delete **reload first**; if the row is absent they return `false` and write nothing.
- `AppUserRepository.CreateAsync` reads the `SysConfig` default password **before** `BeginTransaction`, so
  every command on the connection honours the transaction (SQL Server rejects a non-transactional command on
  a connection with a pending local transaction).
- `AuthRepository`'s three `AppUser` writes — `UpdateUserNameAsync`, `UpdatePasswordAsync`,
  `ResetPasswordToDefaultAsync` — are audited too (retrofitted 2026-07-17; they were the one gap). Two things
  make them worth reading before touching:
  - **They audit the `AppUser` response model, which has no `PasswordHash` property.** That is deliberate and
    load-bearing: `LogUpdateAsync`'s ActionDesc is the list of changed *column names*, so a password change
    audits as `PasswordUpdatedTime` and the hash can never reach `dbo.RowAudit` — a table any authenticated
    user can read via `GET /api/rowaudit`. Do not "improve" the projection by adding `PasswordHash` to it.
  - `ResetPasswordToDefaultAsync` reads the `SysConfig` default password **inside** the transaction and so
    passes `tx` down to `GetDefaultPasswordAsync`. That is the other way round the trap noted above —
    `AppUserRepository.CreateAsync` reads it *before* `BeginTransaction`; either is fine, but a
    non-transactional read on a connection with a pending transaction is not.
- **Not audited:** the lookup-only `TrainingCenter` / `Promotion` repos (no writes), and
  `FeaturedPromoItem.MoveAsync` (a two-row positional swap, not an Insert/Update/Delete).

**Testing.** `RowAuditWriter` is scoped and needs no swap in `CmsApiFactory`; controller tests still run
against the in-memory `I{Table}Repository` fakes, which don't audit. The retrofit itself is covered by
`PublishStatusRepositoryAuditTests`, which drives the **real** `PublishStatusRepository` against an
in-memory **SQLite** database (its SQL is portable — no `SCOPE_IDENTITY`/`nchar`) and asserts each path's
audit row plus the "failed change / failed audit ⇒ no orphan row" transaction guarantee — no SQL Server.

## The read side (viewer)

The trail is exposed for the UI so a user can see a record's history without leaving its page.

**Backend.** `GET /api/rowaudit?tableName={T}&pkid={id}` (`RowAuditController` → `IRowAuditRepository` /
`RowAuditRepository`). It projects the four display columns — `DateTime`, `UserName`, `ActionType`,
`ActionDesc` — into `Models/RowAuditEntry` and returns them **newest-first**, ordered by the audit row's
own `pkid DESC` (a monotonic IDENTITY, so the order is stable even when two changes share a `DateTime`).
Read-only Dapper, one statement, no transaction. `tableName`/`pkid` are both required → `400` if missing;
like every non-auth endpoint it needs a Bearer token. It is a **new repository**, so it is registered in
`Program.cs` and swapped for `InMemoryRowAuditRepository` in `CmsApiFactory` (seeded across tables/pkids so
`RowAuditControllerTests` can prove the filter and the ordering).

**Frontend.** `RowAuditBadgeComponent` (`features/row-audit/`, standalone) + `RowAuditService` +
`RowAuditEntry` model. Inputs `[tableName]` + `[pkid]`; an `effect` fetches whenever `pkid > 0` (so a
create-mode form, whose pkid is 0, renders nothing). It shows the latest change inline on a pill badge
(e.g. `Update by alice · 2026-06-04 14:30`, or a neutral 「尚無異動紀錄 No history」) and opens a
`p-dialog` listing the full trail newest-first, with a 「尚無任何異動紀錄 No history yet」 empty state.
It is placed in the `.page-actions` toolbar of **every detail page and every edit form** (bind the DB
table name and the current record's pkid; forms expose `pkid` as `protected`, and `CourseGroupForm` binds
its existing `pkidDisplay` getter). `FeaturedPromoItem` (custom single-page UI) and the self-service
`auth/profile` page are intentionally excluded.

**Testing gotcha.** Because the badge is a child of every detail/form component, those components' specs
now transitively need `HttpClient` (via `RowAuditService`). The existing specs stub it —
`{ provide: RowAuditService, useValue: { getForRecord: () => of([]) } }` — rather than wiring real HTTP;
do the same for any new detail/form spec. `RowAuditBadgeComponent` has its own spec (inline-latest,
dialog-full-trail, both empty states, no-fetch-when-pkid-0).
