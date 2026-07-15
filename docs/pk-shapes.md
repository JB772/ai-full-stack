# PK shapes & key columns

The repo has four distinct primary-key shapes. Pick the reference feature whose PK matches the table
you're adding and copy it. `Program.cs` registers `DateOnly`/`TimeOnly` Dapper handlers already.

## `AppRole` / `AppUser` — `int` IDENTITY **plus** an immutable natural key

- **`AppRole.RoleId` is immutable.** The table has a `pkid` IDENTITY column *and* a clustered PK on
  `RoleId`, which `AppUserRole` foreign-keys to. `pkid` is the API identifier; `RoleId` is never
  written by the UPDATE statement and the edit form disables the field. `AppUser.UserId` is the same
  shape (locked on edit, never in the UPDATE).
- Create returns **409** on a duplicate natural key; PUT guards `pkid <= 0 → 400`.

### `AppUser` adds a backend-only secret column

`AppUser.pkid` is `int` IDENTITY; `UserId` is the immutable clustered-PK natural key that `AppUserRole`
FKs to. What's new is `PasswordHash`:

- **`PasswordHash` is backend-only — it exists on *no* client-facing surface.** Not in the `AppUser`
  response model, not in `AppUserRequest`, not in any Angular model; SELECTs never read it. On CREATE
  the repository reads the default password from `SysConfig` (`configKey='appConfig'`, whose
  `configValue` is a JSON blob → take its `defaultPassword` property), SHA-256-hashes it
  (`Security/PasswordHasher.Hash`, uppercase hex), and stores it. `PasswordUpdatedTime` stays **NULL**
  on create (default password = "not yet updated"). UPDATE touches neither column.
- **Two write paths for the hash, both stamping `PasswordUpdatedTime = SYSDATETIME()`:**
  - **Admin reset — `POST /api/app-users/{id}/reset-password`** (`AppUserRepository`): no request body,
    so the raw password never crosses the API; it re-seeds from the `SysConfig` default. The frontend
    exposes it as a 重設密碼 button on the detail page — no password field in that UI.
  - **Self-service change — `POST /api/auth/change-password`** (`AuthRepository.UpdatePasswordAsync`,
    identity from the JWT): the signed-in user supplies current + new + confirm; the raw passwords are
    hashed server-side and never returned. Complexity is enforced by `Security/PasswordPolicy`. See the
    change-password bullet in `CLAUDE.md` for the full flow; this is the *only* place a plaintext password
    reaches the API and gets hashed into `PasswordHash` (the reset path uses the default instead).
- **The `SysConfig`/SHA-256 path is *not* covered by `dotnet test`.** The in-memory fake skips it (no
  `SysConfig` row; its `CreateAsync` just seeds an account), so the endpoint tests never exercise the
  default-password lookup. `PasswordHasher` is unit-tested against the canonical SHA-256("test") vector;
  the live Dapper path can only be verified against a DB that has the `appConfig` row (Swagger).
- **The `AppUserRole` N-N (user ↔ role) editor is deferred**, like every other N-N here (see
  `relationships-and-nav.md`). Its two FKs are plain (no cascade); it is counted as `RoleCount` and
  enforced by a 409 delete guard — the mirror image of `AppRole`'s `UserCount` guard. A user with role
  rows can't be deleted through the UI, exactly as a role with users can't. Adding the role-assignment
  picker is a future task.

## `PublishStatus` — `tinyint`, **no** IDENTITY (the *user* supplies the key)

All handled by `PublishStatusRepository` / `PublishStatusesController` — copy them:

- INSERT writes `pkid` explicitly; there is no `SELECT CAST(SCOPE_IDENTITY() AS int)`, and
  `CreateAsync` echoes the caller's pkid back.
- Create must check for a duplicate pkid and return **409** — otherwise SQL Server throws a raw PK
  violation.
- **No `pkid <= 0` → 400 guard on PUT** (the one `AppRole` uses). `0` is a legal `tinyint` key and a
  C# `byte` defaults to `0`, so "absent" and "zero" are indistinguishable. An unknown pkid is a 404.
- Route is `{id:int}` — ASP.NET has no `:byte` constraint — and the controller range-checks 0–255
  before casting, so `/api/publish-statuses/999` is a clean 404 rather than a 500.
- The form makes `pkid` editable on add and `disable()`s it on edit (same as `AppRole.RoleId`).

## `Partner` / `CourseGroup` — `smallint` IDENTITY (DB generates the key, the common case)

Check the column type, not just whether it says IDENTITY:

- `SELECT CAST(SCOPE_IDENTITY() AS smallint)` and `ExecuteScalarAsync<short>`, not `int`.
- Route stays `{id:int}` (ASP.NET has no `:short` constraint) and the controller range-checks against
  `short.MinValue`/`short.MaxValue` before casting, so `/api/partners/99999` is a clean 404 rather than
  an overflow. Same shape as `PublishStatusesController.TryToPkid`.
- The pkid is DB-generated, so the form has **no `pkid` control at all** — unknown on add, immutable on
  edit. This is the opposite of `PublishStatus`, whose form *does* expose it. Don't pattern-match the
  wrong reference.

## `Course` — plain `int` IDENTITY (no separate natural key)

- **A plain `int` IDENTITY PK needs no route range-check.** `Course.pkid` is a real `int` IDENTITY with
  *no* separate natural key (unlike `AppRole`, which is `int` IDENTITY **plus** an immutable `RoleId`). So
  the controller route is `{id:int}` binding straight to `int` — no `TryToPkid` helper like the smallint
  (`Partner`) / tinyint (`PublishStatus`) controllers need.
- PUT still guards `pkid <= 0 → 400`, and the form has no pkid control (DB-generated, immutable on edit
  — same as `Partner`).
