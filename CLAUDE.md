# CLAUDE.md

**CMS** — full-stack admin app generated from the SQL Server schema in `database/`. Backend
`src/CMS.API` (.NET 9, Dapper, no EF, Swagger, JWT auth); frontend `src/CMS.NG` (Angular 20
standalone, PrimeNG v20). SQL Server `.\SQLEXPRESS`, database `CMS`.

## Hard rules

- **PrimeNG stays on v20** — Angular is 20; `primeng@latest` (v21) needs Angular 21 and breaks install.
- UI text is Traditional Chinese (with English), e.g. 新增 / 編輯 / 儲存 / 刪除.
- New entity features follow `spec/code-gen.convention.md` and the AppRole reference feature.
- **Auth is secure-by-default:** every API endpoint requires a valid JWT except `AuthController.Login`.
  Passwords are hashed **server-side only** as lowercase-hex SHA-256 — no password or hash crosses the wire.

## Cross-cutting conventions (every feature, no exceptions)

### Row Audit — every CRUD write is logged

- Every repository **must** log to `RowAudit` on Insert / Update / Delete by injecting the shared
  `IRowAuditWriter` (`src/CMS.API/Auditing/`) and calling `LogInsertAsync` / `LogUpdateAsync` /
  `LogDeleteAsync` with the **real DB table name**.
- **Update**: load the existing row first (the "before"), apply the UPDATE, then log
  `(before, after)` — the after image is the before row with the request's columns applied
  (copy joined labels / n-n lists from before so only real columns diff).
  **Delete**: load the row first so its first string column is still available, then delete, then log.
- Audit writes ride the **same connection + transaction** as the change (pass `conn, tx` to the
  writer) — a failed or rolled-back change must leave no audit row. Missing row → return `false`
  before any audit write.
- Column rules (the writer handles these — don't reimplement): `ActionDesc` = first string-type
  property value on Insert/Delete, comma-separated changed property names on Update (empty when
  nothing changed); `PrimaryKeyValues` = `pkid` as string; `UserName` = the request JWT's
  `userName` claim, fallback `"system"`; never insert `pkid` into `RowAudit` (IDENTITY);
  `ActionDesc` truncates at 1000.
- Every **detail page and form page** places the reusable badge
  (`src/CMS.NG/src/app/shared/row-audit-badge/`, selector `app-row-audit-badge`, inputs
  `tableName` + `pkid`) as the **first item in the page-header actions bar** (this app's
  "toolbar #start"). It shows the latest change inline and opens the full trail dialog
  (`GET /api/rowaudit?tableName=&pkid=`). A missing/0 pkid (new record) shows "no history" and
  must not call the API. Component specs that mount such pages need
  `provideHttpClient() + provideHttpClientTesting()`.

### Exception handling — one global layer

- `ExceptionHandlingMiddleware` (first in the pipeline, `src/CMS.API/Middleware/`) owns unexpected
  errors: it logs the full exception server-side and returns a generic 500 JSON body
  `{ "message": "An unexpected error occurred." }` — never a stack trace, SQL text, or connection
  details. **Do not add per-controller try/catch for unexpected errors**; let them throw.
- Keep meaningful responses as-is: 401 (unauthenticated), 403 (forbidden), and validation 400s
  (`ValidationProblem`) are produced without throwing and bypass the middleware.
- The Angular `authInterceptor` surfaces 500-class errors as a friendly global toast (root
  `MessageService` + the `<p-toast>` in the App shell) using the body's safe `message`;
  401 still clears the session and redirects to Login; 400 validation errors stay on the form.

## Read when needed

| File | Read before… |
|------|--------------|
| [spec/code-gen.convention.md](spec/code-gen.convention.md) | generating or changing an entity CRUD feature (canonical patterns) |
| [docs/setup-notes.md](docs/setup-notes.md) | running/building/testing, or needing repo layout & one-time context |
| [docs/scaffolding-notes.md](docs/scaffolding-notes.md) | any `/crud` run or a per-entity schema question |
| [docs/auth-notes.md](docs/auth-notes.md) | touching login, JWT, roles, profile, or password / reset flows |

Test login: `miles@uuu.com.tw` / `CMS4fun#` (the `defaultPassword`; roles **Admin + User**).
