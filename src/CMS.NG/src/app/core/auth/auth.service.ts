import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '@env';
import { AuthProfile, LoginResponse, ProfileResponse } from './auth.model';

// Session-storage keys. Kept as the exact field names so they read clearly in DevTools.
export const STORAGE_KEYS = {
  userId: 'userId',
  userName: 'userName',
  accessToken: 'accessToken',
} as const;

/** Role name that unlocks the 系統管理 Admin area. */
export const ADMIN_ROLE = 'Admin';

/** Decode a JWT payload (base64url) without verifying the signature. */
function decodeJwtPayload(token: string): Record<string, unknown> | null {
  const parts = token.split('.');
  if (parts.length < 2) {
    return null;
  }
  try {
    const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
    const json = decodeURIComponent(
      Array.from(atob(padded))
        .map((c) => '%' + c.charCodeAt(0).toString(16).padStart(2, '0'))
        .join(''),
    );
    return JSON.parse(json) as Record<string, unknown>;
  } catch {
    return null;
  }
}

/** Roles carried by the token. The API emits them under the standard ClaimTypes.Role URI. */
function rolesFromToken(token: string): string[] {
  const payload = decodeJwtPayload(token);
  if (!payload) {
    return [];
  }
  const raw =
    payload['role'] ??
    payload['roles'] ??
    payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
  if (raw == null) {
    return [];
  }
  return Array.isArray(raw) ? raw.map(String) : [String(raw)];
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly loginUrl = `${environment.apiBaseUrl}/api/Auth/login`;
  private readonly profileUrl = `${environment.apiBaseUrl}/api/Auth/profile`;
  private readonly changePasswordUrl = `${environment.apiBaseUrl}/api/Auth/change-password`;
  private readonly resetPasswordUrl = `${environment.apiBaseUrl}/api/Auth/reset-password`;

  readonly userId = computed(() => this._profile()?.userId ?? '');

  private readonly _profile = signal<AuthProfile | null>(this.readFromStorage());

  readonly profile = this._profile.asReadonly();
  readonly userName = computed(() => this._profile()?.userName ?? '');
  readonly roles = computed(() => this._profile()?.roles ?? []);
  readonly isAdmin = computed(() => this.roles().includes(ADMIN_ROLE));
  readonly isAuthenticated = computed(() => !!this._profile()?.accessToken);

  /** Current bearer token, or null when signed out. */
  get token(): string | null {
    return this._profile()?.accessToken ?? null;
  }

  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }

  login(userId: string, password: string): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>(this.loginUrl, { userId, password })
      .pipe(tap((res) => this.store(res)));
  }

  /** Update the signed-in user's display name; on success reflect it everywhere it's shown. */
  updateUserName(userName: string): Observable<ProfileResponse> {
    return this.http
      .put<ProfileResponse>(this.profileUrl, { userName })
      .pipe(tap((res) => this.applyUserName(res.userName)));
  }

  private applyUserName(userName: string): void {
    sessionStorage.setItem(STORAGE_KEYS.userName, userName);
    const current = this._profile();
    if (current) {
      this._profile.set({ ...current, userName });
    }
  }

  /** Change the signed-in user's password. Nothing is stored client-side; the JWT identifies the user. */
  changePassword(
    currentPassword: string,
    newPassword: string,
    confirmPassword: string,
  ): Observable<void> {
    return this.http.post<void>(this.changePasswordUrl, {
      currentPassword,
      newPassword,
      confirmPassword,
    });
  }

  /**
   * Admin action: reset another user's password back to the system default. The client only
   * sends the target userId — the default password is read and hashed server-side.
   */
  resetPassword(userId: string): Observable<void> {
    return this.http.post<void>(this.resetPasswordUrl, { userId });
  }

  /** Clear all auth state (session storage + in-memory signal). */
  clear(): void {
    sessionStorage.removeItem(STORAGE_KEYS.userId);
    sessionStorage.removeItem(STORAGE_KEYS.userName);
    sessionStorage.removeItem(STORAGE_KEYS.accessToken);
    this._profile.set(null);
  }

  private store(res: LoginResponse): void {
    sessionStorage.setItem(STORAGE_KEYS.userId, res.userId);
    sessionStorage.setItem(STORAGE_KEYS.userName, res.userName);
    sessionStorage.setItem(STORAGE_KEYS.accessToken, res.accessToken);
    this._profile.set({
      userId: res.userId,
      userName: res.userName,
      accessToken: res.accessToken,
      roles: rolesFromToken(res.accessToken),
    });
  }

  private readFromStorage(): AuthProfile | null {
    const accessToken = sessionStorage.getItem(STORAGE_KEYS.accessToken);
    if (!accessToken) {
      return null;
    }
    return {
      userId: sessionStorage.getItem(STORAGE_KEYS.userId) ?? '',
      userName: sessionStorage.getItem(STORAGE_KEYS.userName) ?? '',
      accessToken,
      roles: rolesFromToken(accessToken),
    };
  }
}
