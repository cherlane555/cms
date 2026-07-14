# Setup & reference notes

Detailed setup, run commands, folder map, code-generation patterns, and one-time context for the
CMS project. `CLAUDE.md` (repo root) holds the slim per-session guidance and links here.

## Tech stack

- **Backend:** .NET 9 (`net9.0`), Dapper 2.x, Microsoft.Data.SqlClient, Swashbuckle.AspNetCore
  7.2.0. Controllers-based (not minimal API). All data access is async.
- **Frontend:** Angular 20 standalone components (no NgModules), PrimeNG **v20** (Aura theme,
  `providePrimeNG` in `app.config.ts`), primeicons, Reactive Forms, RxJS. Karma + Jasmine tests.
- **DB:** SQL Server (local `.\SQLEXPRESS`, database `CMS`).

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
ng test --watch=false   # Karma/Jasmine (needs Chrome; use --browsers=ChromeHeadless in CI)
```

- API port **5000** (`Properties/launchSettings.json`), Angular dev port **4200** (`angular.json`).
- CORS allows any `localhost`/`127.0.0.1` origin.
- Frontend talks to the API via `environment.apiBaseUrl` (absolute URL) — there is **no** dev-server
  proxy. `ng serve` uses `environment.development.ts` via `fileReplacements`.
- A production `ng build` exceeds Angular's default bundle-size budget (PrimeNG is large); the
  required commands (`ng serve`, `ng test`) are unaffected.

## Repository layout

```
database/                 Source-of-truth SQL schema (CREATE TABLE scripts)
  auth.sql                AppRole, AppUser, AppUserRole, SysConfig
  admin.sql, course.sql, promotion.sql
spec/                     Conventions + per-feature build specs (drive code generation)
  code-gen.convention.md  THE canonical patterns doc — read before generating a feature
  feature-spec.template.md, sample1.spec.md (Course), sample2.spec.md
  ui-sample-*.png         Visual references (list/view/edit/add)
src/
  CMS.slnx                .NET solution (references API + Tests). Lives here, NOT in CMS.API/,
                          so `cd CMS.API && dotnet build` stays unambiguous (one .csproj there).
  CMS.API/                .NET 9 Web API
    Controllers/          {Entity}Controller (e.g. AppRolesController), LookupsController
    Data/                 IDbConnectionFactory + SqlConnectionFactory (opens SqlConnection)
    Models/               {Entity} (response), {Entity}Request (write DTO), {Entity}Query (search)
    Repositories/         I{Entity}Repository + {Entity}Repository (Dapper), lookup repo
    Program.cs            DI, CORS, Swagger, repo registration
    appsettings.json      ConnectionStrings:CMS
  CMS.API.Tests/          xUnit + Moq (controller tests against a mocked repository)
  CMS.NG/                 Angular 20 app
    src/environments/     environment.ts / environment.development.ts (apiBaseUrl; NO proxy)
    src/app/core/         models/ + services/ (one data service per entity + lookup.service)
    src/app/features/{plural}/{entity}-list | -detail | -form/
    src/app/app.ts/.html/.scss   Shell + sidebar nav
