import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthApi, LogoutReason } from './auth.api';

interface Session {
  token: string;
  username: string;
  displayName: string;
  expiresAt: string;
}

const STORAGE_KEY = 'commerce.session';

/**
 * The session is kept in memory and in sessionStorage (never localStorage). sessionStorage belongs to the tab:
 * a reload keeps it, and the browser deletes it when the tab or the browser is closed. When the page leaves it
 * tells the server, which closes and audits the session unless the page comes back (reload) within seconds.
 */
@Injectable({ providedIn: 'root' })
export class AuthState {
  private readonly api = inject(AuthApi);
  private readonly session = signal<Session | null>(null);

  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly displayName = computed(() => this.session()?.displayName ?? '');

  token(): string | null {
    return this.session()?.token ?? null;
  }

  /** Throws ApiError with a message for the user when the login is refused. */
  async login(username: string, password: string): Promise<void> {
    const result = await firstValueFrom(this.api.login(username, password));

    this.setSession({
      token: result.accessToken,
      username: result.username,
      displayName: result.displayName,
      expiresAt: result.expiresAt,
    });
  }

  /**
   * After a reload: takes the session back from sessionStorage and tells the server that the page came back.
   * If the server no longer accepts it (closed or expired), the session is forgotten.
   */
  async restore(): Promise<void> {
    const saved = this.readSaved();
    if (!saved) {
      return;
    }

    this.session.set(saved);

    try {
      await firstValueFrom(this.api.resume());
    } catch {
      this.expire();
    }
  }

  /** The page came back from the browser cache (back/forward) after announcing that it was leaving. */
  async comeBack(): Promise<void> {
    if (!this.session()) {
      return;
    }

    try {
      await firstValueFrom(this.api.resume());
    } catch {
      this.expire();
    }
  }

  /** Tells the server (which audits it and closes the token) and forgets the session, even if the call fails. */
  async logout(reason: LogoutReason = 'manual'): Promise<void> {
    try {
      await firstValueFrom(this.api.logout(reason));
    } catch {
      // The session is closed here anyway; the token expires by itself
    } finally {
      this.setSession(null);
    }
  }

  /** The server already refused the token (expired or closed): only forget it. */
  expire(): void {
    this.setSession(null);
  }

  /**
   * The page is going away (tab or browser closed, or reload). It cannot know which one, so it announces it: the
   * server closes and audits the session after a few seconds unless the page comes back. The request uses
   * `keepalive`, so the browser sends it even while the page unloads. The session stays in sessionStorage for
   * the reload; if the tab is closed, the browser deletes it.
   */
  leave(): void {
    const current = this.session();
    if (!current) {
      return;
    }

    fetch(`${environment.apiUrl}/api/auth/closing`, {
      method: 'POST',
      keepalive: true,
      headers: { Authorization: `Bearer ${current.token}` },
    }).catch(() => undefined);
  }

  private setSession(value: Session | null): void {
    this.session.set(value);

    try {
      if (value) {
        sessionStorage.setItem(STORAGE_KEY, JSON.stringify(value));
      } else {
        sessionStorage.removeItem(STORAGE_KEY);
      }
    } catch {
      // Storage blocked: the session just does not survive a reload
    }
  }

  private readSaved(): Session | null {
    try {
      const raw = sessionStorage.getItem(STORAGE_KEY);
      const value = raw ? (JSON.parse(raw) as Partial<Session>) : null;

      if (value && typeof value.token === 'string' && typeof value.username === 'string' && typeof value.displayName === 'string' && typeof value.expiresAt === 'string') {
        return value as Session;
      }
    } catch {
      // Unreadable: treated as no session
    }

    return null;
  }
}
