# Setup & reference notes

Run commands, folder map, and one-time context for the CMS project. `CLAUDE.md` (repo root)
holds the slim per-session core and links here. Entity CRUD patterns live in
`spec/code-gen.convention.md`; the auth subsystem lives in [auth-notes.md](auth-notes.md).

## Tech stack

- **Backend:** .NET 9 (`net9.0`), Dapper 2.x, Microsoft.Data.SqlClient, Swashbuckle 7.2.0,
  JWT bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`). Controllers-based (not minimal
  API). All data access is async.
- **Frontend:** Angular 20 standalone components (no NgModules), PrimeNG **v20** (Aura theme,
  `providePrimeNG` in `app.config.ts`), primeicons, Reactive Forms, RxJS. Karma + Jasmine tests.
- **DB:** SQL Server (local `.\SQLEXPRESS`, database `CMS`). `sqlcmd` needs `-C` (trust cert).

## Running

Backend (from `src/CMS.API`):
```powershell
dotnet build
dotnet run              # http://localhost:5000 ; Swagger UI at /swagger
```
Backend tests (from `src/CMS.API.Tests`):
```powershell
dotnet test
```
Frontend (from `src/CMS.NG`):
```powershell
npm install             # first time
npm start               # ng serve -> http://localhost:4200
ng test --watch=false --browsers=ChromeHeadless
```

- API port **5000** (`Properties/launchSettings.json`), Angular dev port **4200** (`angular.json`).
- CORS allows any `localhost`/`127.0.0.1` origin.
- Frontend talks to the API via `environment.apiBaseUrl` (absolute URL) — **no** dev-server proxy.
  `ng serve` uses `environment.development.ts` via `fileReplacements`.
- A production `ng build` exceeds Angular's default bundle-size budget (PrimeNG is large); the
  required commands (`ng serve`, `ng test`) are unaffected.
- `dotnet run` leaves a `CMS.API` process that can lock `bin/` on the next build — stop it first
  (`Get-Process CMS.API | Stop-Process -Force`).

## Repository layout

```
database/                 Source-of-truth SQL schema (CREATE TABLE scripts)
  auth.sql                AppRole, AppUser, AppUserRole, SysConfig
  admin.sql, course.sql, promotion.sql
spec/                     Conventions + per-feature build specs (drive code generation)
  code-gen.convention.md  Canonical CRUD patterns — read before generating a feature
  feature-spec.template.md, sample1.spec.md (Course), sample2.spec.md
  ui-sample-*.png         Visual references (list/view/edit/add)
src/
  CMS.slnx                .NET solution (references API + Tests). Lives in src/, NOT in CMS.API/,
                          so `cd CMS.API && dotnet build` stays unambiguous (one .csproj there).
  CMS.API/                .NET 9 Web API
    Controllers/          {Entity}Controller, LookupsController, AuthController (login/profile/password)
    Data/                 IDbConnectionFactory + SqlConnectionFactory
    Models/               {Entity} (response), {Entity}Request (write DTO), {Entity}Query (search)
    Repositories/         I{Entity}Repository + {Entity}Repository (Dapper), lookup + auth repos
    Security/             Auth: PasswordHasher, PasswordPolicy, TokenService, JWT key/options (auth-notes)
    Program.cs            DI, CORS, Swagger, JWT auth + global authorization, repo registration
    appsettings.json      ConnectionStrings:CMS
  CMS.API.Tests/          xUnit + Moq; WebApplicationFactory for auth/authorization
  CMS.NG/                 Angular 20 app
    src/environments/     environment.ts / environment.development.ts (apiBaseUrl; NO proxy)
    src/app/core/         models/, services/ (one per entity + lookup), auth/ (service/interceptor/guard)
    src/app/features/{plural}/{entity}-list | -detail | -form/; auth/login, profile, app-users
    src/app/app.ts/.html/.scss   Shell + sidebar nav (auth-gated chrome, role-gated Admin group)
docs/setup-notes.md       This file
```

## Code-generation deltas

`spec/code-gen.convention.md` is canonical (models, repo, controller, list/form, special column
types, endpoints). Points worth repeating / beyond that doc:

- **Backend:** async everywhere (`CommandDefinition(..., cancellationToken: ct)`); `nchar` → `RTRIM()`
  in SELECT; alias FK `_pkid` columns to the C# name (`Partner_pkid AS PartnerPkid`); n-n sync is
  delete-then-reinsert inside a transaction. `PUT` takes the id/pkid from the **body**, not the route.
  String-PK route param is `{id}` (no `:int`). Register each repository in `Program.cs`.
- **Frontend:** typed forms — `fb.nonNullable.control(...)` for required controls so `getRawValue()`
  is non-null; branch create-vs-update into **separate** `.subscribe()` calls (a
  `Observable<A> | Observable<void>` union isn't callable under strict types); detail pages `forkJoin`
  the entity + lookups and map ids → labels. Path aliases: `@env`, `@core/*`, `@features/*`, `@shared/*`.
- **Sidebar nav:** add the entry under the right group in `app.ts` (`navGroups`) / `app.html`
  (Ultima style: light surface, uppercase muted labels, indigo active pill). Gate a group by role with
  `requiresRole` (see auth-notes).
- **Delete confirm copy:** `` 確定要刪除主代碼 <b>${pkid}</b>「${businessKey}」？ ``

## Testing approach

- **Backend:** controllers unit-tested against a **mocked `I{Entity}Repository`** (Moq,
  `MockBehavior.Strict`) — no live DB. Auth/authorization tested end-to-end via
  `WebApplicationFactory<Program>` (override `IJwtSigningKeyProvider` + the repos). See auth-notes.
- **Frontend:** services via `HttpTestingController` (assert method/URL/body); components with
  jasmine-spy services returning `of(...)`, `provideRouter([])`, `provideNoopAnimations()`, and a fake
  `ActivatedRoute` (`convertToParamMap`). Components that inject `AuthService` also need `provideHttpClient()`.

## Domain notes

Per-entity schema facts and `/crud` skill corrections live in [scaffolding-notes.md](scaffolding-notes.md).

## One-time context

- **PrimeNG pinned to v20:** `primeng@*` originally pulled v21, which requires Angular 21 and broke
  `npm install`. Installed `primeng@^20` / `@primeng/themes@^20`.
- **Solution location:** `CMS.slnx` sits in `src/`, not inside `CMS.API/`, to avoid `dotnet build`
  ambiguity. It is `.slnx` (SDK 10 default format); SDK 9 is also installed and both build fine.
- **Repo:** git history on `develop` (default) / `main`; remote `origin` → GitHub `cherlane555/cms`.
