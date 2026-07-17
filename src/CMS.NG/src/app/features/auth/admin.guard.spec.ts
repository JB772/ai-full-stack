import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, UrlTree } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { adminGuard } from './admin.guard';
import { AUTH_STORAGE_KEY } from './auth.service';

/**
 * Regression suite for the reported bug: a newly created account with no Admin role could open the
 * 角色 AppRole page. The guard is the usability half (the boundary is [Authorize(Roles="Admin")] on the
 * server — see AdminAuthorizationTests); this pins that a non-Admin is routed away rather than dropped on a
 * screen that 403s on every request.
 */
describe('adminGuard', () => {
  function signIn(roles: string[]): void {
    sessionStorage.setItem(
      AUTH_STORAGE_KEY,
      JSON.stringify({ userId: 'u1', userName: 'u1', accessToken: 'header.payload.sig', roles })
    );
  }

  function run(): boolean | UrlTree {
    return TestBed.runInInjectionContext(() => adminGuard({} as never, {} as never)) as boolean | UrlTree;
  }

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    });
  });

  afterEach(() => sessionStorage.clear());

  it('lets an Admin through', () => {
    signIn(['Admin']);

    expect(run()).toBeTrue();
  });

  it('redirects a signed-in non-Admin to the home page instead of the Admin page', () => {
    // The exact shape of the report: a new account, no Admin role.
    signIn(['User']);

    const result = run();

    expect(result).not.toBeTrue();
    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/featured-promo-items');
  });

  it('redirects a user with no roles at all', () => {
    signIn([]);

    expect(run()).not.toBeTrue();
  });

  it('is not fooled by a role that merely contains "Admin"', () => {
    // isAdmin() must be an exact match, not a substring test.
    signIn(['NotAdminReally', 'SubAdmin']);

    expect(run()).not.toBeTrue();
  });
});
