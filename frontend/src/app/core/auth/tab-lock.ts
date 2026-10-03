import { Injectable, signal } from '@angular/core';

const LOCK_NAME = 'commerce-web-single-tab';

/**
 * Lets only one tab of the browser use the application. The first tab takes a lock that the browser
 * releases by itself when that tab closes; another tab finds it taken and shows a message.
 * It covers the tabs and windows of the same browser profile. It is a convenience of the screen, not a
 * security control: another browser, a private window or another computer is not covered.
 */
@Injectable({ providedIn: 'root' })
export class TabLock {
  private readonly isBlocked = signal(false);

  /** True when the application is already open in another tab. */
  readonly blocked = this.isBlocked.asReadonly();

  async acquire(): Promise<void> {
    this.isBlocked.set(!(await this.tryHold()));
  }

  private tryHold(): Promise<boolean> {
    const locks = typeof navigator === 'undefined' ? undefined : navigator.locks;
    if (!locks) {
      // An old browser without Web Locks: the application works, only without this control
      return Promise.resolve(true);
    }

    return new Promise<boolean>((resolve) => {
      void locks.request(LOCK_NAME, { ifAvailable: true }, (lock) => {
        if (!lock) {
          resolve(false);
          return undefined;
        }

        resolve(true);

        // A promise that never ends keeps the lock until the tab closes
        return new Promise<void>(() => undefined);
      });
    });
  }
}
