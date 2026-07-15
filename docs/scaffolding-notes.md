# Scaffolding & entity notes

Read before any `/crud` run or before creating/modifying an entity feature. Schema facts below are
verified against `database/*.sql` (and the live DB where noted) — trust them over the `/crud`
skill's heuristics.

## Corrections to the `/crud` skill

- **n-n test:** a table is an n-n junction **iff its PK is the composite of its two FK columns**. A
  surrogate `pkid` IDENTITY alongside is fine (`AppUserRole` has one and *is* n-n). PK on `pkid`
  and/or payload cols (DisplayOrder, Description…) → **child entity, not n-n** — the skill's
  name-based heuristic over-matches these. Entities, *not* junctions: `PartnerCourseGroup`
  (CourseGroup/Partner), `CourseFAQ` / `CourseRelatedLink` / `HotCourse` (Course).
- No RowAudit / DateOnly handlers / sticky-toolbar in this codebase — the skill mentions them, but
  they don't exist here. Ignore those steps.
- Lookup routes are **kebab-case plural** (`/api/lookups/course-groups`), not `/api/Lookups/{Table}`.
- Sort fallback when a table has no `DisplayOrder`: pick the natural order per table
  (PublishStatus → `pkid ASC`; CourseGroup → `Description ASC`), not a blind `pkid DESC`.
- Defer Primary-Foreign nav buttons until the referenced feature exists (avoids dead links).

## Entity facts

- **AppRole** (built) — PK is the string `RoleId` (`pkid` IDENTITY is a display surrogate). n-n with
  AppUser via `AppUserRole`: list shows a `使用者數` count (subquery), form has a users multi-select.
  AppUser lookup label is `"UserName (UserId)"`, served by `GET /api/lookups/app-users`.
- **CourseGroup** (built) — ⚠️ `FK_Course_CourseGroup` is **ON DELETE CASCADE**: deleting a group
  deletes every Course under it (1084 courses exist, all grouped; only 25 of 215 groups are
  unreferenced). Not guarded in code; delete confirm warns instead. Add a real dependency check when
  building Course.
- **Course** (pending) — FKs → Partner / CourseGroup / PublishStatus; all three lookups already
  exist. True n-n is only `CourseInCertification` (→ Certification) and `CourseJobCategories`
  (→ JobCategory); both need new `certifications` / `job-categories` lookups.
- **AppUser** (pending) — PK is the string `UserId` (`pkid` is a display surrogate, same pattern as
  AppRole); n-n with AppRole via `AppUserRole`. `SysConfig` is keyed `configKey`/`configValue`
  (table appears in both `admin.sql` and `auth.sql`, identical); live DB verified to have the
  `appConfig` row whose JSON value carries `defaultPassword`. `PasswordUpdatedTime` is nullable —
  set it on create along with the hash.
