import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AppLocation } from './core/auth/app-location';
import { AuthState } from './core/auth/auth.state';
import { TabLock } from './core/auth/tab-lock';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    @if (tabLock.blocked()) {
      <main>
        <section class="login">
          <h1>La aplicación ya está abierta</h1>
          <p class="message warn" role="status">
            Este sistema ya está abierto en otra pestaña o ventana del navegador. Úselo allí, o ciérrelo y pulse Reintentar.
          </p>
          <button type="button" (click)="tabLock.acquire()">Reintentar</button>
        </section>
      </main>
    } @else {
      @if (auth.isAuthenticated()) {
        <nav aria-label="Principal">
          <a routerLink="/carga" routerLinkActive="active">Carga</a>
          <a routerLink="/registros" routerLinkActive="active">Registros</a>
          <a routerLink="/cuarentena" routerLinkActive="active">Cuarentena</a>
          <span class="spacer"></span>
          <span class="nav-user">{{ auth.displayName() }}</span>
          <button type="button" class="secondary" [disabled]="leaving()" (click)="logout()">Cerrar sesión</button>
        </nav>
      }
      <main>
        <router-outlet />
      </main>
    }
  `,
})
export class AppComponent {
  protected readonly auth = inject(AuthState);
  protected readonly tabLock = inject(TabLock);
  private readonly location = inject(AppLocation);

  protected readonly leaving = signal(false);

  protected async logout(): Promise<void> {
    this.leaving.set(true);
    await this.auth.logout('manual');
    // A full navigation empties the memory of the page: nothing of this user stays for the next one
    this.location.assign('/login');
  }
}
