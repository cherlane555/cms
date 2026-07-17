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
- **RowAudit now exists** (see [cross-cutting-notes.md](cross-cutting-notes.md)) and is wired into
  every CRUD repository — ignore any `/crud` skill step claiming otherwise. **No `DateOnly`/`TimeOnly`
  handlers exist yet**: no entity has scaffolded a `date`/`time` column, so `code-gen.convention.md`'s
  "(already in Program.cs)" note is aspirational, not current state — register the handlers in
  `Program.cs` yourself the first time one is needed. Sticky toolbar: only the Course form pins its action toolbar
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
- **AppUser** (minimal — list + edit only, no full CRUD) — PK is the string `UserId` (`pkid` is a
  display surrogate, same pattern as AppRole); n-n with AppRole via `AppUserRole`. `SysConfig` is
  keyed `configKey`/`configValue` (table appears in both `admin.sql` and `auth.sql`, identical);
  live DB verified to have the `appConfig` row whose JSON value carries `defaultPassword`.
  `PasswordUpdatedTime` is nullable — set it on create along with the hash. See auth-notes.md —
  full CRUD was never built (Lab 03 scope), only enough to host the Admin-only Reset Password
  button.
- **Partner** (built) — course parent lookup table (FK target for `Course.Partner_pkid`); standard
  list/detail/form scaffold.
- **PublishStatus** (built) — admin lookup table gating course visibility (`Course.PublishStatus_pkid`;
  the brochure's `isPublished` check depends on `PublishStatus = 2` 上架中). Nav-gated under
  系統管理 Admin alongside AppRole — its write endpoints need `[Authorize(Roles = "Admin")]` (shipped
  without this originally; see cross-cutting-notes.md).
