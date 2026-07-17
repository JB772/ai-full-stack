# Build Spec for Course
- database schema: `.\database\course.sql`

> **Scope note (read `CLAUDE.md`).** `spec/sample1.spec.md` describes a much richer Course than
> this repo actually implements. **RowAudit** logging, inline **QR code**, and the PDF export have
> since shipped on the detail page (存成 PDF spec: `spec/course/CoursePDF.md`). The following
> remain **deliberately out of scope** because no
> reference implementation for them exists in the codebase, and CLAUDE.md warns against inventing them
> while scaffolding a table:
> - **Copy** action and the edit-mode **sub-panels** (`CourseRelatedLink`, `CourseRecomm` inline tables).
> - **N-N** editors for `CourseInCertification` / `CourseJobCategories`. There is no N-N reference
>   feature, and no `certifications` / `job-categories` lookup endpoints. The two junctions are still
>   **counted in the delete guard** (below) so a course that participates in them cannot be silently
>   cascade-deleted; editing membership is a future task.
>
> This spec covers the standard CRUD (list / filter / view / add / edit / delete-guard) over Course's
> scalar columns and its three foreign keys, matching the `Partner` / `CourseGroup` reference features.

---

## Summary

`Course` is the central catalogue entity: a training course offered by a partner, carrying descriptive
content, scheduling dates, pricing and display metadata. It links to `Partner` (required),
`CourseGroup` (optional) and `PublishStatus` (required). It is **the first table in this repo with
outbound FKs**, so it is also the first to carry nav objects resolved by a Dapper multi-map JOIN.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **int IDENTITY** (DB-generated; form has no pkid control) |
| Foreign Keys | `Partner_pkid` → `Partner.pkid` (NOT NULL); `CourseGroup_pkid` → `CourseGroup.pkid` (**nullable**); `PublishStatus_pkid` → `PublishStatus.pkid` (NOT NULL) |
| Required Fields | Title, CourseId, ProdCourseId, FriendlyUrl, DisplayOrder, Partner_pkid, PublishStatus_pkid, ScheduleOn, ScheduleOff, Hour, ListPrice, LearningCredit, CanRepeat |
| N-N Relationships | `CourseInCertification`, `CourseJobCategories` — **counted in delete guard, not editable** (see scope note) |
| Primary-Foreign Links | CourseFAQ, CourseInCertification, CourseJobCategories, CourseRelatedLink, HotCourse, CourseRecomm reference Course — surfaced only as delete-guard counts (no child list pages built yet) |
| Query Filters | keyword (Title/OfficialTitle/CourseId/ProdCourseId/FriendlyUrl); Partner_pkid; CourseGroup_pkid; PublishStatus_pkid; ScheduleOn range; ScheduleOff range; CanRepeat |
| Default Sort | `DisplayOrder ASC, pkid ASC` |

---

## Localization

### Chinese Table Name

- Course: 課程
- Description: 訓練課程主資料

### Chinese Column Names

- pkid: 主代碼
- DisplayOrder: 顯示順序
- CourseId: 簡介代碼
- ProdCourseId: 科目代碼
- Title: 課程名稱
- OfficialTitle: 官方課程名稱
- Partner_pkid: 原廠 (Partner.Name)
- CourseGroup_pkid: 課程群組 (CourseGroup.Description)
- PublishStatus_pkid: 上架狀態 (PublishStatus.Description)
- ScheduleOn: 上架日期
- ScheduleOff: 下架日期
- Hour: 時數
- ListPrice: 定價
- LearningCredit: 點數
- FriendlyUrl: 友善網址
- Material: 教材
- Objective: 課程目標
- Target: 適合對象
- Prerequisites: 先備知識
- Outline: 課程大綱
- TowardCertOrExam: 考試／認證說明
- Note: 備註
- OtherInfo: 其他資訊
- CanRepeat: 允許重聽

---

## Required Fields

Required (NOT NULL, excluding IDENTITY PK): `Title`, `CourseId`, `ProdCourseId`, `FriendlyUrl`,
`DisplayOrder`, `Partner_pkid`, `PublishStatus_pkid`, `ScheduleOn`, `ScheduleOff`, `Hour`,
`ListPrice`, `LearningCredit`, `CanRepeat`.

Optional (nullable): `OfficialTitle`, `CourseGroup_pkid`, `Material`, `Objective`, `Target`,
`Prerequisites`, `Outline`, `TowardCertOrExam`, `Note`, `OtherInfo`.

---

## Foreign Keys

