# Build Spec for Partner
- database schema: `.\database\course.sql` (also scripted identically in `.\database\promotion.sql`)

---

## Summary

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **smallint IDENTITY(1,1)** — DB-generated (unlike `PublishStatus`), C# `short` |
| Foreign Keys | **N/A** — `Partner` has no outbound FK columns |
| Required Fields | `Name`, `AppKey`, `NameOnPartnerMenu`, `NameOnCourseDetailPage`, `DisplayOrder` |
| N-N Relationships | **N/A** — see the `PartnerCourseGroup` note below; it is a child entity, not a junction |
| Primary-Foreign Links | `Course`, `Certification`, `PartnerCourseGroup`, `Promotion2` (FK-enforced); `Seminar` (not enforced) |
| Query Filters | keyword (Name / AppKey / NameOnPartnerMenu / NameOnCourseDetailPage), DisplayOrder range, HasImage tri-state |
| Default Sort | `ORDER BY p.DisplayOrder ASC, p.pkid ASC` |

### Two schema facts that drive the whole design

1. **`pkid` is a real IDENTITY.** Follow `AppRole`, not `PublishStatus`: `SELECT CAST(SCOPE_IDENTITY() AS smallint)`
   on INSERT, no caller-supplied pkid, no duplicate-pkid 409, and the `pkid <= 0` → 400 guard on PUT is
   valid here (IDENTITY seeds at 1, so 0 is never a legal key).

2. **`smallint`, not `int`.** C# `short`. The route is `{id:int}` (ASP.NET has no `:short` constraint) and the
   controller range-checks against `short.MinValue`/`short.MaxValue` before casting, so `/api/partners/99999`
   is a clean 404 rather than an overflow — the same shape as `PublishStatusesController.TryToPkid`.

---

## Localization

### Chinese Table Name

- Partner: 合作夥伴
- Description: 課程的合作夥伴／原廠 (如 Microsoft、Cisco)。課程、認證、研討會與促銷活動都掛在夥伴之下。

### Chinese Column Names

- pkid: 主代碼
- Name: 名稱
- AppKey: 應用代碼
- NameOnPartnerMenu: 夥伴選單顯示名稱
- NameOnCourseDetailPage: 課程明細頁顯示名稱
- DisplayOrder: 顯示順序
- ImageFilename: 圖片檔名
- CourseCount: 課程數 (derived)
- CertificationCount: 認證數 (derived)
- CourseGroupCount: 課程群組數 (derived)
- PromotionCount: 促銷活動數 (derived)
- SeminarCount: 研討會數 (derived)

---

## Required Fields

Required (NOT NULL):
- `Name` — nvarchar(50)
- `AppKey` — varchar(10)
- `NameOnPartnerMenu` — nvarchar(200)
- `NameOnCourseDetailPage` — nvarchar(50)
- `DisplayOrder` — int

Optional (nullable):
- `ImageFilename` — varchar(50)

**No UNIQUE constraint on `AppKey`.** The schema declares only `PK_Partner` on `pkid`. `AppKey` reads like a
natural code, but the database does not enforce uniqueness on it, so the API does **not** add a duplicate-AppKey
409 guard — that would invent a constraint the DB does not have. (Contrast `AppRole.RoleId`, which *is* a
clustered PK and therefore *does* get a 409.) If uniqueness is actually wanted, it belongs in the schema first.

---

## Foreign Keys

**N/A** — `Partner` has no FK columns. It is a pure lookup/parent table.

---

## Foreign-Primary Links

**N/A** — no outbound FKs, so there is nothing to navigate to.

---

## Primary-Foreign Links

Five tables point at `Partner.pkid`. Four are FK-enforced; one is not.

| Child table | FK column | Nullable | FK constraint | Chinese label |
|-------------|-----------|----------|---------------|---------------|
| `Course` | `Partner_pkid` | NOT NULL | `FK_Course_Partner` | 對應課程 |
| `Certification` | `Partner_pkid` | NOT NULL | `FK_Certification_Partner` | 對應認證 |
| `PartnerCourseGroup` | `Partner_pkid` | NOT NULL | `FK_PartnerCourseGroup_Partner` | 對應課程群組 |
| `Promotion2` | `RelatedPartner_pkid` | NULL | `FK_Promotion2_Partner` | 對應促銷活動 |
| `Seminar` | `Partner_pkid` | NULL | **none** | 對應研討會 |

