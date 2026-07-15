import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '@env/environment';
import { AuthProfile, LoginRequest } from './auth.model';

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
