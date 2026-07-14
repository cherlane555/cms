# CLAUDE.md

**CMS** — full-stack admin app from a SQL Server schema. Backend `src/CMS.API` (.NET 9, Dapper, no
EF, Swagger); frontend `src/CMS.NG` (Angular 20 standalone, PrimeNG v20). First feature: AppRole CRUD.

New features follow `spec/code-gen.convention.md` and the AppRole feature. Setup, run commands,
folder map, testing approach, and full patterns: see [docs/setup-notes.md](docs/setup-notes.md).

## Must-know

- **PrimeNG stays on v20** — Angular is 20; `primeng@latest` (v21) needs Angular 21 and breaks install.
- **Backend:** models `{Entity}` / `{Entity}Request` / `{Entity}Query`; async Dapper repo
  (`I{Entity}Repository` + impl) via `IDbConnectionFactory`; `RTRIM()` nchar; alias FK `_pkid` cols to
  C# names. Controller `api/{entity-plural}`, **`PUT` takes id from the body**. n-n = delete-then-
  reinsert junction rows in a transaction. Register repos in `Program.cs`.
- **Frontend:** feature folders `features/{plural}/{entity}-list|-detail|-form` (lazy standalone).
  Service in `core/services`, base URL from `@env`, `encodeURIComponent` string ids. List = `p-table`
  + filter `p-drawer` + sessionStorage `{entity}-list-{filters,sort,page}`; delete via
  `ConfirmationService`+`MessageService`. Form = Reactive Forms, `forkJoin` lookups, `fb.nonNullable`
  required controls, `p-multiSelect` for n-n, disable PK in edit. Add nav in `app.ts`/`app.html`.
- **AppRole:** PK is string `RoleId` (`pkid` is a display surrogate). N-N with AppUser via AppUserRole
  → list shows `使用者數` count; form has a users multi-select (`GET /api/lookups/app-users`).
- UI text is Traditional Chinese.
