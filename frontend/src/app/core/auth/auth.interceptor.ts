import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiError } from '../api-error';
import { AppLocation } from './app-location';
import { AuthState } from './auth.state';

/**
 * Adds the token to the calls to the API. When the API answers 401 with a session open, the session
 * is over (expired or closed): it is forgotten and the page goes to the login, which also empties the memory.
 * It must go before errorInterceptor in the list, so it sees the ApiError.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthState);
  const location = inject(AppLocation);

  const isLogin = request.url.endsWith('/api/auth/login');
  const token = auth.token();
  const sendsToken = request.url.startsWith(environment.apiUrl) && !isLogin && token !== null;

  const outgoing = sendsToken ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;

  return next(outgoing).pipe(
    catchError((error: unknown) => {
      if (error instanceof ApiError && error.status === 401 && auth.isAuthenticated() && !isLogin) {
        auth.expire();
        location.assign('/login?expired=1');
      }

      return throwError(() => error);
    }),
  );
};
