import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthState } from './auth.state';

/** Screens that need a session. Without one, the user goes to the login. */
export const authGuard: CanActivateFn = () => {
  return inject(AuthState).isAuthenticated() ? true : inject(Router).createUrlTree(['/login']);
};

/** The login screen is not for a user who already has a session. */
export const loginGuard: CanActivateFn = () => {
  return inject(AuthState).isAuthenticated() ? inject(Router).createUrlTree(['/carga']) : true;
};