docs/setup-notes.md       This file
```

## Code-generation patterns

Follow `spec/code-gen.convention.md`. What the AppRole feature established:

### Backend
- **Models:** `{Entity}.cs` (response, includes nav/subquery fields like counts), `{Entity}Request.cs`
  (write DTO; n-n carried as `List<...>`), `{Entity}Query.cs` (search DTO; `Keyword?`, optional FK/bool
  filters).
- **Repository:** Dapper only. Interface + impl, constructor-injected `IDbConnectionFactory`.
  Async everywhere with `CommandDefinition(..., cancellationToken: ct)`. `nchar` columns → `RTRIM()`
  in SELECT. Alias FK `_pkid` columns to the C# property name (`Partner_pkid AS PartnerPkid`).
  **n-n sync:** inside a transaction, delete-then-reinsert the junction rows on create/update.
- **Controller:** route `api/{entity-plural}` (kebab-case). Endpoints: `GET /` (all),
  `POST /query` (filtered), `GET /{id}`, `POST /` (create), `PUT /` (update — **id/pkid comes from
  the body, not the route**), `DELETE /{id}`. Return `Ok/NotFound/CreatedAtAction/NoContent/Conflict`.
- **String PK entities** (e.g. AppRole's `RoleId`): route param is `{id}` with **no** `:int`
  constraint; the entity is located by that string key.
- Register each repository in `Program.cs` (`AddScoped<I..., ...>`).

### Frontend
- **Feature folders:** `features/{plural}/{entity}-list`, `-detail`, `-form`. Standalone components,
  lazy-loaded via `loadComponent` in `app.routes.ts`.
- **Data service:** `core/services/{entity}.service.ts`, `providedIn: 'root'`, `inject(HttpClient)`,
  base URL from `@env`. String-PK ids are `encodeURIComponent`'d in the URL.
- **List page:** PrimeNG `p-table` (sortable, paginated) + filter `p-drawer`. Persist to
  `sessionStorage` under keys `{entity}-list-filters`, `{entity}-list-sort`, `{entity}-list-page`.
  Header has a "搜尋條件" (open drawer) + "新增" button. Per-row view/edit/delete actions; delete uses
  `ConfirmationService` (`p-confirmDialog`) + `MessageService` (`p-toast`).
- **Form page:** Reactive Forms; `forkJoin` for parallel lookup calls on init. Typed forms — use
  `fb.nonNullable.control(...)` for required string/number controls so `getRawValue()` is non-null.
  n-n via `p-multiSelect` (`appendTo="body"`, `[maxSelectedLabels]="9999"`). Disable the PK control
  in edit mode. Branch create vs update into separate `.subscribe()` calls (don't build a
  `Observable<A> | Observable<void>` union — it isn't callable under strict types).
- **Detail page:** `forkJoin` the entity + lookups, map FK/n-n ids to display labels.
- **Sidebar nav:** add the entry under the right group in `app.ts` (`navGroups`) / `app.html`.
  The sidebar is styled after the PrimeNG "Ultima" analytics dashboard (light surface, uppercase
  muted section labels, indigo active pill).

### Conventions
- Path aliases (`tsconfig.json`): `@env`, `@core/*`, `@features/*`, `@shared/*`.
- UI text is Traditional Chinese (with English), e.g. 新增 / 編輯 / 儲存 / 取消 / 刪除確認.
- Delete confirmation copy: ``確定要刪除主代碼 <b>${pkid}</b>「${businessKey}」？``

## Testing approach
- **Backend:** controllers are unit-tested against a **mocked `I{Entity}Repository`** (Moq,
  `MockBehavior.Strict`) — no live DB needed. Cover list, filter, view (found/not-found), add
  (created/conflict/validation), edit (updated/not-found), delete.
- **Frontend:** services tested with `HttpTestingController` (assert method/URL/body). Components
  tested with jasmine-spy services returning `of(...)`, `provideRouter([])`, `provideNoopAnimations()`,
  and a fake `ActivatedRoute` (`convertToParamMap`).

## Domain notes
- **AppRole** — PK is the string column `RoleId` (the `pkid` IDENTITY is a display surrogate).
  N-N with **AppUser** via **AppUserRole**: the list shows a `使用者數` count (subquery over
  AppUserRole) and the form has a users multi-select. AppUser lookup label is `"UserName (UserId)"`,
  served by `GET /api/lookups/app-users`.

## One-time context
- **PrimeNG pinned to v20:** `primeng@*` originally pulled v21, which requires Angular 21 and broke
  `npm install`. Installed `primeng@^20` / `@primeng/themes@^20`.
- **Solution location:** `CMS.slnx` sits in `src/`, not inside `CMS.API/`, to avoid `dotnet build`
  ambiguity (a solution beside the `.csproj` makes the folder have two build targets). It is `.slnx`
  (the SDK 10 default format); SDK 9 is also installed and both build fine.
- **Repo:** git history on `develop` (default) / `main`; remote `origin` → GitHub `cherlane555/cms`.
