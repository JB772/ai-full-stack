# Build Spec for CourseGroup
- database schema: `.\database\course.sql`

---

## Summary

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **smallint IDENTITY(1,1)** — DB-generated, C# `short` (same shape as `Partner`) |
| Foreign Keys | **N/A** — `CourseGroup` has no outbound FK columns |
| Required Fields | `Description` (the only non-key column) |
| N-N Relationships | **N/A** — `PartnerCourseGroup` looks like a junction but is not one; see below |
| Primary-Foreign Links | `Course` (**ON DELETE CASCADE** ⚠), `PartnerCourseGroup` |
| Query Filters | keyword (Description) — there is nothing else to filter on |
| Default Sort | `ORDER BY cg.Description ASC, cg.pkid ASC` |

### The one schema fact that dominates this feature

```sql
ALTER TABLE [dbo].[Course] WITH CHECK ADD CONSTRAINT [FK_Course_CourseGroup]
FOREIGN KEY([CourseGroup_pkid]) REFERENCES [dbo].[CourseGroup] ([pkid])
ON DELETE CASCADE          -- <<<< this
```

**`FK_Course_CourseGroup` cascades on delete.** SQL Server will not reject a `DELETE FROM CourseGroup` that
still has courses — it will *silently delete every one of those courses*. And because `FK_CourseInCertification_Course`
and `FK_CourseJobCategories_Course` are themselves `ON DELETE CASCADE`, the deletion keeps travelling: one
click on 刪除 could wipe a course group, all its courses, and all their certification / job-category links.

This inverts the usual reasoning. Elsewhere in the codebase the 409 guard exists so the user gets a readable
message *instead of* a raw FK violation (the DB would have blocked it anyway). Here **the guard is the only
protection that exists** — the database will happily perform the destruction. `CourseCount` is therefore not a
convenience column; it is load-bearing.

`FK_PartnerCourseGroup_CourseGroup` has **no** cascade, so that one behaves normally (it would throw a raw FK
violation without a guard). Both counts block the delete.

---

## Localization

### Chinese Table Name

- CourseGroup: 課程群組
- Description: 課程的分類群組 (如「資料庫」「雲端」)。`Course` 掛在群組之下，`PartnerCourseGroup` 再把群組對應到各合作夥伴。

### Chinese Column Names

- pkid: 主代碼
- Description: 群組說明
- CourseCount: 課程數 (derived)
- PartnerCourseGroupCount: 夥伴課程群組數 (derived)

---

## Required Fields

Required (NOT NULL):
- `Description` — nvarchar(100)

Optional (nullable):
- **None.** `CourseGroup` has exactly two columns and the non-key one is NOT NULL.

**No UNIQUE constraint on `Description`.** The schema declares only `PK_CourseGroup` on `pkid`. Two groups may
legally share a description, so the API adds **no** duplicate-description 409 — that would invent a constraint
the database does not have. (Same reasoning as `Partner.AppKey`.)

---

## Foreign Keys

**N/A** — `CourseGroup` has no FK columns. It is a pure lookup/parent table, so the feature consumes no lookup
endpoints and its form has no dropdowns.

---

## Foreign-Primary Links

**N/A** — no outbound FKs, so there is nothing to navigate to.

---

## Primary-Foreign Links

Two tables point at `CourseGroup.pkid`.

| Child table | FK column | Nullable | FK constraint | On delete | Chinese label |
|-------------|-----------|----------|---------------|-----------|---------------|
| `Course` | `CourseGroup_pkid` | NULL | `FK_Course_CourseGroup` | **CASCADE** ⚠ | 對應課程 |
| `PartnerCourseGroup` | `CourseGroup_pkid` | NOT NULL | `FK_PartnerCourseGroup_CourseGroup` | NO ACTION | 對應夥伴課程群組 |

`Course.CourseGroup_pkid` is nullable, so a course may sit in no group at all — the group is a soft
classification. That makes the cascade even more surprising: the column is optional, yet removing the group it
points at destroys the whole course row rather than nulling the column out.

**No navigation buttons yet.** Neither child feature exists in the app (only `AppRole`, `PublishStatus`, and a
half-built `Partner` backend). Link buttons would point at dead routes, so the detail page shows the child
*counts* — read straight off the response model — and no buttons. Add the buttons when `Course` lands.

---

## N-N Relationships

**N/A — deliberately.** `PartnerCourseGroup` has two FK columns (`Partner_pkid`, `CourseGroup_pkid`) and so
pattern-matches a junction table, but it is not one:

```sql
CREATE TABLE [dbo].[PartnerCourseGroup](
    [pkid]             [int] IDENTITY(1,1) NOT NULL,
    [Partner_pkid]     [smallint] NOT NULL,      -- FK → Partner
    [CourseGroup_pkid] [smallint] NOT NULL,      -- FK → CourseGroup
    [DisplayOrder]     [int] NOT NULL,           -- payload
    [Description]      [nvarchar](100) NOT NULL  -- payload, NOT NULL
)
```

