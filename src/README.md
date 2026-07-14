# CMS — Full-stack skeleton

Generated from the DB schema in `../database` following `../spec/code-gen.convention.md`.
First feature: **AppRole** CRUD (list/filter, view, add, edit) with the `AppRole ↔ AppUser`
N-N relationship (user count in the list, users multi-select in the form).

## Layout

```
src/
  CMS.slnx                 .NET solution (references API + Tests)
  CMS.API/                 .NET 9 Web API (Dapper, Swagger, CORS)
    Controllers/           AppRolesController, LookupsController
    Data/                  IDbConnectionFactory + SqlConnectionFactory
    Models/                AppRole, AppRoleRequest, AppRoleQuery, AppUserLookup
    Repositories/          IAppRoleRepository/AppRoleRepository, ILookupRepository/LookupRepository
  CMS.API.Tests/           xUnit tests (Moq) for the AppRole endpoints
  CMS.NG/                  Angular 20 app (standalone components, PrimeNG)
    src/environments/      environment.ts / environment.development.ts (no proxy)
    src/app/core/          models + data services
    src/app/features/app-roles/  app-role-list / app-role-detail / app-role-form
```

## Backend — CMS.API

Connection string (`appsettings.json` → `ConnectionStrings:CMS`):

```
Server=.\SQLEXPRESS;Database=CMS;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False
```

```powershell
cd CMS.API
dotnet build
dotnet run              # http://localhost:5000, Swagger UI at /swagger
```

```powershell
cd CMS.API.Tests
dotnet test             # 13 tests
```

### Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET    | `/api/app-roles`         | All roles (with user count) |
| POST   | `/api/app-roles/query`   | Filtered search (keyword, permissionLevel) |
| GET    | `/api/app-roles/{id}`    | Single role by RoleId (+ assigned user ids) |
| POST   | `/api/app-roles`         | Create (409 on duplicate RoleId) |
| PUT    | `/api/app-roles`         | Update (RoleId in body — it is the PK) |
| DELETE | `/api/app-roles/{id}`    | Delete |
| GET    | `/api/lookups/app-users` | AppUser lookup for the users multi-select |

> Note: `AppRole` uses the string column `RoleId` as its primary key (the `pkid` IDENTITY is a
> display surrogate), so id-routed endpoints take `RoleId` and the Angular service
> `encodeURIComponent`s it.

## Frontend — CMS.NG

```powershell
cd CMS.NG
npm install             # first time
npm start               # ng serve -> http://localhost:4200
ng test --watch=false   # 20 Karma/Jasmine tests
```

- Path aliases: `@env`, `@core/*`, `@features/*`, `@shared/*` (see `tsconfig.json`).
- API base URL comes from `src/environments/environment*.ts` (no dev-server proxy).
- PrimeNG v20 (Aura theme); sidebar nav has the `系統管理 Admin → 角色 AppRole` entry wired to `/app-roles`.
