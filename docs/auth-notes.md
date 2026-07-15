# Auth subsystem notes

JWT authentication/authorization and the profile/password flows (Lab 05). Read before touching
login, tokens, roles, or any password reset/change. General run/test setup is in
[setup-notes.md](setup-notes.md).

## Model / conventions

- **Password hash:** SHA-256, **lowercase hex** (`Security/PasswordHasher.Sha256Hex`) — the format
  seeded since Lab 03. Login compares with `OrdinalIgnoreCase`. Hashing is **server-side only**; no
  password or hash ever crosses the wire (requests send plaintext over TLS; responses never include a hash).
- **JWT:** HS256, 24-hour lifetime (`Security/TokenService`). Claims: `sub`, `jti`, `userId`,
  `userName`, and one **`ClaimTypes.Role`** claim per role (serialized as the long
  `http://schemas.microsoft.com/ws/2008/06/identity/claims/role` URI).
- **Signing key:** the `symmetricSecurityKey` property of the JSON in `SysConfig` where
  `configKey = 'appConfig'` (same JSON also holds `defaultPassword`). Read at runtime — never
  hard-coded — via `IAuthRepository.GetSigningKeyAsync`; cached process-wide by `IJwtSigningKeyProvider`.
- **Password policy** (`Security/PasswordPolicy`): length ≥ 8 **and** ≥ 3 of 4 classes {upper, lower,
  digit, symbol}. Bilingual `ComplexityMessage` constant is mirrored client-side in
  `core/auth/password.validators.ts` (`PASSWORD_COMPLEXITY_MESSAGE`).
- `PasswordUpdatedTime` is written with **`GETUTCDATE()`** (UTC). Some older seed rows hold local time.

## Backend (`CMS.API`)

- **Authentication:** `AddAuthentication().AddJwtBearer()`; `ConfigureJwtBearerOptions`
  (`IPostConfigureOptions<JwtBearerOptions>`) sets validation params — no issuer/audience, validate
  lifetime + signing key, `RoleClaimType = ClaimTypes.Role`, key from `IJwtSigningKeyProvider`.
- **Authorization:** global **fallback policy** `RequireAuthenticatedUser()` in `Program.cs` → every
  endpoint needs a token unless it has `[AllowAnonymous]`. Middleware order: `UseAuthentication()`
  **before** `UseAuthorization()`.
- **`[AllowAnonymous]` goes on the `Login` action, NOT the `AuthController` class** — a class-level
  `[AllowAnonymous]` overrides action-level `[Authorize]` and would leak onto the other Auth endpoints.
- **Endpoints** (all under `/api/Auth`; every one except login resolves the user from the JWT's
  `userId` claim — never from the body):
  | Route | Guard | Behaviour |
  |-------|-------|-----------|
  | `POST /login` | `[AllowAnonymous]` | verify IsActive=1 + `PasswordHash == SHA256(pw)`; returns `{userId, userName, accessToken}`; generic 401 on any failure |
  | `PUT /profile` | `[Authorize]` | update **UserName only** for the JWT user (required, trimmed); ignores any body `userId` |
  | `POST /change-password` | `[Authorize]` | verify current pw → complexity → new==confirm → set `PasswordHash`+`PasswordUpdatedTime` |
  | `POST /reset-password` | `[Authorize(Roles="Admin")]` | body `{userId}`; set target to `SHA256(defaultPassword)`+timestamp; non-Admin → **403** |
- **`AuthRepository`:** reads appConfig props via a shared helper (`GetSigningKeyAsync`,
  `GetDefaultPasswordAsync`); `GetLoginUserAsync` (user + roles), `UpdateUserNameAsync`,
  `UpdatePasswordAsync` (stamps `PasswordUpdatedTime = GETUTCDATE()`).

## Frontend (`CMS.NG`, `core/auth/`)

- **`auth.service.ts`** — `login` / `clear` (logout) / `updateUserName` / `changePassword` /
  `resetPassword`. Signals: `userName`, `userId`, `roles`, `isAdmin`, `isAuthenticated`. Stores
  **three session-storage keys**: `userId`, `userName`, `accessToken`. Roles are decoded from the JWT
  payload (handles the long `ClaimTypes.Role` URI, plus `role`/`roles`).
- **`auth.interceptor.ts`** — attaches `Authorization: Bearer <token>` to `apiBaseUrl` calls; on a
  **401** clears the session and redirects to `/login` (skips the login endpoint).
- **`auth.guard.ts`** — `CanActivateChildFn`; redirects to `/login?returnUrl=…` when unauthenticated.
- **`password.validators.ts`** — `passwordComplexityValidator` + `passwordsMatchValidator` +
  `PASSWORD_COMPLEXITY_MESSAGE` (mirror of the backend policy).
- **Routes:** `login` is public; everything else sits under a pathless parent with
  `canActivateChild: [authGuard]`. Interceptor wired via
  `provideHttpClient(withFetch(), withInterceptors([authInterceptor]))`.
- **Shell (`app.ts` / `app.html`):** chrome (sidebar + header) renders only when authenticated; the
  header shows `userName` as a link to `/profile` + a logout button; the **系統管理 Admin** nav group is
  gated by `auth.isAdmin()` (`requiresRole` on the group).
- **Pages:** `features/auth/login`; `features/profile` (display name + change password);
  `features/app-users` (minimal list + edit hosting the Admin-only **Reset Password** button).

## Gotchas / decisions

- **No AppUser CRUD existed** (Lab 03's was never built in this repo). Step 5 added only a *minimal*
  AppUser list + edit to host the reset button; user data is sourced from `/api/lookups/app-users`
  (there is no single-user GET).
- Admin visibility is enforced **server-side** (`[Authorize(Roles="Admin")]` → 403), not just by
  hiding the button.
- **Tests:** auth/authorization use `WebApplicationFactory<Program>` with `ConfigureTestServices`
  overriding `IJwtSigningKeyProvider` (fixed 32-byte test key) and the repos (Moq). Front-end specs
  build a JWT with a small base64url helper; any component using `AuthService` must also provide
  `provideHttpClient()`.
- Packages added: `Microsoft.AspNetCore.Authentication.JwtBearer` 9.0.16 (API),
  `Microsoft.AspNetCore.Mvc.Testing` 9.0.16 (Tests), `System.IdentityModel.Tokens.Jwt` 8.16.0 (API).