It carries its own NOT NULL payload (`DisplayOrder`, `Description`) and its own surrogate `pkid`. A
`p-multiselect` emits nothing but a list of `Partner_pkid`s, so the delete-then-reinsert sync pattern could not
supply `Description` and every INSERT would fail. `PartnerCourseGroup` is a first-class child entity needing its
own CRUD feature; here it is only a **count + delete guard**. (`spec/course/Partner.md` reaches the same
conclusion from the other side of the same table.)

---

## Query Filters

| Filter | Type | SQL |
|--------|------|-----|
| `Keyword` | `string?` | `LIKE %kw%` on `Description` |

That is the complete list, and it is not an oversight: the table has exactly one non-key column. There are **no
FK columns** (no dropdowns), **no bit columns** (no tri-state toggles), **no date columns** (no range pairs), and
**no nullable columns** (nothing to build a `HasX` tri-state from, unlike `Partner.HasImage`).

The child counts are deliberately **not** filters. They are derived per-row via correlated subquery; filtering on
them would mean a `HAVING`-style predicate over the subquery for a table that will hold a few dozen rows at most.
The user can sort the count columns in the grid instead.

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/course-groups` | **New** | All groups, `ORDER BY Description ASC, pkid ASC`; option label `Description` |

`CourseGroup` is an FK target for `Course` and `PartnerCourseGroup`, so both of those features will need this
dropdown. The CourseGroup feature itself consumes **no** lookups.

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/course-groups` | List all, default sort |
| `POST` | `/api/course-groups/query` | Filtered query (body: `CourseGroupQuery`) |
| `GET` | `/api/course-groups/{id:int}` | Get by pkid. Outside `short` range → 404 |
| `POST` | `/api/course-groups` | Create. 201 + `Location`. 400 on validation failure |
| `PUT` | `/api/course-groups` | Update, **pkid from body**. 400 if `pkid <= 0`, 404 if unknown |
| `DELETE` | `/api/course-groups/{id:int}` | 204; **409** if any child rows exist; 404 if unknown |
| `GET` | `/api/lookups/course-groups` | Slim lookup list |

No auth attributes — the app has no auth pipeline yet.

Route is `{id:int}` (ASP.NET has no `:short` constraint) and the controller range-checks against
`short.MinValue`/`short.MaxValue` before casting, so `/api/course-groups/99999` is a clean 404 rather than an
overflow — the `PartnersController.TryToPkid` shape.

### Delete guard (409) — the important one

Reject the delete if **either** child count is non-zero, and name the offenders:

> 課程群組「資料庫」仍有 12 筆課程、3 筆夥伴課程群組，無法刪除。

The counts come off the entity the controller already loaded for its 404 check — no second round trip
(the `PublishStatus.CourseCount` pattern).

Unlike every other delete guard in this codebase, **this one is not a cosmetic upgrade over a raw SQL error.**
`FK_Course_CourseGroup` is `ON DELETE CASCADE`: without the guard, `DELETE FROM CourseGroup WHERE pkid = 3`
returns `204 No Content` having quietly destroyed every course in group 3. The guard must be checked before the
`DELETE` reaches SQL Server, and `InMemoryCourseGroupRepository` must not be allowed to hide that (it has no
cascade of its own to model, so the *test* for this lives at the controller layer).

---

## Backend Notes

### Models

```csharp
// Models/CourseGroup.cs — response
public class CourseGroup
{
    public short Pkid { get; set; }                       // smallint IDENTITY
    public string Description { get; set; } = string.Empty;

    // Correlated-subquery counts — the list columns and, critically, the delete guard.
    public int CourseCount { get; set; }
    public int PartnerCourseGroupCount { get; set; }
}

// Models/CourseGroupRequest.cs — write DTO
public class CourseGroupRequest
{
    public short Pkid { get; set; }                       // 0 on create; identifies the row on update

    [Required, StringLength(100)] public string Description { get; set; } = string.Empty;
}

// Models/CourseGroupQuery.cs — search DTO
public class CourseGroupQuery
{
    public string? Keyword { get; set; }
}
```

### SQL — SELECT

Shared `SelectColumns` const, used by `GetAllAsync` / `QueryAsync` / `GetByPkidAsync`:

```sql
SELECT cg.pkid, cg.Description,
       (SELECT COUNT(*) FROM Course c             WHERE c.CourseGroup_pkid = cg.pkid) AS CourseCount,
       (SELECT COUNT(*) FROM PartnerCourseGroup p WHERE p.CourseGroup_pkid = cg.pkid) AS PartnerCourseGroupCount
FROM CourseGroup cg
```

