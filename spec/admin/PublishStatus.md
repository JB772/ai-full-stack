# Build Spec for PublishStatus
- database schema: `.\database\admin.sql`

> The identical `PublishStatus` definition is also scripted into `course.sql` and `promotion.sql`
> (byte-for-byte the same table). `admin.sql` is treated as the source of truth.

---

## Summary

`PublishStatus` is a small lookup/reference table describing the publication state of content
(draft / published / discontinued). It is a pure FK **target**: it has no outbound foreign keys of
its own, and both `Course` and `Promotion2` point at it. Practically it is a code table that will
hold a handful of rows.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **tinyint, NOT an IDENTITY column** — the value is supplied by the user on create (see Special Column Notes) |
| Foreign Keys | **N/A** — no outbound FKs |
| Required Fields | `pkid`, `Description`, `IsDraft`, `IsPublished`, `IsDiscontinued` (every column is NOT NULL) |
| N-N Relationships | **N/A** |
| Primary-Foreign Links | `Course.PublishStatus_pkid`, `Promotion2.PublishStatus_pkid` |
| Query Filters | keyword (`Description`), `IsDraft`, `IsPublished`, `IsDiscontinued` (all tri-state bools) |
| Default Sort | `pkid ASC` |

---

## Localization

### Chinese Table Name

- PublishStatus: 發布狀態
- Description: 內容發布狀態代碼表 (草稿／已發布／已下架)

### Chinese Column Names

- pkid: 主代碼
- Description: 狀態說明
- IsDraft: 草稿
- IsPublished: 已發布
- IsDiscontinued: 已下架

Derived (not DB columns, returned by the API):

- CourseCount: 課程數
- PromotionCount: 促銷活動數

---

## Required Fields

Every column in the table is NOT NULL, so all are required:

Required (NOT NULL):
- `pkid` — tinyint, **user-supplied** (not auto-generated). Valid range 0–255.
- `Description` — nvarchar(50)
- `IsDraft` — bit
- `IsPublished` — bit
- `IsDiscontinued` — bit

Optional (nullable): **none**.

---

## Foreign Keys

**N/A** — `PublishStatus` has no outbound foreign keys.

---

## Foreign-Primary Links

**N/A** — no outbound foreign keys, so there is nothing to link out to.

---

## Primary-Foreign Links

Two tables reference `PublishStatus.pkid`:

| Child table | FK column | Constraint |
|-------------|-----------|------------|
| `Course` | `PublishStatus_pkid` (tinyint NOT NULL) | `FK_Course_PublishStatus` |
| `Promotion2` | `PublishStatus_pkid` (tinyint NOT NULL) | `FK_Promotion2_PublishStatus` |

**Navigation buttons are deliberately NOT generated.** Neither `Course` nor `Promotion2` has been
scaffolded yet — there is no `/courses` or `/promotions` route to link to, and emitting buttons that
navigate to a non-existent route would produce a dead link. This mirrors how `AppRole` handles
`AppUserRole` today: it surfaces a **count**, not a link.

Instead, both relationships surface as read-only counts:

- `CourseCount` and `PromotionCount` are correlated subqueries on the response model, shown as
  columns in the list and as fields in the detail view.
- They are what the delete guard checks (see below).

When `Course` / `Promotion2` are scaffolded later, add link buttons to
`/courses?publishStatusPkid={pkid}` and `/promotions?publishStatusPkid={pkid}`.

---

## N-N Relationships

**N/A** — no junction tables reference `PublishStatus`.

---

## Query Filters

- **keyword**: string
  - LIKE on `Description` only (it is the sole string column).

- **IsDraft**: bool?
  - Exact match on the `IsDraft` bit column.
  - Tri-state: null = no filter, true = only drafts, false = only non-drafts.

- **IsPublished**: bool?
  - Exact match on the `IsPublished` bit column. Tri-state as above.

- **IsDiscontinued**: bool?
  - Exact match on the `IsDiscontinued` bit column. Tri-state as above.

No FK filters (no outbound FKs) and no date-range filters (no date columns).

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/publish-statuses` | **New** | All PublishStatus rows, ordered by `pkid ASC`; option label = `Description` |

`PublishStatus` is an FK target for `Course` and `Promotion2`, so per the convention it needs a
lookup endpoint. This is consumed by the future Course / Promotion features, not by
PublishStatus's own UI (it has no FK dropdowns of its own).

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/publish-statuses` | List all, ordered by `pkid ASC` |
| `POST` | `/api/publish-statuses/query` | Filtered query (body: `PublishStatusQuery`) |
| `GET` | `/api/publish-statuses/{id}` | Get by pkid |
| `POST` | `/api/publish-statuses` | Create — **409** if `pkid` already exists |
| `PUT` | `/api/publish-statuses` | Update (pkid from body; pkid itself is immutable) |
| `DELETE` | `/api/publish-statuses/{id}` | Delete — **409** if any Course or Promotion2 still references it |

