# Build Spec for AppUser
- database schema: `.\database\auth.sql`

> **Scope note (read `CLAUDE.md`).** This spec follows the `AppRole` reference implementation:
> an `int` IDENTITY `pkid` **plus** an immutable natural key (`UserId`) that another table
> (`AppUserRole`) foreign-keys to. Two deliberate scope decisions:
> - **`PasswordHash` is backend-only** — never sent to or received from the frontend, never in
>   `AppUserRequest` or any Angular model. It is seeded from `SysConfig` on create and only changed
>   through a dedicated **reset-password** endpoint (details below).
> - **The `AppUserRole` N-N (user ↔ role) editor is deferred**, exactly as every other table in this
>   repo defers N-N editing (see `Course.md`). `AppUserRole` is still **counted in the delete guard**
>   so a user with role assignments returns a clean **409** instead of a raw FK violation. This is the
>   mirror image of `AppRole`'s existing `UserCount` guard. Assigning roles to a user is a future task.

---

## Summary

`AppUser` is a login account: a `UserId` (natural key), a display `UserName`, an `IsActive` flag, a
backend-only `PasswordHash`, and a nullable `PasswordUpdatedTime`. It is the auth counterpart to
`AppRole`; the two are linked many-to-many through `AppUserRole`.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **int IDENTITY** (API identifier). Clustered PK is on `UserId` (the immutable natural key). |
| Foreign Keys | None outbound. `UserId` is FK-referenced **by** `AppUserRole`. |
| Required Fields | `UserId`, `UserName`, `IsActive`, `PasswordHash` (PasswordHash set server-side, not by the client) |
| N-N Relationships | `AppUserRole` — AppUser ↔ AppRole. **Editor deferred**, counted in delete guard. |
| Primary-Foreign Links | `AppUserRole` references `AppUser.UserId` — surfaced only as the `RoleCount` delete-guard count (no child list page). |
| Query Filters | keyword (`UserId`, `UserName`); `IsActive` tri-state |
| Default Sort | `UserId ASC` |

---

## Localization

### Chinese Table Name

- AppUser: 使用者
- Description: 系統登入帳號

### Chinese Column Names

- pkid: 主代碼
- UserId: 帳號
- UserName: 使用者名稱
- IsActive: 啟用
- PasswordHash: 密碼雜湊 (後端專用，不出現在前端)
- PasswordUpdatedTime: 密碼更新時間
- RoleCount: 角色數 (AppUserRole 關聯筆數)

---

## Required Fields

Required (NOT NULL, excluding IDENTITY PK):
- `UserId` — immutable after create (natural key referenced by `AppUserRole`)
- `UserName`
- `IsActive` (bit, DB default `1`)
- `PasswordHash` — **set server-side**, never supplied by the client

Optional (nullable):
- `PasswordUpdatedTime` (datetime) — null until the password is reset

---

## Foreign Keys

`AppUser` has no outbound FK columns. **N/A**

---

## Foreign-Primary Links

`AppUser` has no outbound FK columns. **N/A**

---

## Primary-Foreign Links

`AppUserRole` references `AppUser.UserId`. No child list page is built (the N-N editor is deferred),
so no navigation button is rendered. The relationship surfaces as the `RoleCount` count on the detail
page and drives the delete guard.

---

## N-N Relationships

### AppUser ↔ AppRole via `AppUserRole`

| Column | Type | Notes |
|--------|------|-------|
| pkid | int IDENTITY | surrogate PK |
| UserId | nvarchar(200) NOT NULL | FK → AppUser.UserId (part of composite unique key) |
| RoleId | nvarchar(200) NOT NULL | FK → AppRole.RoleId (part of composite unique key) |

**Editor deferred** (consistent with the whole repo — see `Course.md` scope note). The junction is
counted by `RoleCount` and enforced by the delete guard. The two FKs are **plain FKs (no
`ON DELETE CASCADE`)**, so SQL Server would reject the delete anyway; the 409 guard is a readability
upgrade, symmetric with `AppRole`'s `UserCount` guard.

---

## Query Filters (`POST /api/app-users/query`)

