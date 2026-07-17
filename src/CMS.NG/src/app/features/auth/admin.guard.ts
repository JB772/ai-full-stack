import { inject } from '@angular/core';
import { CanActivateChildFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/**
 * Blocks the「系統管理 Admin」routes for anyone whose JWT does not carry the Admin role, sending them to
 * the home page instead of a screen they cannot use.
 *
 * This is a **usability** guard, not the security boundary. It runs in the browser against a token the user
 * holds, so it is advisory by construction — the real enforcement is `[Authorize(Roles = "Admin")]` on
 * `AppRolesController` / `AppUsersController` and the PublishStatus write actions. Both layers exist on
 * purpose: without the server attribute a non-Admin can still call the API directly; without this guard they
 * get routed to a page that renders and then 403s on every request.
 *
 * Nav hiding (`app.ts`) is a third, purely cosmetic layer — and on its own it was worse than useless, because
 * `/` redirected everyone to `/app-roles`, so a non-Admin landed on the Admin page the menu was hiding.
 */
export const adminGuard: CanActivateChildFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.isAdmin() ? true : router.createUrlTree(['/featured-promo-items']);
};
