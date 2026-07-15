import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import {
  ActivatedRouteSnapshot,
  provideRouter,
  Router,
  RouterStateSnapshot,
  UrlTree
} from '@angular/router';
import { AUTH_STORAGE_KEY } from './auth.service';
import { authGuard } from './auth.guard';

describe('authGuard', () => {
  let router: Router;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    });
    router = TestBed.inject(Router);
  });

  afterEach(() => sessionStorage.clear());

  function runGuard() {
    return TestBed.runInInjectionContext(() =>
      authGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot)
    );
  }

  it('redirects to /login when there is no token', () => {
    const result = runGuard();

    expect(result instanceof UrlTree).toBeTrue();
    expect(router.serializeUrl(result as UrlTree)).toBe('/login');
  });

  it('allows activation when a token is present in session storage', () => {
    sessionStorage.setItem(
      AUTH_STORAGE_KEY,
      JSON.stringify({ userId: 'admin', userName: 'admin', accessToken: 'token', roles: [] })
    );

    expect(runGuard()).toBeTrue();
  });
});