`Seminar.Partner_pkid` is a *soft* reference: the column exists and clearly points at `Partner`, but
`promotion.sql` declares no `FK_Seminar_Partner`. SQL Server would therefore happily let a `Partner` delete
orphan a `Seminar` row. The delete guard counts it anyway (see below).

**No navigation buttons yet.** None of these five child features exist in the app — only `AppRole` and
`PublishStatus` are built. Link buttons would point at dead routes, so the detail page shows the child *counts*
(read straight off the response model) and no buttons. Add the buttons when the child features land.

---

## N-N Relationships

**N/A — deliberately.**

`PartnerCourseGroup` looks like a Partner ↔ CourseGroup junction, and a naive scaffold would render it as a
`p-multiselect` on the Partner form. It is not one:

```sql
CREATE TABLE [dbo].[PartnerCourseGroup](
    [pkid]             [int] IDENTITY(1,1) NOT NULL,
    [Partner_pkid]     [smallint] NOT NULL,   -- FK → Partner
    [CourseGroup_pkid] [smallint] NOT NULL,   -- FK → CourseGroup
    [DisplayOrder]     [int] NOT NULL,        -- payload
    [Description]      [nvarchar](100) NOT NULL -- payload, NOT NULL
)
```

It carries its own NOT NULL payload (`DisplayOrder`, `Description`). A multi-select emits nothing but a list of
`CourseGroup_pkid`s, so the delete-then-reinsert sync pattern could not supply `Description` and every INSERT
would fail. `PartnerCourseGroup` is a first-class child entity that needs its own CRUD feature; here it is only
a **count + delete guard**.

---

## Query Filters

| Filter | Type | SQL |
|--------|------|-----|
| `Keyword` | `string?` | `LIKE %kw%` on `Name`, `AppKey`, `NameOnPartnerMenu`, `NameOnCourseDetailPage` |
| `DisplayOrderFrom` | `int?` | `DisplayOrder >= @DisplayOrderFrom` |
| `DisplayOrderTo` | `int?` | `DisplayOrder <= @DisplayOrderTo` |
| `HasImage` | `bool?` | `true` → `ImageFilename IS NOT NULL`; `false` → `IS NULL`; `null` → no filter |

- **Keyword** covers all four short identifying strings. `ImageFilename` is excluded — it is a storage
  filename, not something a user searches by.
- **No FK dropdowns** (no FK columns), **no bool columns**, **no date columns** — so no date-range filters.
- **`HasImage`** is a tri-state derived from the one nullable column, mirroring the `PublishStatus` tri-state
  pattern (`null = 不篩選`).

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/partners` | **New** | All partners, `ORDER BY DisplayOrder ASC, pkid ASC`; option label `Name` |

`Partner` is an FK target for `Course`, `Certification`, `PartnerCourseGroup`, `Promotion2` and `Seminar`, so
those features will need this dropdown. The Partner feature itself consumes **no** lookups.

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/partners` | List all, default sort |
| `POST` | `/api/partners/query` | Filtered query (body: `PartnerQuery`) |
| `GET` | `/api/partners/{id:int}` | Get by pkid. Outside `short` range → 404 |
| `POST` | `/api/partners` | Create. 201 + `Location`. 400 on validation failure |
| `PUT` | `/api/partners` | Update, **pkid from body**. 400 if `pkid <= 0`, 404 if unknown |
| `DELETE` | `/api/partners/{id:int}` | 204; **409** if any child rows exist; 404 if unknown |
| `GET` | `/api/lookups/partners` | Slim lookup list |

No auth attributes — the app has no auth pipeline yet.

### Delete guard (409)

Reject the delete if **any** of the five child counts is non-zero, and name the offenders in the message:

> 合作夥伴「Microsoft」仍有 12 筆課程、3 筆認證，無法刪除。

The counts come off the entity the controller already loaded for its 404 check — no second round trip
(the `PublishStatus.CourseCount` pattern). `SeminarCount` is included in the guard even though no FK enforces
it: a delete would silently orphan those rows, and CLAUDE.md's rule is "deletes that would orphan children
return 409", not "deletes that SQL Server would reject".