Resolved by a multi-map JOIN in every SELECT; the response model exposes them as nav objects
(`partner`, `courseGroup`, `publishStatus`) so the list and detail pages show labels, not raw ids.

- **Partner_pkid** → `Partner.pkid` (NOT NULL). Alias `PartnerPkid`. Nav label = `Name`.
  Dropdown source `GET /api/lookups/partners`, ordered by `DisplayOrder`.
- **CourseGroup_pkid** → `CourseGroup.pkid` (**nullable** — `LEFT JOIN`, "無" option). Alias
  `CourseGroupPkid`. Nav label = `Description`. Source `GET /api/lookups/course-groups`.
- **PublishStatus_pkid** → `PublishStatus.pkid` (NOT NULL). Alias `PublishStatusPkid`. Nav label =
  `Description`. Source `GET /api/lookups/publish-statuses`.

## Foreign-Primary Links

Not built as clickable cross-links in this pass (the reference features don't yet establish the
pattern for outbound FK links). The nav labels are shown read-only in list and detail.

## Primary-Foreign Links

The tables that reference `Course` (`CourseFAQ`, `CourseInCertification`, `CourseJobCategories`,
`CourseRelatedLink`, `HotCourse`, and `CourseRecomm` via `CourseId`) have no list pages yet, so no
navigation buttons are rendered. Their row counts are surfaced on the detail page and drive the
delete guard.

---

## N-N Relationships

`CourseInCertification` and `CourseJobCategories` are genuine junctions, but **membership editing is
out of scope** (see scope note). Both are `ON DELETE CASCADE` on `Course_pkid`, so the delete guard
counts them — otherwise deleting a course would silently destroy its junction rows.

---

## Query Filters (`POST /api/courses/query`)

- **keyword**: LIKE across `Title`, `OfficialTitle`, `CourseId`, `ProdCourseId`, `FriendlyUrl`
  (short identifying columns only — the large `nvarchar(max)`/`nvarchar(4000)` bodies are excluded).
- **partnerPkid** (`short?`): exact match on `Partner_pkid`.
- **courseGroupPkid** (`short?`): exact match on `CourseGroup_pkid`.
- **publishStatusPkid** (`byte?`): exact match on `PublishStatus_pkid`.
- **scheduleOnFrom / scheduleOnTo** (`DateOnly?`): inclusive range on `ScheduleOn`.
- **scheduleOffFrom / scheduleOffTo** (`DateOnly?`): inclusive range on `ScheduleOff`.
- **canRepeat** (`bool?`): tri-state exact match on `CanRepeat` (null = 不篩選).

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/partners` | Exists | Partner list (ordered by DisplayOrder) |
| `GET /api/lookups/course-groups` | Exists | CourseGroup list |
| `GET /api/lookups/publish-statuses` | Exists | PublishStatus list |

No new lookup endpoint for Course itself — nothing we are building consumes it yet.

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/courses` | List all (nav objects populated) |
| `POST` | `/api/courses/query` | Filtered query (body: `CourseQuery`) |
| `GET` | `/api/courses/{id:int}` | Get by pkid |
| `POST` | `/api/courses` | Create (pkid DB-generated) → 201 |
| `PUT` | `/api/courses` | Update (pkid from body; `<= 0` → 400; unknown → 404) |
| `DELETE` | `/api/courses/{id:int}` | Delete; 409 if any child rows |

`{id:int}` binds directly — pkid is a real `int` IDENTITY, so no smallint/tinyint range-check is
needed (contrast `Partner` / `PublishStatus`).

---

## Backend Notes

### Models

`Course` response model: all scalar columns; `PartnerPkid` / `CourseGroupPkid?` / `PublishStatusPkid`;
nav objects `Partner { pkid, name }`, `CourseGroup { pkid, description }?`,
`PublishStatus { pkid, description }`; six delete-guard counts (`CourseFaqCount`,
`CertificationCount`, `JobCategoryCount`, `RelatedLinkCount`, `HotCourseCount`, `RecommCount`).

`CourseRequest` write DTO: `Pkid` + every writable column, with data annotations mirroring the schema
lengths / nullability. `CourseQuery`: the filters above.

### SQL — SELECT (multi-map)

