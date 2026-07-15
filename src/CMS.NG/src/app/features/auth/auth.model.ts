/** Credentials posted to POST /api/Auth/login. */
export interface LoginRequest {
  userId: string;
  password: string;
}

/**
 * Signed-in profile. `userId`/`userName`/`accessToken` come straight from the login response;
 * `roles` are decoded from the JWT's `role` claim(s) at login time and persisted alongside it, so
 * role checks never need a separate API call.
 */
export interface AuthProfile {
  userId: string;
  userName: string;
  accessToken: string;
  roles: string[];
}

/** Body posted to PUT /api/Auth/profile. Only userName is honoured; the server takes userId from the JWT. */
export interface UpdateProfileRequest {
  userName: string;
}

/** Response from PUT /api/Auth/profile. userId/roles are read-only display fields. */
export interface ProfileResponse {
  userId: string;
  userName: string;
  roles: string[];
}

/**
 * Body posted to POST /api/Auth/change-password. The server derives identity from the JWT, so no
 * userId is sent. All three are plaintext, used only in transit; the server never returns any hash.
 */
export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmNewPassword: string;
}
