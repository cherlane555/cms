# CLAUDE.md

**CMS** — full-stack admin app generated from the SQL Server schema in `database/`. Backend
`src/CMS.API` (.NET 9, Dapper, no EF, Swagger, JWT auth); frontend `src/CMS.NG` (Angular 20
standalone, PrimeNG v20). SQL Server `.\SQLEXPRESS`, database `CMS`.

## Hard rules (never violate; the *how* lives in the linked notes)

- **PrimeNG stays on v20** — `primeng@latest` (v21) needs Angular 21 and breaks install.
- UI text is Traditional Chinese first, English second (新增 / 編輯 / 儲存 / 刪除).
- **Auth is secure-by-default:** every endpoint needs a JWT except `AuthController.Login`; passwords
  are hashed server-side only — no password or hash crosses the wire.
- **Every feature:** repositories audit Insert/Update/Delete via `IRowAuditWriter`; detail/form pages
  lead the actions bar with `<app-row-audit-badge>`; errors flow through the global exception
  middleware (no per-controller try/catch).

## Read when needed

| File | Read before… |
|------|--------------|
| [spec/code-gen.convention.md](spec/code-gen.convention.md) | generating or changing an entity CRUD feature — canonical patterns; new features follow it and the AppRole reference feature |
| [docs/cross-cutting-notes.md](docs/cross-cutting-notes.md) | wiring a repository or detail/form page (row audit), or touching error handling — the *how* behind the cross-cutting rule above |
| [docs/auth-notes.md](docs/auth-notes.md) | touching login, JWT, roles, profile, or password/reset flows — incl. the SHA-256 lowercase-hex hashing detail |
| [docs/setup-notes.md](docs/setup-notes.md) | running/building/testing, or needing repo layout & one-time context |
| [docs/scaffolding-notes.md](docs/scaffolding-notes.md) | any `/crud` run or a per-entity schema question |
| [docs/print-notes.md](docs/print-notes.md) | a print/PDF route, a `data: { bare: true }` shell-less route, or the 課程簡章 |

Test login: `miles@uuu.com.tw` / `CMS4fun#` (roles **Admin + User**).
