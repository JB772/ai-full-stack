import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '@env/environment';
import { AuthProfile, ChangePasswordRequest, LoginRequest, ProfileResponse, ResetPasswordRequest } from './auth.model';

/** Session-storage key holding the signed-in {@link AuthProfile}. */
export const AUTH_STORAGE_KEY = 'auth-profile';

/**
 * Owns the signed-in session. The profile lives in **session** storage (cleared when the tab closes),
 * and is mirrored into a signal so the shell reacts to login/logout. Roles are read from the JWT, not
 * a separate endpoint.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly loginUrl = `${environment.apiBaseUrl}/Auth/login`;
  private readonly profileUrl = `${environment.apiBaseUrl}/Auth/profile`;
  private readonly changePasswordUrl = `${environment.apiBaseUrl}/Auth/change-password`;
  private readonly resetPasswordUrl = `${environment.apiBaseUrl}/Auth/reset-password`;

  private readonly profileSignal = signal<AuthProfile | null>(this.readProfile());

  readonly profile = this.profileSignal.asReadonly();
  readonly userName = computed(() => this.profileSignal()?.userName ?? '');
  readonly roles = computed(() => this.profileSignal()?.roles ?? []);
  readonly isAuthenticated = computed(() => !!this.profileSignal()?.accessToken);
  readonly isAdmin = computed(() => this.roles().includes('Admin'));

  /** Current bearer token, or null when signed out. */
  get token(): string | null {
    return this.profileSignal()?.accessToken ?? null;
  }

  login(request: LoginRequest): Observable<AuthProfile> {
    return this.http.post<AuthProfile>(this.loginUrl, request).pipe(
      tap(response => this.setSession(response))
    );
  }

  logout(): void {
    this.clearSession();
  }

  /**
   * Updates the signed-in user's own display name. The server derives the identity from the JWT, so
   * only `userName` is sent. On success the new name is written back to the session profile and signal,
   * so the shell (and anything reading `userName`) refreshes immediately.
   */
  updateProfile(userName: string): Observable<ProfileResponse> {
    return this.http.put<ProfileResponse>(this.profileUrl, { userName }).pipe(
      tap(response => this.applyUserName(response.userName))
    );
  }

  /**
   * Changes the signed-in user's own password. The server derives identity from the JWT and verifies
   * the current password server-side. No session/token state changes on success (the token stays valid),
   * so there is nothing to persist here — the caller just surfaces success/failure.
   */
  changePassword(request: ChangePasswordRequest): Observable<void> {
    return this.http.post<void>(this.changePasswordUrl, request);
  }

  /**
   * Admin-only: resets another account's password to the system default. The client sends only the
   * target `userId` — no password or hash crosses the API in either direction. The server enforces the
   * Admin role (a non-Admin gets 403). Touches no session state — the admin's own token/profile are
   * unaffected — so there is nothing to persist here.
   */
  resetPasswordToDefault(userId: string): Observable<void> {
    const request: ResetPasswordRequest = { userId };
    return this.http.post<void>(this.resetPasswordUrl, request);
  }

  /** Refreshes just the userName in the persisted profile + signal, leaving token and roles intact. */
  private applyUserName(userName: string): void {
    const current = this.profileSignal();
    if (!current) {
      return;
    }
    const updated: AuthProfile = { ...current, userName };
    sessionStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(updated));
    this.profileSignal.set(updated);
  }

  /** Persists the profile (with roles decoded from the token) to session storage. */
  private setSession(response: AuthProfile): void {
    const profile: AuthProfile = {
      userId: response.userId,
      userName: response.userName,
      accessToken: response.accessToken,
      roles: this.decodeRoles(response.accessToken)
    };
    sessionStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(profile));
    this.profileSignal.set(profile);
  }

  /** Clears the session everywhere (used on logout and on any 401). */
  clearSession(): void {
    sessionStorage.removeItem(AUTH_STORAGE_KEY);
    this.profileSignal.set(null);
  }

  private readProfile(): AuthProfile | null {
    const raw = sessionStorage.getItem(AUTH_STORAGE_KEY);
    if (!raw) {
      return null;
    }
    try {
      const parsed = JSON.parse(raw) as AuthProfile;
      return { ...parsed, roles: parsed.roles ?? [] };
    } catch {
      return null;
    }
  }

  /** Reads the `role` claim from the JWT payload. A single role serializes as a string, several as an array. */
  private decodeRoles(token: string): string[] {
    const claims = this.decodeToken(token);
    const role = claims?.['role'];
    if (Array.isArray(role)) {
      return role.map(String);
    }
    if (typeof role === 'string') {
      return [role];
    }
    return [];
  }

  private decodeToken(token: string): Record<string, unknown> | null {
    const payload = token.split('.')[1];
    if (!payload) {
      return null;
    }
    try {
      const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
      const padded = base64 + '='.repeat((4 - (base64.length % 4)) % 4);
      const decoded = atob(padded);
      const json = decodeURIComponent(
        decoded
          .split('')
          .map(c => '%' + c.charCodeAt(0).toString(16).padStart(2, '0'))
          .join('')
      );
      return JSON.parse(json) as Record<string, unknown>;
    } catch {
      return null;
    }
  }
}
