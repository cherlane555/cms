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
- No RowAudit / DateOnly handlers in this codebase — the skill mentions them, but they don't exist
  here. Ignore those steps. Sticky toolbar: only the Course form pins its action toolbar
  (`course-form.scss`; works because `app.scss` makes the shell's `.content` pane the scroll
  container — `top: -1.25rem` cancels the pane's padding so the bar pins flush).
- Lookup routes are **kebab-case plural** (`/api/lookups/course-groups`), not `/api/Lookups/{Table}`.
- Sort fallback when a table has no `DisplayOrder`: pick the natural order per table
  (PublishStatus → `pkid ASC`; CourseGroup → `Description ASC`), not a blind `pkid DESC`.
- Defer Primary-Foreign nav buttons until the referenced feature exists (avoids dead links).

## Entity facts

- **AppRole** (built) — PK is the string `RoleId` (`pkid` IDENTITY is a display surrogate). n-n with
  AppUser via `AppUserRole`: list shows a `使用者數` count (subquery), form has a users multi-select.
  AppUser lookup label is `"UserName (UserId)"`, served by `GET /api/lookups/app-users`.
- **CourseGroup** (built) — ⚠️ `FK_Course_CourseGroup` is **ON DELETE CASCADE**: deleting a group
  deletes every Course under it (1084 courses exist, all grouped). Guarded: the delete endpoint
  returns 409 while the group still contains courses.
- **Course** (built) — FKs → Partner / CourseGroup / PublishStatus. True n-n is only
  `CourseInCertification` (→ Certification) and `CourseJobCategories` (→ JobCategory); the
  `certifications` / `job-categories` lookups exist. The form's action toolbar is sticky (see the
  corrections bullet above).
- **FeaturedPromoItem** (built, custom spec `spec/custom/FeaturedPromoItem/`) — NOT the standard
  list/detail/form scaffold: one weekly (Mon–Sun) grid page with TrainingCenter tabs, 3 slots per
  day, inline editing, and a Copy/Paste clipboard held in the service. Unique key
  `(ScheduleOn, TrainingCenter_pkid, Slot)`: create/update pre-check via `IsSlotTakenAsync` → 409;
  `POST /{id}/move` swaps slots through a temporary Slot 0 inside a transaction. `Promotion_pkid`
  is set by looking up `Promotion2.PromoCode` (`GET /api/lookups/promotions/by-code/{code}`;
  PromoCode is unique). `ScheduleOn` is a `date` column mapped to C# `DateTime` (no DateOnly
  handlers in this codebase). Lookups added: `training-centers`, `promotions/by-code`.
- **AppUser** (pending) — PK is the string `UserId` (`pkid` is a display surrogate, same pattern as
  AppRole); n-n with AppRole via `AppUserRole`. `SysConfig` is keyed `configKey`/`configValue`
  (table appears in both `admin.sql` and `auth.sql`, identical); live DB verified to have the
  `appConfig` row whose JSON value carries `defaultPassword`. `PasswordUpdatedTime` is nullable —
  set it on create along with the hash.
