import { inject } from '@angular/core';
import { CanActivateChildFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/**
 * Blocks protected routes when there is no token in session storage, redirecting to the login page.
 * The login route sits outside the guarded subtree, so it stays public.
 */
export const authGuard: CanActivateChildFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.isAuthenticated() ? true : router.createUrlTree(['/login']);
};
