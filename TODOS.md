# TODOS

## P3 — Generalize the course print/archive pattern to other entities

- **What:** Extract the Course print/archive view (print header block, paper CSS rules,
  completeness-test pattern) into a reusable shape for other detail pages (Partner,
  CourseGroup, AppUser…).
- **Why:** The 2026-07-16 course-PDF review predicted the "do this for X too" ask arrives
  once admins see the Course archive. A Course-hardcoded component doesn't generalize.
- **Pros:** Second consumer becomes a copy of a proven pattern (or a shared field-descriptor
  component); paper CSS rules written once.
- **Cons:** Speculative until a second requester actually asks; a premature abstraction over
  n=1 is worse than a copy.
- **Context:** Deferred by /autoplan (2026-07-16, branch feature-course-pdf). The build
  decisions live in the design doc at
  `~/.gstack/projects/JB772-ai-full-stack/JB772-worktree-feature-course-pdf-design-20260716-130321.md`
  (see "0D Selective-Expansion Analysis" and the Decision Audit Trail).
- **Effort:** M (human) → S (CC). **Priority:** P3.
- **Depends on:** the Course print feature shipping first; a second entity actually needing it.