```sql
SELECT c.pkid, c.Title, c.OfficialTitle, c.CourseId, c.ProdCourseId, c.FriendlyUrl, c.DisplayOrder,
       c.Partner_pkid AS PartnerPkid, c.CourseGroup_pkid AS CourseGroupPkid,
       c.PublishStatus_pkid AS PublishStatusPkid, c.ScheduleOn, c.ScheduleOff, c.Hour, c.ListPrice,
       c.LearningCredit, c.Material, c.Objective, c.Target, c.Prerequisites, c.Outline,
       c.TowardCertOrExam, c.Note, c.OtherInfo, c.CanRepeat,
       (SELECT COUNT(*) FROM CourseFAQ f            WHERE f.Course_pkid = c.pkid) AS CourseFaqCount,
       (SELECT COUNT(*) FROM CourseInCertification i WHERE i.Course_pkid = c.pkid) AS CertificationCount,
       (SELECT COUNT(*) FROM CourseJobCategories j   WHERE j.Course_pkid = c.pkid) AS JobCategoryCount,
       (SELECT COUNT(*) FROM CourseRelatedLink r     WHERE r.Course_pkid = c.pkid) AS RelatedLinkCount,
       (SELECT COUNT(*) FROM HotCourse h             WHERE h.Course_pkid = c.pkid) AS HotCourseCount,
       (SELECT COUNT(*) FROM CourseRecomm cr WHERE cr.CourseId = c.CourseId OR cr.RecommCourseId = c.CourseId) AS RecommCount,
       p.pkid, p.Name,
       cg.pkid, cg.Description,
       ps.pkid, ps.Description
FROM Course c
JOIN Partner p        ON p.pkid  = c.Partner_pkid
LEFT JOIN CourseGroup cg ON cg.pkid = c.CourseGroup_pkid
JOIN PublishStatus ps ON ps.pkid = c.PublishStatus_pkid
```

`splitOn: "pkid,pkid,pkid"`; the nullable `LEFT JOIN` yields a null `CourseGroup` nav object when the
FK is null. `ScheduleOn` / `ScheduleOff` are `date` → `DateOnly` (handlers already in `Program.cs`).

### SQL — INSERT / UPDATE

INSERT writes every non-key column and `SELECT CAST(SCOPE_IDENTITY() AS int)`. UPDATE sets the same
columns `WHERE pkid = @Pkid`. No `nchar` columns on Course, so no `RTRIM()` needed.

### Delete guard

`CoursesController.Delete` loads the entity (for its 404), then blocks with **409** if any of the six
counts is non-zero, naming only the non-empty children (same shape as
`PartnersController.DescribeBlockers`). This is load-bearing for the two cascade junctions.

---

## Frontend Notes

### List

`p-table` columns per the request: 主代碼, 顯示順序, 簡介代碼, 科目代碼, 課程名稱, 原廠
(`partner.name`), 課程群組 (`courseGroup.description`), 上架狀態 (`publishStatus.description`),
上架日期, 下架日期, 時數, 定價, 點數, 允許重聽, plus a 操作 column. Default sort `displayOrder ASC`.
Filter drawer holds keyword, the three FK `p-select`s, both date ranges, and the tri-state 允許重聽.
Lookups for the drawer load via `forkJoin(PartnerService.getAll, CourseGroupService.getAll,
PublishStatusService.getAll)`; restore saved filters after they resolve.

Because the default sort is `displayOrder` (not `pkid`), component specs must return a fresh array per
`query()` call and assert rows by text, never by index (the `p-table` in-place-sort gotcha).

### Form

Reactive form over all writable columns. FK `p-select`s populated by the same `forkJoin`. Dates are
`p-datepicker` (`Date` ⇄ `yyyy-MM-dd` via local-component helper, never `toISOString`). `CanRepeat`
is a `p-checkbox [binary]`. Long text uses `textarea pTextarea`; `Outline` / `TowardCertOrExam` are
`nvarchar(max)` (no maxlength). No pkid control — the key is IDENTITY-generated. `CourseGroup` select
includes a "無" (null) option.

### Session Storage Keys

`course-list-filters`, `course-list-sort`, `course-list-page`.

### Delete Confirmation

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.title}」？
```

---

## Files to create / modify

**Backend:** `Models/Course.cs`, `Models/CourseRequest.cs`, `Models/CourseQuery.cs`,
`Repositories/ICourseRepository.cs`, `Repositories/CourseRepository.cs`,
`Controllers/CoursesController.cs`, register in `Program.cs`.

**Frontend:** `features/courses/course.model.ts`, `course.service.ts`,
`course-list/`, `course-detail/`, `course-form/`, routes in `app.routes.ts`, sidebar item in `app.ts`.

**Tests:** `CMS.API.Tests/Fakes/InMemoryCourseRepository.cs`, `CMS.API.Tests/CoursesControllerTests.cs`,
register in `CmsApiFactory.cs`; `course.service.spec.ts`, `course-list.spec.ts`,
`course-detail.spec.ts`, `course-form.spec.ts`.
