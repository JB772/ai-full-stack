# Outbound FKs, nav objects & N-N relationships

## `Course` is the reference for outbound FKs (Dapper multi-map)

`Course` is the first table with outbound FKs, so it is the first to multi-map nav objects. Every other
built table (AppRole, AppUser, PublishStatus, Partner, CourseGroup) has *no* outbound FK, so before
Course there was no multi-map anywhere. `CourseRepository` JOINs Partner / CourseGroup / PublishStatus
and returns nested nav objects (`course.partner.name`, etc.). Points that bite:

- The response model carries **purpose-built slim nav classes** (`CourseNavPartner { Pkid, Name }`,
  `CourseNavCourseGroup`, `CourseNavPublishStatus`) — *not* the full `Partner` / `CourseGroup` models.
  Reusing the full models would drag their own count subqueries into the JOIN for nothing.
- `splitOn: "pkid,pkid,pkid"` — one entry per nav object. Keep all Course columns (scalars, aliased
  `Partner_pkid AS PartnerPkid`, the six count subqueries) *before* the first `p.pkid` in the SELECT, or
  the split lands in the wrong place.
- `CourseGroup_pkid` is **nullable → `LEFT JOIN`**, and Dapper yields a **null** nav object (not an empty
  one) when the id is null. `GetByPkidAsync` uses `QueryAsync(...).SingleOrDefault()`, not
  `QuerySingleOrDefaultAsync`, because the multi-map overload has no single-row variant.
- Frontend: **there is no lookup service.** The list filter drawer and the form dropdowns load their FK
  options by `forkJoin`-ing the existing feature services (`PartnerService.getAll()`,
  `CourseGroupService.getAll()`, `PublishStatusService.getAll()`). The list/detail **display** labels
  come straight off the nav objects, so they don't depend on the lookups resolving.
- `date` columns (`ScheduleOn` / `ScheduleOff`) ⇄ PrimeNG `p-datepicker` go through
  `features/courses/date.util.ts` (`toIsoDate` / `fromIsoDate`, **local** components — never
  `toISOString()`, which shifts to UTC and lands the wrong day for UTC+8).

## N-N editing is deferred everywhere — but count the junctions in the delete guard

No feature builds an N-N membership editor yet. See `delete-guards.md` for `Course`'s cascade junctions
and `AppUser`'s `AppUserRole`.

## Two FK columns don't make a junction table

`PartnerCourseGroup` looks like a Partner ↔ CourseGroup N-N, but it also carries `DisplayOrder` and
`Description nvarchar(100) NOT NULL`. A `p-multiselect` emits only a list of ids, so the
delete-then-reinsert sync could never supply `Description` and every INSERT would fail. It is a child
entity needing its own CRUD, not an N-N picker. **Check for payload columns before scaffolding an N-N.**