- **keyword** (`string?`): LIKE across `UserId`, `UserName` (short identifying columns only).
- **isActive** (`bool?`): tri-state exact match on `IsActive` (null = 全部, true = 啟用, false = 停用).

`PasswordHash` / `PasswordUpdatedTime` are **not** filterable.

---

## PasswordHash handling (backend only)

- **Excluded everywhere client-facing**: not in `AppUser` response model? — it *is* omitted from the
  response model and from `AppUserRequest`, and from every Angular interface. The client never sees
  the hash nor sends a password.
- **On CREATE**: read `SysConfig.configValue` where `configKey = 'appConfig'`; the value is a JSON
  object — extract its `defaultPassword` string property; SHA-256 hash it; store the hex digest as
  `PasswordHash`. `PasswordUpdatedTime` is left **NULL** on create (the account still holds the
  default password — it has not been *updated* yet).
- **On UPDATE**: `PasswordHash` and `PasswordUpdatedTime` are **never touched** by the normal update
  (only `UserName` and `IsActive` are written).
- **Reset password**: `POST /api/app-users/{id:int}/reset-password` re-reads `defaultPassword` from
  `SysConfig`, SHA-256 hashes it, writes `PasswordHash`, and stamps `PasswordUpdatedTime = SYSDATETIME()`.
  No request body — the raw password never crosses the API boundary. `204` on success, `404` if the
  user does not exist.
- Hashing is a small static helper (`PasswordHasher.Hash`) so it can be unit-tested deterministically.

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| (none) | — | `AppUser` is not consumed as an FK dropdown by any built feature. No new lookup. |

`GET /api/lookups/app-roles` already exists but is **not** used here (the role editor is deferred).

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/app-users` | List all (ordered by `UserId`) |
| `POST` | `/api/app-users/query` | Filtered query (body: `AppUserQuery`) |
| `GET` | `/api/app-users/{id:int}` | Get by pkid; 404 if missing |
| `POST` | `/api/app-users` | Create; 409 on duplicate `UserId`; password seeded from `SysConfig` |
| `PUT` | `/api/app-users` | Update (pkid from body; `<= 0` → 400; unknown → 404). `UserId`/password unchanged. |
| `DELETE` | `/api/app-users/{id:int}` | Delete; 409 if `RoleCount > 0` |
| `POST` | `/api/app-users/{id:int}/reset-password` | Reset password to default; 204 / 404. No body. |

`{id:int}` binds directly — `pkid` is a real `int` IDENTITY, so no range-check helper is needed
(same as `AppRole` / `Course`; contrast the smallint/tinyint controllers).

---

## Backend Notes

### Models

**`AppUser`** (response): `Pkid`, `UserId`, `UserName`, `IsActive`, `PasswordUpdatedTime` (`DateTime?`),
`RoleCount` (`int`). **No `PasswordHash`.**

**`AppUserRequest`** (write DTO): `Pkid`, `UserId` (`[Required]`, `[StringLength(200)]`), `UserName`
(`[Required]`, `[StringLength(200)]`), `IsActive` (`bool`, default `true`). **No `PasswordHash`.**

**`AppUserQuery`**: `Keyword` (`string?`), `IsActive` (`bool?`).

Type mapping: `bit` → `bool`; `datetime` NULL → `DateTime?`.

### SQL — SELECT

```sql
SELECT u.pkid, u.UserId, u.UserName, u.IsActive, u.PasswordUpdatedTime,
       (SELECT COUNT(*) FROM AppUserRole ur WHERE ur.UserId = u.UserId) AS RoleCount
