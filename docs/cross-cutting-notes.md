# Cross-cutting conventions — Row Audit, Exception Handling & Authorization (Lab 06)

Read this before wiring a new entity's repository or pages, touching error handling, or gating a
role-restricted feature's write endpoints. The non-negotiable one-liners live in `CLAUDE.md`; this
file is the how.

## Row Audit — backend

Every repository logs to `RowAudit` on Insert / Update / Delete by injecting the shared
`IRowAuditWriter` (`src/CMS.API/Auditing/`) and calling `LogInsertAsync` / `LogUpdateAsync` /
`LogDeleteAsync` with the **real DB table name** (e.g. `"Course"`, `"FeaturedPromoItem"`).

Per-operation pattern (see `PartnerRepository` for the canonical shape):

- **Insert**: open conn + `BeginTransaction`, INSERT (read pkid back), build the created entity,
  `LogInsertAsync(table, created, conn, tx)`, commit.
- **Update**: load the existing row first (the "before") on the same tx; if null → return `false`.
  Apply the UPDATE; if 0 rows → return `false`. Log `(before, after)` where the **after image is
  the before row with the request's columns applied** — copy joined labels (`PartnerName`,
  `PromoCode`, `UserCount`…) and n-n lists from before so only real table columns diff. Commit.
- **Delete**: load the row first (its first string column must survive), DELETE, log, commit.

Audit writes ride the **same connection + transaction** as the change (pass `conn, tx` to the
writer) — a failed or rolled-back change leaves no audit row.

Column rules — the writer implements these by reflection; **don't reimplement**:

| Column | Rule |
|--------|------|
| `ActionDesc` | Insert/Delete: first string-type property value. Update: comma-separated changed property names (empty when nothing changed). Truncated at 1000. |
| `PrimaryKeyValues` | the entity's `pkid`, as a string |
| `UserName` | the request JWT's `userName` claim; fallback `"system"` |
| `pkid` | never inserted (IDENTITY) |

Read side: `GET /api/rowaudit?tableName=&pkid=` (`RowAuditController` / `RowAuditRepository`)
returns `dateTime / userName / actionType / actionDesc`, newest first.

## Row Audit — frontend badge

Every **detail page and form page** places the reusable badge as the **first item in the
page-header actions bar** (this app's "toolbar #start"):

```html
<app-row-audit-badge tableName="Course" [pkid]="course()?.pkid" />
```

- Component: `src/CMS.NG/src/app/shared/row-audit-badge/` (standalone, selector
  `app-row-audit-badge`, inputs `tableName` + `pkid`). Service: `@core/services/row-audit.service`.
- Shows the latest change inline ("Update by alice · 2026-06-04 14:30"); click opens a `p-dialog`
  with the full trail, newest first.
- A missing/0/null pkid (new, unsaved record) shows "尚無紀錄 No history" and does **not** call
  the API — form pages bind their pkid signal (0 in create mode).
- Testing gotcha: any spec that mounts a page containing the badge needs
  `provideHttpClient(), provideHttpClientTesting()` in its providers (the badge injects
  `HttpClient` transitively).
- Not on app-users/profile pages: the AppUser table is not audited (AuthRepository has no writer).

## Exception handling — one global layer

- `ExceptionHandlingMiddleware` (`src/CMS.API/Middleware/`, registered **first** in the pipeline)
  logs the full exception (message + stack) server-side and returns one safe body:
  `500` + `{ "message": "An unexpected error occurred." }` — never a stack trace, SQL text, or
  connection details. **No per-controller try/catch for unexpected errors** — let them throw.
- 401 / 403 / validation 400 (`ValidationProblem`) are produced without throwing and bypass the
  middleware — keep them meaningful.
- Angular: `authInterceptor` (`@core/auth`) toasts 500-class errors globally using the body's safe
  `message` (fallback text when there is no body), via the root `MessageService` (provided in
  `app.config.ts`) + the global `<p-toast>` in the App shell. 401 still clears the session and
  redirects to `/login`; 400 validation errors stay on the form. Page-level toasts keep their own
  component-scoped `MessageService` — don't remove either.

## Authorization — write endpoints for role-gated features

The global fallback policy (`RequireAuthenticatedUser()`, see auth-notes) only proves a caller is
*logged in* — it says nothing about role. `app.ts`'s `navGroups` computed signal hides a sidebar
group when the user lacks its `requiresRole`, but that is a **display filter only**: no route guard
enforces it (`auth.guard.ts` checks authentication, not role), and the API is the real boundary. A
non-admin caller can always reach a hidden route's endpoints directly.

**Two features shipped without a server-side gate and were exploitable before the fix:**

- `AppRolesController` — Create/Update write the request's `UserIds` straight to `AppUserRole`,
  the exact table `AuthRepository.GetLoginUserAsync` reads to build a JWT's role claims. Any
  authenticated caller could self-assign to `RoleId: "Admin"` and re-login with full admin access.
- `PublishStatusesController` — nav-grouped under 系統管理 Admin alongside AppRole, but had no
  `[Authorize]` at all. `Course.PublishStatus_pkid` FKs to this table and business logic (the
  brochure's `isPublished` gate) depends on its rows.

**Rule:** if a feature's nav entry carries `requiresRole` (`app.ts`), add
`[Authorize(Roles = "Admin")]` to its Create/Update/Delete actions — mirror
`AuthController.ResetPassword`'s existing pattern. `GetAll`/`Query`/`GetById` can stay open to any
authenticated user unless the read itself is sensitive.

**The TOCTOU that comes with it:** every entity here checks `ExistsAsync` (a separate, earlier
connection) before `CreateAsync` (a new transaction) — a concurrent create for the same key can
race past the check. The PK constraint is the real backstop; catch its violation
(`SqlException.Number is 2627 or 2601`) inside `CreateAsync` and throw a typed
`{Entity}ConflictException` (see `RoleConflictException`, `PublishStatusConflictException`,
`SlotConflictException`) for the controller to map to 409, instead of letting a raw `SqlException`
surface as a 500. Verify with a real two-concurrent-transaction test against the live DB (see
`AppRoleRepositoryConcurrencyTests`) — a mocked test can't reproduce the race.
