import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { environment } from '../../../environments/environment';
import { ApiError } from '../api-error';
import { LoginResult } from '../models';
import { AuthApi } from './auth.api';
import { AuthState } from './auth.state';

const result: LoginResult = {
  accessToken: 'token-123',
  expiresAt: '2030-01-01T00:30:00Z',
  username: 'admin',
  displayName: 'Administrador',
};

function setup(api: Partial<AuthApi>): AuthState {
  TestBed.configureTestingModule({ providers: [{ provide: AuthApi, useValue: api }] });
  return TestBed.inject(AuthState);
}

describe('AuthState', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    sessionStorage.clear();
  });
  afterEach(() => vi.unstubAllGlobals());

  it('starts without a session', () => {
    const state = setup({});

    expect(state.isAuthenticated()).toBe(false);
    expect(state.token()).toBeNull();
  });

  it('login opens the session and keeps the token', async () => {
    const state = setup({ login: () => of(result) });

    await state.login('admin', 'secret');

    expect(state.isAuthenticated()).toBe(true);
    expect(state.token()).toBe('token-123');
    expect(state.displayName()).toBe('Administrador');
  });

  it('keeps the session in sessionStorage, never in localStorage, and removes it on logout', async () => {
    const make = () => ({ setItem: vi.fn(), getItem: vi.fn(), removeItem: vi.fn(), clear: vi.fn(), key: vi.fn(), length: 0 });
    const local = make();
    const session = make();
    vi.stubGlobal('localStorage', local);
    vi.stubGlobal('sessionStorage', session);
    const state = setup({ login: () => of(result), logout: () => of(undefined) });

    await state.login('admin', 'secret');
    expect(session.setItem).toHaveBeenCalledTimes(1);

    await state.logout('manual');
    expect(session.removeItem).toHaveBeenCalledWith('commerce.session');
    expect(local.setItem).not.toHaveBeenCalled();
  });

  it('restore takes the session back after a reload and tells the server the page came back', async () => {
    const resume = vi.fn(() => of(undefined));
    const first = setup({ login: () => of(result) });
    await first.login('admin', 'secret');
    TestBed.resetTestingModule();

    const state = setup({ resume });
    await state.restore();

    expect(resume).toHaveBeenCalledTimes(1);
    expect(state.isAuthenticated()).toBe(true);
    expect(state.token()).toBe('token-123');
  });

  it('restore forgets the session when the server no longer accepts it', async () => {
    const first = setup({ login: () => of(result) });
    await first.login('admin', 'secret');
    TestBed.resetTestingModule();

    const state = setup({ resume: () => throwError(() => new ApiError('Sesión cerrada', 401)) });
    await state.restore();

    expect(state.isAuthenticated()).toBe(false);
    expect(sessionStorage.getItem('commerce.session')).toBeNull();
  });

  it('restore does nothing when nothing was saved', async () => {
    const resume = vi.fn(() => of(undefined));
    const state = setup({ resume });

    await state.restore();

    expect(resume).not.toHaveBeenCalled();
    expect(state.isAuthenticated()).toBe(false);
  });

  it('restore ignores a saved value that is damaged', async () => {
    sessionStorage.setItem('commerce.session', '{"token":1}');
    const state = setup({ resume: vi.fn() });

    await state.restore();

    expect(state.isAuthenticated()).toBe(false);
    sessionStorage.removeItem('commerce.session');
  });

  it('a refused login leaves no session and passes the message on', async () => {
    const state = setup({ login: () => throwError(() => new ApiError('Usuario o contraseña incorrectos.', 401)) });

    await expect(state.login('admin', 'wrong')).rejects.toThrow('Usuario o contraseña incorrectos.');

    expect(state.isAuthenticated()).toBe(false);
  });

  it('logout tells the server the reason and closes the session', async () => {
    const logout = vi.fn(() => of(undefined));
    const state = setup({ login: () => of(result), logout });
    await state.login('admin', 'secret');

    await state.logout('manual');

    expect(logout).toHaveBeenCalledWith('manual');
    expect(state.isAuthenticated()).toBe(false);
    expect(state.token()).toBeNull();
  });

  it('logout closes the session here even if the server cannot be reached', async () => {
    const state = setup({
      login: () => of(result),
      logout: () => throwError(() => new ApiError('No se pudo conectar', 0)),
    });
    await state.login('admin', 'secret');

    await state.logout('manual');

    expect(state.isAuthenticated()).toBe(false);
  });

  it('expire forgets the token', async () => {
    const state = setup({ login: () => of(result) });
    await state.login('admin', 'secret');

    state.expire();

    expect(state.isAuthenticated()).toBe(false);
  });

  it('leave announces the exit with keepalive and the token, and keeps the session for a reload', async () => {
    const fetchMock = vi.fn(() => Promise.resolve(new Response(null, { status: 204 })));
    vi.stubGlobal('fetch', fetchMock);
    const state = setup({ login: () => of(result) });
    await state.login('admin', 'secret');

    state.leave();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
    expect(url).toBe(`${environment.apiUrl}/api/auth/closing`);
    expect(init.keepalive).toBe(true);
    expect(init.method).toBe('POST');
    expect((init.headers as Record<string, string>)['Authorization']).toBe('Bearer token-123');
    expect(state.isAuthenticated()).toBe(true);
    expect(sessionStorage.getItem('commerce.session')).not.toBeNull();
  });

  it('leave does nothing when there is no session', () => {
    const fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
    const state = setup({});

    state.leave();

    expect(fetchMock).not.toHaveBeenCalled();
  });
});
