// Auth DTOs shared by the login page, service, interceptor and guard.

export interface LoginRequest {
  userId: string;
  password: string;
}

/** Shape returned by POST /api/Auth/login. */
export interface LoginResponse {
  userId: string;
  userName: string;
  accessToken: string;
}

/** The signed-in user as held in memory (roles decoded from the JWT). */
export interface AuthProfile {
  userId: string;
  userName: string;
  accessToken: string;
  roles: string[];
}

/** Shape returned by PUT /api/Auth/profile. */
export interface ProfileResponse {
  userId: string;
  userName: string;
}