Route id constraint is `{id:int}`; the controller range-checks `0–255` and returns `404` for
out-of-range values before casting to `byte`. (A `tinyint` PK cannot use a `byte` route constraint —
ASP.NET has no `:byte` constraint.)

Error conditions:
- `409 Conflict` on create when `pkid` is taken: `主代碼「{pkid}」已存在。`
- `409 Conflict` on delete when referenced: `發布狀態「{Description}」仍有 {n} 筆課程、{m} 筆促銷活動，無法刪除。`
- `404 Not Found` on get/update/delete of an unknown pkid.

No auth attributes (the project has no auth configured yet).

---

## Backend Notes

### Models

```csharp
// Models/PublishStatus.cs — response model
public class PublishStatus
{
    /// <summary>主代碼</summary>
    public byte Pkid { get; set; }

    /// <summary>狀態說明</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>草稿</summary>
    public bool IsDraft { get; set; }

    /// <summary>已發布</summary>
    public bool IsPublished { get; set; }

    /// <summary>已下架</summary>
    public bool IsDiscontinued { get; set; }

    /// <summary>課程數 (Course 關聯筆數)</summary>
    public int CourseCount { get; set; }

    /// <summary>促銷活動數 (Promotion2 關聯筆數)</summary>
    public int PromotionCount { get; set; }
}

// Models/PublishStatusRequest.cs — write DTO
public class PublishStatusRequest
{
    [Range(0, 255, ErrorMessage = "主代碼須介於 0 到 255。")]
    public byte Pkid { get; set; }          // supplied on create; identifies the row on update

    [Required(ErrorMessage = "狀態說明為必填。")]
    [StringLength(50, ErrorMessage = "狀態說明不可超過 50 個字元。")]
    public string Description { get; set; } = string.Empty;

    public bool IsDraft { get; set; }
    public bool IsPublished { get; set; }
    public bool IsDiscontinued { get; set; }
}

// Models/PublishStatusQuery.cs — search DTO
public class PublishStatusQuery
{
    public string? Keyword { get; set; }
    public bool? IsDraft { get; set; }
    public bool? IsPublished { get; set; }
    public bool? IsDiscontinued { get; set; }
}
```

### SQL — SELECT

Shared `SelectColumns` constant, with the two child counts as correlated subqueries (same shape as
`AppRoleRepository.UserCount`):

```sql
SELECT p.pkid, p.Description, p.IsDraft, p.IsPublished, p.IsDiscontinued,
       (SELECT COUNT(*) FROM Course c     WHERE c.PublishStatus_pkid = p.pkid) AS CourseCount,
       (SELECT COUNT(*) FROM Promotion2 m WHERE m.PublishStatus_pkid = p.pkid) AS PromotionCount
FROM PublishStatus p
```

- `GetAllAsync` / `QueryAsync`: append `ORDER BY p.pkid ASC`.
- `GetByPkidAsync`: append `WHERE p.pkid = @Pkid`.
- No JOINs, no multi-map, no `splitOn` — there are no nav objects.
- No `nchar` columns, so **no `RTRIM()` needed**.

### SQL — INSERT

`pkid` **is included** in the column list — it is not an IDENTITY, so it must be written explicitly,
and there is no `SCOPE_IDENTITY()` to select back.

```sql
INSERT INTO PublishStatus (pkid, Description, IsDraft, IsPublished, IsDiscontinued)
VALUES (@Pkid, @Description, @IsDraft, @IsPublished, @IsDiscontinued);
```

`CreateAsync` returns the request's own `Pkid` (a `byte`), not a generated identity.

### SQL — UPDATE

`pkid` is the row identifier and is **immutable** — it is in the `WHERE`, never in the `SET`.

```sql
UPDATE PublishStatus
SET Description = @Description,
    IsDraft = @IsDraft,
    IsPublished = @IsPublished,
    IsDiscontinued = @IsDiscontinued
WHERE pkid = @Pkid;
```

### SQL — existence / guard queries

```sql
-- PkidExistsAsync (create-time duplicate check)
SELECT COUNT(1) FROM PublishStatus WHERE pkid = @Pkid;
```

