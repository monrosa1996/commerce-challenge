import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthApi } from './auth.api';
import { AuthState } from './auth.state';
import { registerUnloadHandlers } from './session-guard';

function setup() {
  vi.stubGlobal('fetch', vi.fn(() => Promise.resolve(new Response(null, { status: 204 }))));
  TestBed.configureTestingModule({
    providers: [
      {
        provide: AuthApi,
        useValue: { login: () => of({ accessToken: 't', expiresAt: '2030-01-01T00:00:00Z', username: 'admin', displayName: 'A' }) },
      },
    ],
  });

  const auth = TestBed.inject(AuthState);
  const target = new EventTarget();
  registerUnloadHandlers(auth, target as unknown as Window);

  return { auth, target };
}

describe('registerUnloadHandlers', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    sessionStorage.clear();
  });

  it('asks the browser to confirm before leaving while there is a session', async () => {
    const { auth, target } = setup();
    await auth.login('admin', 'x');
    const event = new Event('beforeunload', { cancelable: true });

    target.dispatchEvent(event);

    expect(event.defaultPrevented).toBe(true);
  });

  it('does not ask anything when there is no session', () => {
    const { target } = setup();
    const event = new Event('beforeunload', { cancelable: true });

    target.dispatchEvent(event);

    expect(event.defaultPrevented).toBe(false);
  });

  it('announces to the server that the page is leaving, and keeps the session for a reload', async () => {
    const { auth, target } = setup();
    await auth.login('admin', 'x');

    target.dispatchEvent(new Event('pagehide'));

    expect(fetch).toHaveBeenCalledTimes(1);
    expect(auth.isAuthenticated()).toBe(true);
  });

  it('resumes the session when the page comes back from the browser cache', async () => {
    const { auth, target } = setup();
    await auth.login('admin', 'x');
    const comeBack = vi.spyOn(auth, 'comeBack').mockResolvedValue();

    target.dispatchEvent(Object.assign(new Event('pageshow'), { persisted: true }));
    target.dispatchEvent(Object.assign(new Event('pageshow'), { persisted: false }));

    expect(comeBack).toHaveBeenCalledTimes(1);
  });

  it('sends nothing when the page leaves without a session', () => {
    const { target } = setup();

    target.dispatchEvent(new Event('pagehide'));

    expect(fetch).not.toHaveBeenCalled();
  });
});
