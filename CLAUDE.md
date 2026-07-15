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

## Read when needed

| File | Read before… |
|------|--------------|
| [spec/code-gen.convention.md](spec/code-gen.convention.md) | generating or changing an entity CRUD feature (canonical patterns) |
| [docs/setup-notes.md](docs/setup-notes.md) | running/building/testing, or needing repo layout & one-time context |
| [docs/scaffolding-notes.md](docs/scaffolding-notes.md) | any `/crud` run or a per-entity schema question |
| [docs/auth-notes.md](docs/auth-notes.md) | touching login, JWT, roles, profile, or password / reset flows |

Test login: `miles@uuu.com.tw` / `CMS4fun#` (the `defaultPassword`; roles **Admin + User**).
