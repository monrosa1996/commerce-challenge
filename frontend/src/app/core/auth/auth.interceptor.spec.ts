import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { environment } from '../../../environments/environment';
import { ApiError } from '../api-error';
import { errorInterceptor } from '../error.interceptor';
import { LoginResult } from '../models';
import { AppLocation } from './app-location';
import { AuthApi } from './auth.api';
import { authInterceptor } from './auth.interceptor';
import { AuthState } from './auth.state';

const login: LoginResult = { accessToken: 'token-123', expiresAt: '2030-01-01T00:30:00Z', username: 'admin', displayName: 'Administrador' };
const assign = vi.fn();

function setup() {
  assign.mockClear();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
      provideHttpClientTesting(),
      { provide: AuthApi, useValue: { login: () => of(login) } },
      { provide: AppLocation, useValue: { assign } },
    ],
  });

  return {
    http: TestBed.inject(HttpClient),
    controller: TestBed.inject(HttpTestingController),
    auth: TestBed.inject(AuthState),
  };
}

describe('authInterceptor', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('adds the token to the calls to the API', async () => {
    const { http, controller, auth } = setup();
    await auth.login('admin', 'secret');

    http.get(`${environment.apiUrl}/api/commerce`).subscribe();

    expect(controller.expectOne(`${environment.apiUrl}/api/commerce`).request.headers.get('Authorization')).toBe('Bearer token-123');
  });

  it('does not send the token to other addresses', async () => {
    const { http, controller, auth } = setup();
    await auth.login('admin', 'secret');

    http.get('https://example.com/data').subscribe();

    expect(controller.expectOne('https://example.com/data').request.headers.has('Authorization')).toBe(false);
  });

  it('does not send the token with the login call', async () => {
    const { http, controller, auth } = setup();
    await auth.login('admin', 'secret');

    http.post(`${environment.apiUrl}/api/auth/login`, {}).subscribe({ error: () => undefined });

    expect(controller.expectOne(`${environment.apiUrl}/api/auth/login`).request.headers.has('Authorization')).toBe(false);
  });

  it('sends no header when there is no session', () => {
    const { http, controller } = setup();

    http.get(`${environment.apiUrl}/api/commerce`).subscribe({ error: () => undefined });

    expect(controller.expectOne(`${environment.apiUrl}/api/commerce`).request.headers.has('Authorization')).toBe(false);
  });

  it('when the API answers 401 the session ends and the page goes to the login', async () => {
    const { http, controller, auth } = setup();
    await auth.login('admin', 'secret');
    let failure: unknown;

    http.get(`${environment.apiUrl}/api/commerce`).subscribe({ error: (error: unknown) => (failure = error) });
    controller
      .expectOne(`${environment.apiUrl}/api/commerce`)
      .flush({ title: 'No autorizado', detail: 'Su sesión ha expirado o no es válida. Inicie sesión de nuevo.' }, { status: 401, statusText: 'Unauthorized' });

    expect(auth.isAuthenticated()).toBe(false);
    expect(assign).toHaveBeenCalledWith('/login?expired=1');
    expect(failure).toBeInstanceOf(ApiError);
    expect((failure as ApiError).message).toBe('Su sesión ha expirado o no es válida. Inicie sesión de nuevo.');
  });

  it('a refused login (401) does not send the page anywhere and shows the message', () => {
    const { http, controller } = setup();
    let failure: unknown;

    http.post(`${environment.apiUrl}/api/auth/login`, {}).subscribe({ error: (error: unknown) => (failure = error) });
    controller
      .expectOne(`${environment.apiUrl}/api/auth/login`)
      .flush({ title: 'No autorizado', detail: 'Usuario o contraseña incorrectos.' }, { status: 401, statusText: 'Unauthorized' });

    expect(assign).not.toHaveBeenCalled();
    expect((failure as ApiError).message).toBe('Usuario o contraseña incorrectos.');
  });
});
