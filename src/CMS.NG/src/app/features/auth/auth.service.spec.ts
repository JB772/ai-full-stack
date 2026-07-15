import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '@env/environment';
import { AUTH_STORAGE_KEY, AuthService } from './auth.service';
import { AuthProfile } from './auth.model';

/** Builds a syntactically valid (unsigned) JWT so the service can decode its `role` claim. */
function fakeJwt(payload: Record<string, unknown>): string {
  const encode = (value: unknown) =>
    btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode(payload)}.signature`;
}

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  const loginUrl = `${environment.apiBaseUrl}/Auth/login`;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('POSTs credentials and stores the profile with roles decoded from the token in session storage', () => {
    const token = fakeJwt({ userId: 'admin', userName: 'admin', role: ['Admin', 'Editor'] });

    let result: AuthProfile | undefined;
    service.login({ userId: 'admin', password: 'P@ssw0rd' }).subscribe(p => (result = p));

    const req = httpMock.expectOne(loginUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ userId: 'admin', password: 'P@ssw0rd' });
    req.flush({ userId: 'admin', userName: '系統管理員', accessToken: token });

    expect(result).toBeTruthy();
    expect(service.isAuthenticated()).toBeTrue();
    expect(service.token).toBe(token);
    expect(service.userName()).toBe('系統管理員');
    expect(service.roles()).toEqual(['Admin', 'Editor']);
    expect(service.isAdmin()).toBeTrue();

    const stored = JSON.parse(sessionStorage.getItem(AUTH_STORAGE_KEY)!) as AuthProfile;
    expect(stored.accessToken).toBe(token);
    expect(stored.roles).toEqual(['Admin', 'Editor']);
  });

  it('treats a single role claim (serialized as a string) as one role', () => {
    const token = fakeJwt({ role: 'Editor' });
    service.login({ userId: 'editor', password: 'x' }).subscribe();
    httpMock.expectOne(loginUrl).flush({ userId: 'editor', userName: 'editor', accessToken: token });

    expect(service.roles()).toEqual(['Editor']);
    expect(service.isAdmin()).toBeFalse();
  });

  it('logout clears session storage and the auth state', () => {
    const token = fakeJwt({ role: 'Admin' });
    service.login({ userId: 'admin', password: 'x' }).subscribe();
    httpMock.expectOne(loginUrl).flush({ userId: 'admin', userName: 'admin', accessToken: token });
    expect(service.isAuthenticated()).toBeTrue();

    service.logout();

    expect(service.isAuthenticated()).toBeFalse();
    expect(service.token).toBeNull();
    expect(sessionStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
  });
});
