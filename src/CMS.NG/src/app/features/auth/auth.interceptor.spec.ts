import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { AUTH_STORAGE_KEY, AuthService } from './auth.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let router: jasmine.SpyObj<Router>;
  let messages: jasmine.SpyObj<MessageService>;

  function seedSession(token: string): void {
    sessionStorage.setItem(
      AUTH_STORAGE_KEY,
      JSON.stringify({ userId: 'admin', userName: 'admin', accessToken: token, roles: ['Admin'] })
    );
  }

  beforeEach(() => {
    sessionStorage.clear();
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    messages = jasmine.createSpyObj<MessageService>('MessageService', ['add']);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: Router, useValue: router },
        { provide: MessageService, useValue: messages }
      ]
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('attaches the Bearer header when a token is present', () => {
    seedSession('token-123');
    TestBed.inject(AuthService); // construct with the seeded session

    http.get('/api/app-users').subscribe();

    const req = httpMock.expectOne('/api/app-users');
    expect(req.request.headers.get('Authorization')).toBe('Bearer token-123');
    req.flush([]);
  });

  it('sends no Authorization header when there is no token', () => {
    http.get('/api/app-users').subscribe();

    const req = httpMock.expectOne('/api/app-users');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush([]);
  });

  it('on a 401 clears session storage and redirects to /login', () => {
    seedSession('token-123');
    const auth = TestBed.inject(AuthService);
    expect(auth.isAuthenticated()).toBeTrue();

    http.get('/api/app-users').subscribe({ next: () => undefined, error: () => undefined });

    const req = httpMock.expectOne('/api/app-users');
    req.flush('unauthorized', { status: 401, statusText: 'Unauthorized' });

    expect(sessionStorage.getItem(AUTH_STORAGE_KEY)).toBeNull();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
    expect(messages.add).not.toHaveBeenCalled();
  });

  it('on a 500 surfaces a friendly toast using the safe message from the body', () => {
    http.get('/api/app-users').subscribe({ next: () => undefined, error: () => undefined });

    const req = httpMock.expectOne('/api/app-users');
    req.flush(
      { message: 'An unexpected error occurred.' },
      { status: 500, statusText: 'Internal Server Error' }
    );

    expect(messages.add).toHaveBeenCalledTimes(1);
    const arg = messages.add.calls.mostRecent().args[0];
    expect(arg.severity).toBe('error');
    expect(arg.detail).toBe('An unexpected error occurred.');
    // A 500 must not touch the session or redirect.
    expect(router.navigate).not.toHaveBeenCalled();
  });

  it('on a 500 with no message body falls back to a generic toast', () => {
    http.get('/api/app-users').subscribe({ next: () => undefined, error: () => undefined });

    const req = httpMock.expectOne('/api/app-users');
    req.flush('boom', { status: 503, statusText: 'Service Unavailable' });

    expect(messages.add).toHaveBeenCalledTimes(1);
    expect(messages.add.calls.mostRecent().args[0].detail).toBe('An unexpected error occurred.');
  });

  it('leaves a validation 400 for the form — no toast, no redirect', () => {
    http.post('/api/publish-statuses', {}).subscribe({ next: () => undefined, error: () => undefined });

    const req = httpMock.expectOne('/api/publish-statuses');
    req.flush({ errors: { description: ['Required'] } }, { status: 400, statusText: 'Bad Request' });

    expect(messages.add).not.toHaveBeenCalled();
    expect(router.navigate).not.toHaveBeenCalled();
  });
});