There is **no separate usage-count query**. The delete guard reuses `GetByPkidAsync`, which the
controller already calls to produce its 404 — its `SelectColumns` computes `CourseCount` and
`PromotionCount`, so the guard reads them off the loaded entity. (`AppRole` has a distinct
`GetUserCountAsync` even though its model also carries `UserCount`; that extra round trip is
redundant and is not copied here.)

### SQL — DELETE

```sql
DELETE FROM PublishStatus WHERE pkid = @Pkid;
```

### N-N Sync Pattern

**N/A**.

### Special Column Notes

- **`pkid` is `tinyint NOT NULL` with NO `IDENTITY`.** This is the single most important deviation
  from every other table scaffolded so far. Consequences, all of which the generated code must honour:
  - `INSERT` writes `pkid` explicitly; there is no `SELECT CAST(SCOPE_IDENTITY() AS int)`.
  - `CreateAsync` returns `byte`, not `int`.
  - Create must reject a duplicate `pkid` with `409` — the DB would otherwise throw a PK violation.
  - The **new** form exposes `pkid` as an editable, required number field (0–255).
  - The **edit** form disables `pkid` (same treatment `AppRole` gives `RoleId`).
- The PK constraint is named `PK_PublishingStatus` ("Publish**ing**Status") while the table is
  `PublishStatus`. A pre-existing naming inconsistency in the DB; it affects nothing in code.
- No `nchar`, no `date`/`time`, no computed columns, no defaults — no type handlers needed.
- **RowAudit is not wired up in this codebase.** The `RowAudit` table exists in `admin.sql` and the
  sample specs mention an `AuditHelper`, but there is no `RowAuditWriter` in `CMS.API` and `AppRole`
  does not log. This feature therefore does **not** log audit rows. Adding auditing is a separate
  cross-cutting task.

---

## Frontend Notes

Files follow the existing `AppRole` layout (`features/{plural}/…`), **not** the `core/models` +
`core/services` layout mentioned in the generic skill text — that structure does not exist in this repo.

### Routes

| Path | Component | Title |
|------|-----------|-------|
| `publish-statuses` | `PublishStatusList` | 發布狀態 PublishStatus |
| `publish-statuses/new` | `PublishStatusForm` | 新增發布狀態 |
| `publish-statuses/:id` | `PublishStatusDetail` | 檢視發布狀態 |
| `publish-statuses/:id/edit` | `PublishStatusForm` | 編輯發布狀態 |

`/new` must be registered **before** `/:id` so it is not swallowed by the param route.

### Angular model

```ts
export interface PublishStatus {
  pkid: number;
  description: string;
  isDraft: boolean;
  isPublished: boolean;
  isDiscontinued: boolean;
  courseCount: number;
  promotionCount: number;
}

export interface PublishStatusRequest {
  pkid: number;
  description: string;
  isDraft: boolean;
  isPublished: boolean;
  isDiscontinued: boolean;
}

export interface PublishStatusQuery {
  keyword?: string;
  isDraft?: boolean | null;
  isPublished?: boolean | null;
  isDiscontinued?: boolean | null;
}
```

PK is numeric, so the service does **not** need `encodeURIComponent`.

### List component

Columns: 主代碼 (pkid), 狀態說明 (description), 草稿 / 已發布 / 已下架 (three boolean columns rendered as
`pi-check` / `pi-times` icons), 課程數 (courseCount), 促銷活動數 (promotionCount), plus the row action
buttons (檢視 / 編輯 / 刪除).

Sortable, paginated `p-table`. Default sort `pkid ASC`.

Filter drawer (`p-drawer`): a keyword input plus three tri-state checkboxes
(`p-checkbox [indeterminate]` or a 3-option `p-select` of 全部 / 是 / 否) for IsDraft, IsPublished,
IsDiscontinued.

No FK dropdowns, so **no `forkJoin` lookup loading is required on init** — the list just loads its own
data.

### Session Storage Keys

| Key | Contents |
|-----|----------|
| `publish-status-list-filters` | Last query filter values |
| `publish-status-list-sort` | `{ sortField, sortOrder }` |
| `publish-status-list-page` | `{ first, rows }` |

No incoming cross-entity query params (nothing navigates *into* this list yet).

