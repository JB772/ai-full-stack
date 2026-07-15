# Delete guards — return 409, don't let a FK error (or silent orphan) happen

**Deletes that would orphan children return 409**, not a FK exception — check the child count first.
Prefer a correlated-subquery count on the response model (`PublishStatus.CourseCount`) and read the
guard off the entity the controller already loaded for its 404 — no second round trip.

## A parent can have several child tables

`Partner` has five. Count them all, and have the 409 message name only the ones that actually have rows
— see `PartnersController.DescribeBlockers`.

## Guard on what would be *orphaned*, not on what SQL Server would reject

`Seminar.Partner_pkid` points at `Partner` but the schema declares **no FK constraint** for it, so a
delete would silently orphan those rows. `SeminarCount` is in the guard anyway. Grep for `{Table}_pkid`
columns, not just for `FOREIGN KEY` clauses.

## Some FKs are `ON DELETE CASCADE` — there the guard is the *only* protection

Elsewhere the 409 is a readability upgrade: the DB would have rejected the delete anyway and the guard
just turns a raw FK violation into a sentence. **Not so** for `FK_Course_CourseGroup` (and
`FK_CourseInCertification_Course`, `FK_CourseJobCategories_Course`,
`FK_CertificationJobCategories_Certification`). `DELETE FROM CourseGroup WHERE pkid = 2` **succeeds**,
silently destroying every `Course` in the group and then cascading on into *their* children.
`CourseGroupsController.Delete` checks `CourseCount` before the DELETE ever reaches SQL Server, and that
check is load-bearing. **Grep the `ALTER TABLE … ADD CONSTRAINT` block for `ON DELETE CASCADE` before
writing any delete path.**

## Junctions whose editor is deferred still belong in the guard

`Course`'s N-N (`CourseInCertification`, `CourseJobCategories`) has no editor, but both are
`ON DELETE CASCADE` on `Course_pkid`, so `CoursesController.DescribeBlockers` counts them anyway —
otherwise deleting a course would silently destroy the junction rows. The guard spans six children
(FAQ, certifications, job categories, related links, hot-course, and `CourseRecomm` by `CourseId`).
`AppUser`'s deferred `AppUserRole` is the same idea (plain FK, counted as `RoleCount`).
