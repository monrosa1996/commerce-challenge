import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { TabLock } from './tab-lock';

/** A fake of navigator.locks: the first request takes the lock, the next ones find it taken. */
function fakeLocks() {
  let taken = false;
  const request = vi.fn((_name: string, options: { ifAvailable?: boolean }, callback: (lock: unknown) => unknown) => {
    if (taken && options.ifAvailable) {
      return Promise.resolve(callback(null));
    }

    taken = true;
    return Promise.resolve(callback({}));
  });

  return { request, release: () => (taken = false) };
}

describe('TabLock', () => {
  beforeEach(() => TestBed.resetTestingModule());
  afterEach(() => vi.unstubAllGlobals());

  it('lets the first tab in', async () => {
    vi.stubGlobal('navigator', { locks: fakeLocks() });
    const lock = TestBed.inject(TabLock);

    await lock.acquire();

    expect(lock.blocked()).toBe(false);
  });

  it('asks for the lock only if it is available, so a second tab does not wait', async () => {
    const locks = fakeLocks();
    vi.stubGlobal('navigator', { locks });

    await TestBed.inject(TabLock).acquire();

    expect(locks.request).toHaveBeenCalledWith('commerce-web-single-tab', { ifAvailable: true }, expect.any(Function));
  });

  it('blocks a second tab while the first one has the lock', async () => {
    const locks = fakeLocks();
    vi.stubGlobal('navigator', { locks });
    await TestBed.inject(TabLock).acquire();

    // The second tab is another page: another instance of the service
    TestBed.resetTestingModule();
    const second = TestBed.inject(TabLock);
    await second.acquire();

    expect(second.blocked()).toBe(true);
  });

  it('lets the second tab in after the first one closes', async () => {
    const locks = fakeLocks();
    vi.stubGlobal('navigator', { locks });
    await TestBed.inject(TabLock).acquire();
    TestBed.resetTestingModule();
    const second = TestBed.inject(TabLock);
    await second.acquire();

    locks.release();
    await second.acquire();

    expect(second.blocked()).toBe(false);
  });

  it('does not block when the browser has no Web Locks', async () => {
    vi.stubGlobal('navigator', {});
    const lock = TestBed.inject(TabLock);

    await lock.acquire();

    expect(lock.blocked()).toBe(false);
  });
});