---

## Backend Notes

### Models

```csharp
// Models/Partner.cs — response
public class Partner
{
    public short Pkid { get; set; }                                  // smallint IDENTITY
    public string Name { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
    public string NameOnPartnerMenu { get; set; } = string.Empty;
    public string NameOnCourseDetailPage { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string? ImageFilename { get; set; }

    // Correlated-subquery counts — drive the list columns and the delete guard.
    public int CourseCount { get; set; }
    public int CertificationCount { get; set; }
    public int CourseGroupCount { get; set; }
    public int PromotionCount { get; set; }
    public int SeminarCount { get; set; }
}

// Models/PartnerRequest.cs — write DTO
public class PartnerRequest
{
    public short Pkid { get; set; }                                  // 0 on create; identifies the row on update

    [Required, StringLength(50)]  public string Name { get; set; } = string.Empty;
    [Required, StringLength(10)]  public string AppKey { get; set; } = string.Empty;
    [Required, StringLength(200)] public string NameOnPartnerMenu { get; set; } = string.Empty;
    [Required, StringLength(50)]  public string NameOnCourseDetailPage { get; set; } = string.Empty;
    [Range(0, int.MaxValue)]      public int DisplayOrder { get; set; }
    [StringLength(50)]            public string? ImageFilename { get; set; }
}

// Models/PartnerQuery.cs — search DTO
public class PartnerQuery
{
    public string? Keyword { get; set; }
    public int? DisplayOrderFrom { get; set; }
    public int? DisplayOrderTo { get; set; }
    public bool? HasImage { get; set; }
}
```

### SQL — SELECT

Shared `SelectColumns` const, used by `GetAllAsync` / `QueryAsync` / `GetByPkidAsync`:

```sql
SELECT p.pkid, p.Name, p.AppKey, p.NameOnPartnerMenu, p.NameOnCourseDetailPage,
       p.DisplayOrder, p.ImageFilename,
       (SELECT COUNT(*) FROM Course c             WHERE c.Partner_pkid = p.pkid)        AS CourseCount,
       (SELECT COUNT(*) FROM Certification ct     WHERE ct.Partner_pkid = p.pkid)       AS CertificationCount,
       (SELECT COUNT(*) FROM PartnerCourseGroup g WHERE g.Partner_pkid = p.pkid)        AS CourseGroupCount,
       (SELECT COUNT(*) FROM Promotion2 m         WHERE m.RelatedPartner_pkid = p.pkid) AS PromotionCount,
       (SELECT COUNT(*) FROM Seminar s            WHERE s.Partner_pkid = p.pkid)        AS SeminarCount
FROM Partner p
```

No JOINs (no FK nav objects), so no multi-map and no `splitOn`.
No `nchar(n)` columns on `Partner` → **no `RTRIM()` needed** (note `Certification.Title` *is* `nchar(100)` and
will need it when that feature is built — it is only counted here).
No `date` / `time` columns → the `DateOnly` / `TimeOnly` handlers are irrelevant to this table.

Ordering everywhere: `ORDER BY p.DisplayOrder ASC, p.pkid ASC`.

### SQL — INSERT

```sql
INSERT INTO Partner (Name, AppKey, NameOnPartnerMenu, NameOnCourseDetailPage, DisplayOrder, ImageFilename)
VALUES (@Name, @AppKey, @NameOnPartnerMenu, @NameOnCourseDetailPage, @DisplayOrder, @ImageFilename);
SELECT CAST(SCOPE_IDENTITY() AS smallint);
```

`pkid` is excluded — it is IDENTITY-generated. `ExecuteScalarAsync<short>`.

### SQL — UPDATE

```sql
UPDATE Partner
SET Name = @Name,
    AppKey = @AppKey,
    NameOnPartnerMenu = @NameOnPartnerMenu,
    NameOnCourseDetailPage = @NameOnCourseDetailPage,
    DisplayOrder = @DisplayOrder,
    ImageFilename = @ImageFilename
WHERE pkid = @Pkid;
```