No JOINs (no FK nav objects), so no multi-map and no `splitOn`.
No `nchar(n)` columns → **no `RTRIM()` needed**.
No `date` / `time` columns → the `DateOnly` / `TimeOnly` handlers are irrelevant here.

Ordering everywhere: `ORDER BY cg.Description ASC, cg.pkid ASC`.

**Sort decision.** The table has no `DisplayOrder` column, so the template's first preference is unavailable.
`Description` ASC beats `pkid` ASC because `pkid` is a meaningless IDENTITY here (contrast `PublishStatus`, whose
pkid is a hand-assigned semantic code and therefore sorts meaningfully) and because the same ordering feeds the
`/api/lookups/course-groups` dropdown, where alphabetical is what a user scanning for 「雲端」 expects. `pkid` is
the tiebreaker so the order is stable when two groups share a description — which the schema permits.

### SQL — INSERT

```sql
INSERT INTO CourseGroup (Description)
VALUES (@Description);
SELECT CAST(SCOPE_IDENTITY() AS smallint);
```

`pkid` is excluded — IDENTITY-generated. `ExecuteScalarAsync<short>`.

### SQL — UPDATE

```sql
UPDATE CourseGroup
SET Description = @Description
WHERE pkid = @Pkid;
```

`Description` is the only writable column. Nothing foreign-keys to it (children reference `pkid`), so it is
freely editable — there is no immutable business key here, unlike `AppRole.RoleId`.

### SQL — DELETE

```sql
DELETE FROM CourseGroup WHERE pkid = @Pkid
```

Guarded by the controller. See the 409 section — the database will *not* stop this one.

### N-N Sync Pattern

**N/A** — see the N-N section.

### Special Column Notes

- `pkid` is `smallint` → C# `short`; `SCOPE_IDENTITY()` cast to `smallint`; route range-checked.
- **`FK_Course_CourseGroup` is `ON DELETE CASCADE`** — the delete guard is mandatory, not decorative.
- No column-name typos, no computed columns, no `nchar` columns, no nullable columns, no default constraints.
- `RowAudit` is **not** wired up in this repo — no audit rows are written (see CLAUDE.md).

---

## Frontend Notes

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${group.pkid}</b>「${group.description}」？
```

### Session Storage Keys

| Key | Contents |
|-----|----------|
| `course-group-list-filters` | `{ keyword }` |
| `course-group-list-sort` | `{ sortField, sortOrder }` — defaults `description` / `1` |
| `course-group-list-page` | `{ first, rows }` — defaults `0` / `20` |

No incoming cross-entity query params: `CourseGroup` is a top-level parent, and no built feature navigates into it.

### Lookup Binding in List

**N/A** — no FK columns to resolve, so no `forkJoin` on init. The list calls `query()` and renders.

### Route Table

| Route | Component | Title |
|-------|-----------|-------|
| `/course-groups` | `CourseGroupList` | 課程群組 CourseGroup |
| `/course-groups/new` | `CourseGroupForm` | 新增課程群組 |
| `/course-groups/:id` | `CourseGroupDetail` | 檢視課程群組 |
| `/course-groups/:id/edit` | `CourseGroupForm` | 編輯課程群組 |

`/course-groups/new` is registered **before** `/course-groups/:id` so `new` is not swallowed as an id.

### Angular Model

```ts
export interface CourseGroup {
  pkid: number;
  description: string;
  courseCount: number;
  partnerCourseGroupCount: number;
}

export interface CourseGroupRequest {
  pkid: number;              // 0 on create — the DB generates the real key
  description: string;
}