### Delete Confirmation

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.description}」？
```

A 409 from the API (still referenced by courses/promotions) is surfaced as an error toast carrying the
server's message.

### Form layout

| Field | Widget | Notes |
|-------|--------|-------|
| 主代碼 pkid | `p-inputnumber` | required, 0–255. **Editable on new, disabled on edit.** |
| 狀態說明 description | `input pInputText` | required, maxlength 50 |
| 草稿 isDraft | `p-checkbox` (binary) | defaults false |
| 已發布 isPublished | `p-checkbox` (binary) | defaults false |
| 已下架 isDiscontinued | `p-checkbox` (binary) | defaults false |

Reactive Forms. No `forkJoin` (no lookups to load). No date pickers.

The three booleans are **not** made mutually exclusive: the schema allows any combination and there is
no CHECK constraint, so the UI must not invent a rule the database does not enforce.

### Special Form Behaviors

- On edit, `pkid` is loaded then the control is `disable()`d — the same pattern `AppRoleForm` uses for
  `RoleId`. Because a disabled control is excluded from `form.value`, the submit handler must read the
  pkid from the route param / loaded entity and merge it into the request payload.

### Sub-panels

**N/A**.

### Sidebar placement

Nav group `系統管理 Admin` **already exists** in `app.ts` (`navGroups`) and holds 角色 AppRole. Append:

```ts
{ label: '發布狀態 PublishStatus', icon: 'pi pi-flag', route: '/publish-statuses' }
```

No new group needed; `app.html` renders groups generically, so it needs no edit.

---

## Tests

### Backend — `src/CMS.API.Tests/`

Existing harness: `CmsApiFactory` (a `WebApplicationFactory`) swaps the real repository for an
in-memory fake, so no SQL Server is needed. Mirror it:

- `Fakes/InMemoryPublishStatusRepository.cs` — implements `IPublishStatusRepository` over a `List<>`,
  including the pkid-exists check and the usage counts.
- `PublishStatusesControllerTests.cs`:
  - `GetAll` returns seeded rows ordered by pkid.
  - `Query` filters by keyword and by each tri-state bool.
  - `GetByPkid` → 200 when found, 404 when not, 404 when id is out of tinyint range (e.g. 999).
  - `Create` → 201; **409 when pkid already exists**; 400 when `Description` is missing.
  - `Update` → 204; 404 for unknown pkid; pkid is not altered.
  - `Delete` → 204 when unused; **409 when CourseCount + PromotionCount > 0**; 404 for unknown pkid.

### Frontend — Karma + Jasmine

- `publish-status.service.spec.ts` — `HttpTestingController`; assert each method hits the right
  URL/verb (`GET /publish-statuses`, `POST /publish-statuses/query`, `PUT /publish-statuses`, …).
- `publish-status-list.spec.ts` — renders rows from a mocked service; filter drawer applies a query.
- `publish-status-detail.spec.ts` — renders a loaded record.
- `publish-status-form.spec.ts` — required-field validation on `description` and `pkid`; **`pkid`
  control is enabled in new mode and disabled in edit mode.**

---

## Files to Create / Modify

| File | Action |
|------|--------|
| `src/CMS.API/Models/PublishStatus.cs` | Create |
| `src/CMS.API/Models/PublishStatusRequest.cs` | Create |
| `src/CMS.API/Models/PublishStatusQuery.cs` | Create |
| `src/CMS.API/Repositories/IPublishStatusRepository.cs` | Create |
| `src/CMS.API/Repositories/PublishStatusRepository.cs` | Create |
| `src/CMS.API/Controllers/PublishStatusesController.cs` | Create |
| `src/CMS.API/Controllers/LookupsController.cs` | **Modify** — add `GET /api/lookups/publish-statuses` |
| `src/CMS.API/Program.cs` | **Modify** — register `IPublishStatusRepository` |
| `src/CMS.API.Tests/Fakes/InMemoryPublishStatusRepository.cs` | Create |
| `src/CMS.API.Tests/PublishStatusesControllerTests.cs` | Create |
| `src/CMS.API.Tests/CmsApiFactory.cs` | **Modify** — swap in the new fake |
| `src/CMS.NG/src/app/features/publish-statuses/publish-status.model.ts` | Create |
| `src/CMS.NG/src/app/features/publish-statuses/publish-status.service.ts` | Create |
| `src/CMS.NG/src/app/features/publish-statuses/publish-status.service.spec.ts` | Create |
| `src/CMS.NG/src/app/features/publish-statuses/publish-status-list/` (ts/html/spec) | Create |
| `src/CMS.NG/src/app/features/publish-statuses/publish-status-detail/` (ts/html/spec) | Create |
| `src/CMS.NG/src/app/features/publish-statuses/publish-status-form/` (ts/html/spec) | Create |
| `src/CMS.NG/src/app/app.routes.ts` | **Modify** — 4 lazy routes |
| `src/CMS.NG/src/app/app.ts` | **Modify** — sidebar item under 系統管理 Admin |