FROM AppUser u
```
`GetAll` appends `ORDER BY u.UserId ASC`; `Query` adds the keyword / IsActive `WHERE` clauses;
`GetByPkid` adds `WHERE u.pkid = @Pkid`. **`PasswordHash` is never selected.**

### SQL — INSERT

Reads the default password first (see PasswordHash handling), then:
```sql
INSERT INTO AppUser (UserId, UserName, IsActive, PasswordHash, PasswordUpdatedTime)
VALUES (@UserId, @UserName, @IsActive, @PasswordHash, NULL);
SELECT CAST(SCOPE_IDENTITY() AS int);
```

### SQL — UPDATE

```sql
UPDATE AppUser
SET UserName = @UserName, IsActive = @IsActive
WHERE pkid = @Pkid;
```
`UserId` (natural key) and `PasswordHash` / `PasswordUpdatedTime` are intentionally omitted.

### SQL — reset password

```sql
UPDATE AppUser
SET PasswordHash = @PasswordHash, PasswordUpdatedTime = SYSDATETIME()
WHERE pkid = @Pkid;
```

### SQL — default password lookup

```sql
SELECT configValue FROM SysConfig WHERE configKey = 'appConfig';
```
Parse the JSON, read `defaultPassword`. Missing config / property → `500` (misconfiguration).

### Delete guard

`AppUsersController.Delete` loads the user (404), then blocks with **409** if `GetRoleCountAsync > 0`,
naming the count in the message. Mirrors `AppRolesController.Delete`.

### Special column notes

- No `nchar` columns — no `RTRIM()` needed.
- `PasswordUpdatedTime` is a plain `datetime` (nullable). Dapper returns `Kind = Unspecified`; the
  frontend appends `'Z'` before display (see Frontend Notes).
- No `DateOnly` / `TimeOnly` columns.

---

## Frontend Notes

### Model (`app-user.model.ts`)

```ts
export interface AppUser {
  pkid: number;
  userId: string;
  userName: string;
  isActive: boolean;
  passwordUpdatedTime: string | null;
  roleCount: number;
}
export interface AppUserRequest {
  pkid: number;
  userId: string;
  userName: string;
  isActive: boolean;
}
export interface AppUserQuery {
  keyword?: string | null;
  isActive?: boolean | null;
}
```
No password field anywhere.

### Service (`app-user.service.ts`)

Standard six methods + `resetPassword(pkid: number): Observable<void>` →
`POST /api/app-users/{pkid}/reset-password` (no body).

### List

`p-table` columns: 主代碼, 帳號 (`userId`), 使用者名稱 (`userName`), 啟用 (`p-tag` 是/否),
密碼更新時間 (`passwordUpdatedTime` → `+ 'Z' | date:'yyyy-MM-dd HH:mm'`, `—` when null),
角色數 (`roleCount`), 操作. Default sort `userId ASC`.
Filter drawer: keyword input + `IsActive` tri-state `p-select` (全部 / 啟用 / 停用).

### Detail

Field grid mirroring the list, plus a **重設密碼** button (calls `resetPassword`, confirms first,
reloads on success) alongside 返回 / 編輯.

### Form

Reactive form: `userId` (immutable — `disable()` on edit, hint text), `userName` (required),
`isActive` (`p-checkbox [binary]`, default `true`). **No password control.** No pkid control (IDENTITY).

### Session Storage Keys

`app-user-list-filters`, `app-user-list-sort`, `app-user-list-page`.

### Delete Confirmation

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.userId}」？
```

### Sidebar

Add 使用者 AppUser under the existing **系統管理 Admin** group in `app.ts`.

---

## Files to create / modify

**Backend:** `Models/AppUser.cs`, `Models/AppUserRequest.cs`, `Models/AppUserQuery.cs`,
`Security/PasswordHasher.cs`, `Repositories/IAppUserRepository.cs`, `Repositories/AppUserRepository.cs`,
`Controllers/AppUsersController.cs`, register in `Program.cs`.

**Frontend:** `features/app-users/app-user.model.ts`, `app-user.service.ts`,
`app-user-list/`, `app-user-detail/`, `app-user-form/`, routes in `app.routes.ts`, sidebar item in `app.ts`.

**Tests:** `CMS.API.Tests/Fakes/InMemoryAppUserRepository.cs`, `CMS.API.Tests/AppUsersControllerTests.cs`,
`CMS.API.Tests/PasswordHasherTests.cs`, register the fake in `CmsApiFactory.cs`;
`app-user.service.spec.ts`, `app-user-list.spec.ts`, `app-user-detail.spec.ts`, `app-user-form.spec.ts`.