export interface CourseGroupQuery {
  keyword?: string | null;
}
```

### List Component

Columns: 主代碼 · 群組說明 · 課程數 · 夥伴課程群組數 · 操作

Filter drawer: a single 關鍵字 `input pInputText` (placeholder 群組說明), Enter to search.

Default sort `description` ASC, paginator `[10, 20, 50, 100]`, `dataKey="pkid"`.

### Form Layout

| Field | Widget | Validators |
|-------|--------|------------|
| 群組說明 | `input pInputText` | required, maxLength 100 |

`pkid` has **no form control** — it is IDENTITY-generated, unknown on create, and immutable on edit (the
`Partner` shape, not the `PublishStatus` one, where pkid *is* user-supplied and therefore *does* get a control).
In edit mode the detail page shows it read-only.

A one-field form is correct here, not an oversight. Do not pad it.

### Special Form Behaviors

None. No auto-defaulting, no conditional visibility, no `{ emitEvent: false }` guards.

### Detail Page

Shows 主代碼, 群組說明, 課程數, 夥伴課程群組數. When either count is non-zero, a usage note explains the
delete is blocked — and, given the cascade, why that block matters:

> 此課程群組仍被 12 筆課程、3 筆夥伴課程群組引用，無法刪除。

### Sub-panels (edit mode only)

**N/A** — the child entities have no features yet.

### Sidebar Placement

Nav group **課程管理 Course** (icon `pi pi-book`) in `navGroups` in `app.ts`, below the existing 系統管理 Admin
group. The group does not exist yet — `spec/course/Partner.md` calls for it but the Partner frontend was never
built — so this feature **creates** it. `app.html` renders nav groups generically and needs no edit.
Item: `課程群組 CourseGroup`, icon `pi pi-sitemap`, route `/course-groups`.

---

## Tests

### Backend — `CMS.API.Tests`

- `Fakes/InMemoryCourseGroupRepository.cs` — mirrors the SQL semantics: IDENTITY counter starting past the seed
  rows, keyword `Contains` on Description, default sort (Description ASC, pkid ASC), child counts as the delete
  guard. Seeded so one group is unreferenced, one has courses, and one has only PartnerCourseGroup rows.
- **Register it in `CmsApiFactory`** (`RemoveAll<ICourseGroupRepository>()` + `AddSingleton`) — miss this and the
  test host resolves the real Dapper repository and reaches for SQL Server.
- `CourseGroupsControllerTests.cs`: list + default sort; keyword filter (hit, miss, no-filter); get-by-id found /
  not-found / out-of-`short`-range → 404; create 201 with a *generated* pkid; create 400 on a missing
  description; update 204 + persistence, 400 on `pkid <= 0`, 404 on unknown pkid; delete 204 when unreferenced,
  **409 when courses exist** (the cascade case — assert the group is still there afterwards), **409 when only
  PartnerCourseGroup rows exist**, 404 when unknown; lookup endpoint.

### Frontend — `CMS.NG`

- `course-group.service.spec.ts` — each method hits the right URL/verb; `pkid` travels in the PUT **body**, not
  the route. (`pkid` is numeric, so no `encodeURIComponent`.)
- `course-group-list.spec.ts` — loads on init; renders a row per group; keyword filter apply/clear/persist/restore;
  corrupt session storage ignored; delete confirm → reload; 409 message surfaced; navigation.
- `course-group-detail.spec.ts` — renders the fields and both counts; usage note when either count > 0; missing
  group → back to the list.
- `course-group-form.spec.ts` — add mode posts `pkid: 0` and navigates to the created detail page; edit mode loads
  and PUTs with the pkid; required-field validation blocks submit; description is trimmed.

---

## Files to Create / Modify

### Backend

| File | Action |
|------|--------|
| `src/CMS.API/Models/CourseGroup.cs` | Create |
| `src/CMS.API/Models/CourseGroupRequest.cs` | Create |
| `src/CMS.API/Models/CourseGroupQuery.cs` | Create |
| `src/CMS.API/Repositories/ICourseGroupRepository.cs` | Create |
| `src/CMS.API/Repositories/CourseGroupRepository.cs` | Create |
| `src/CMS.API/Controllers/CourseGroupsController.cs` | Create |
| `src/CMS.API/Controllers/LookupsController.cs` | Modify — inject `ICourseGroupRepository`, add `GET /api/lookups/course-groups` |
| `src/CMS.API/Program.cs` | Modify — register `ICourseGroupRepository` → `CourseGroupRepository` |

### Frontend

| File | Action |
|------|--------|
| `src/CMS.NG/src/app/features/course-groups/course-group.model.ts` | Create |
| `src/CMS.NG/src/app/features/course-groups/course-group.service.ts` | Create |
| `src/CMS.NG/src/app/features/course-groups/course-group-list/course-group-list.{ts,html,scss}` | Create |
| `src/CMS.NG/src/app/features/course-groups/course-group-detail/course-group-detail.{ts,html,scss}` | Create |
| `src/CMS.NG/src/app/features/course-groups/course-group-form/course-group-form.{ts,html,scss}` | Create |
| `src/CMS.NG/src/app/app.routes.ts` | Modify — four routes, `/new` before `/:id` |
| `src/CMS.NG/src/app/app.ts` | Modify — new 課程管理 Course nav group |

### Tests

| File | Action |
|------|--------|
| `src/CMS.API.Tests/Fakes/InMemoryCourseGroupRepository.cs` | Create |
| `src/CMS.API.Tests/CourseGroupsControllerTests.cs` | Create |
| `src/CMS.API.Tests/CmsApiFactory.cs` | Modify — swap in the fake |
| `src/CMS.NG/.../course-group.service.spec.ts` | Create |
| `src/CMS.NG/.../course-group-list/course-group-list.spec.ts` | Create |
| `src/CMS.NG/.../course-group-detail/course-group-detail.spec.ts` | Create |
| `src/CMS.NG/.../course-group-form/course-group-form.spec.ts` | Create |
