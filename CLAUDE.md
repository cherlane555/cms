# CLAUDE.md

**CMS** — full-stack admin app generated from the SQL Server schema in `database/`. Backend
`src/CMS.API` (.NET 9, Dapper, no EF, Swagger); frontend `src/CMS.NG` (Angular 20 standalone,
PrimeNG v20). SQL Server `.\SQLEXPRESS`, database `CMS`.

## Hard rules

- **PrimeNG stays on v20** — Angular is 20; `primeng@latest` (v21) needs Angular 21 and breaks install.
- UI text is Traditional Chinese.
- New features follow `spec/code-gen.convention.md` and the AppRole reference feature.

## Read when needed

- [docs/setup-notes.md](docs/setup-notes.md) — run commands, folder map, backend/frontend
  code-generation patterns, testing approach. **Read before building, running, or writing code.**
- [docs/scaffolding-notes.md](docs/scaffolding-notes.md) — verified per-entity schema facts and
  corrections to the `/crud` skill. **Read before any `/crud` run or entity feature change.**
