import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it } from 'vitest';
import { AuthApi } from './auth.api';
import { authGuard, loginGuard } from './auth.guard';
import { AuthState } from './auth.state';

const route = {} as ActivatedRouteSnapshot;
const routerState = {} as RouterStateSnapshot;

function setup() {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      {
        provide: AuthApi,
        useValue: { login: () => of({ accessToken: 't', expiresAt: '2030-01-01T00:00:00Z', username: 'admin', displayName: 'A' }) },
      },
    ],
  });

  return { auth: TestBed.inject(AuthState), router: TestBed.inject(Router) };
}

const run = (guard: typeof authGuard) => TestBed.runInInjectionContext(() => guard(route, routerState));

describe('guards', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('authGuard sends the user to the login when there is no session', () => {
    const { router } = setup();

    const result = run(authGuard);

    expect(router.serializeUrl(result as UrlTree)).toBe('/login');
  });

  it('authGuard lets the user in with a session', async () => {
    const { auth } = setup();
    await auth.login('admin', 'x');

    expect(run(authGuard)).toBe(true);
  });

  it('loginGuard shows the login when there is no session', () => {
    setup();

    expect(run(loginGuard)).toBe(true);
  });

  it('loginGuard sends a user with a session to the app', async () => {
    const { auth, router } = setup();
    await auth.login('admin', 'x');

    expect(router.serializeUrl(run(loginGuard) as UrlTree)).toBe('/carga');
  });
});