Every non-key column is writable. Unlike `AppRole.RoleId` / `PublishStatus.pkid`, **`Partner` has no immutable
business key** — `AppKey` is freely editable, because nothing foreign-keys to it (children reference `pkid`).

### N-N Sync Pattern

**N/A** — see the N-N section.

### Special Column Notes

- `pkid` is `smallint` → C# `short`; `SCOPE_IDENTITY()` cast to `smallint`; route range-checked.
- No column-name typos, no computed columns, no `nchar` columns, no default constraints that differ from C#.
- `RowAudit` is **not** wired up in this repo — no audit rows are written (see CLAUDE.md).

---

## Frontend Notes

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${partner.pkid}</b>「${partner.name}」？
```

### Session Storage Keys

| Key | Contents |
|-----|----------|
| `partner-list-filters` | `{ keyword, displayOrderFrom, displayOrderTo, hasImage }` |
| `partner-list-sort` | `{ sortField, sortOrder }` — defaults `displayOrder` / `1` |
| `partner-list-page` | `{ first, rows }` — defaults `0` / `20` |

No incoming cross-entity query params: `Partner` is a top-level parent, and no built feature navigates into it.

### Lookup Binding in List

**N/A** — no FK columns to resolve, so no `forkJoin` on init. The list calls `query()` and renders.

### Route Table

| Route | Component | Title |
|-------|-----------|-------|
| `/partners` | `PartnerList` | 合作夥伴 Partner |
| `/partners/new` | `PartnerForm` | 新增合作夥伴 |
| `/partners/:id` | `PartnerDetail` | 檢視合作夥伴 |
| `/partners/:id/edit` | `PartnerForm` | 編輯合作夥伴 |

`/partners/new` is registered **before** `/partners/:id` so `new` is not swallowed as an id.

### Angular Model

```ts
export interface Partner {
  pkid: number;
  name: string;
  appKey: string;
  nameOnPartnerMenu: string;
  nameOnCourseDetailPage: string;
  displayOrder: number;
  imageFilename: string | null;
  courseCount: number;
  certificationCount: number;
  courseGroupCount: number;
  promotionCount: number;
  seminarCount: number;
}

export interface PartnerRequest {
  pkid: number;              // 0 on create — the DB generates the real key
  name: string;
  appKey: string;
  nameOnPartnerMenu: string;
  nameOnCourseDetailPage: string;
  displayOrder: number;
  imageFilename: string | null;
}

export interface PartnerQuery {
  keyword?: string | null;
  displayOrderFrom?: number | null;
  displayOrderTo?: number | null;
  hasImage?: boolean | null;   // tri-state: null = 不篩選
}
```

### List Component

Columns: 主代碼 · 名稱 · 應用代碼 · 夥伴選單顯示名稱 · 顯示順序 · 圖片檔名 · 課程數 · 認證數 · 操作
(`NameOnCourseDetailPage` and the three lower-traffic counts stay on the detail page — the list would be too wide).

Filter drawer: keyword `input`, 顯示順序 from/to `p-inputnumber` pair, 有圖片 `p-select` tri-state
(`appendTo="body"`, `[options]="triStateOptions"`).

Default sort `displayOrder` ASC, paginator `[10, 20, 50, 100]`, `dataKey="pkid"`.

### Form Layout

| Field | Widget | Validators |
|-------|--------|------------|
| 名稱 | `input pInputText` | required, maxLength 50 |
| 應用代碼 | `input pInputText` | required, maxLength 10 |
| 夥伴選單顯示名稱 | `input pInputText` | required, maxLength 200 |
| 課程明細頁顯示名稱 | `input pInputText` | required, maxLength 50 |
| 顯示順序 | `p-inputnumber` | required, min 0 |
| 圖片檔名 | `input pInputText` | optional, maxLength 50 |

`pkid` has **no form control** — it is IDENTITY-generated, unknown on create, and immutable on edit. This is the
one place `Partner` diverges hardest from `PublishStatus`, whose form *does* expose `pkid`. In edit mode the
detail page shows it read-only.

Empty `imageFilename` is sent as `null`, not `''`, so the nullable column stays NULL.

### Special Form Behaviors

None. No auto-defaulting, no conditional visibility, no `{ emitEvent: false }` guards.

### Sub-panels (edit mode only)

**N/A** — the child entities have no features yet.

### Sidebar Placement

New nav group **課程管理 Course** (icon `pi pi-book`), added to `navGroups` in `app.ts` below the existing
系統管理 Admin group. `app.html` renders groups generically and needs no edit.
Item: `合作夥伴 Partner`, icon `pi pi-building`, route `/partners`.

---

## Tests

### Backend — `CMS.API.Tests`

- `Fakes/InMemoryPartnerRepository.cs` — mirrors the SQL semantics: IDENTITY counter, keyword LIKE across the
  four string columns, DisplayOrder range, HasImage tri-state, default sort, child counts as the delete guard.
- **Register it in `CmsApiFactory`** (`RemoveAll<IPartnerRepository>()` + `AddSingleton`) — miss this and the
  test host resolves the real Dapper repository and reaches for SQL Server.
- `PartnersControllerTests.cs`: list + default sort; each query filter; get-by-id found / not-found /
  out-of-`short`-range → 404; create 201 with a *generated* pkid; create 400 on missing required fields;
  update 204 + persistence, 400 on `pkid <= 0`, 404 on unknown pkid; delete 204 when unreferenced,
  **409 when any child count is non-zero** (incl. the Seminar-only case), 404 when unknown; lookup endpoint.

### Frontend — `CMS.NG`

- `partner.service.spec.ts` — each method hits the right URL/verb; `pkid` travels in the PUT **body**, not the
  route. (`pkid` is numeric, so no `encodeURIComponent`.)
- `partner-list.spec.ts` — loads on init; renders a row per partner; filter apply/clear/persist/restore;
  corrupt session storage ignored; delete confirm → reload; 409 message surfaced; navigation.
- `partner-detail.spec.ts` — renders all fields and the five counts; usage note when any count > 0; missing
  partner → back to the list.
- `partner-form.spec.ts` — add mode posts without a pkid and navigates to the created detail page; edit mode
  loads and PUTs with the pkid; required-field validation blocks submit; empty imageFilename → `null`.

---

## Files to Create / Modify

### Backend

| File | Action |
|------|--------|
| `src/CMS.API/Models/Partner.cs` | Create |
| `src/CMS.API/Models/PartnerRequest.cs` | Create |
| `src/CMS.API/Models/PartnerQuery.cs` | Create |
| `src/CMS.API/Repositories/IPartnerRepository.cs` | Create |
| `src/CMS.API/Repositories/PartnerRepository.cs` | Create |
| `src/CMS.API/Controllers/PartnersController.cs` | Create |
| `src/CMS.API/Controllers/LookupsController.cs` | Modify — inject `IPartnerRepository`, add `GET /api/lookups/partners` |
| `src/CMS.API/Program.cs` | Modify — register `IPartnerRepository` → `PartnerRepository` |

### Frontend

| File | Action |
|------|--------|
| `src/CMS.NG/src/app/features/partners/partner.model.ts` | Create |
| `src/CMS.NG/src/app/features/partners/partner.service.ts` | Create |
| `src/CMS.NG/src/app/features/partners/partner-list/partner-list.{ts,html,scss}` | Create |
| `src/CMS.NG/src/app/features/partners/partner-detail/partner-detail.{ts,html,scss}` | Create |
| `src/CMS.NG/src/app/features/partners/partner-form/partner-form.{ts,html,scss}` | Create |
| `src/CMS.NG/src/app/app.routes.ts` | Modify — four routes, `/new` before `/:id` |
| `src/CMS.NG/src/app/app.ts` | Modify — new 課程管理 Course nav group |

### Tests

| File | Action |
|------|--------|
| `src/CMS.API.Tests/Fakes/InMemoryPartnerRepository.cs` | Create |
| `src/CMS.API.Tests/PartnersControllerTests.cs` | Create |
| `src/CMS.API.Tests/CmsApiFactory.cs` | Modify — swap in the fake |
| `src/CMS.NG/.../partner.service.spec.ts` | Create |
| `src/CMS.NG/.../partner-list/partner-list.spec.ts` | Create |
| `src/CMS.NG/.../partner-detail/partner-detail.spec.ts` | Create |
| `src/CMS.NG/.../partner-form/partner-form.spec.ts` | Create |
