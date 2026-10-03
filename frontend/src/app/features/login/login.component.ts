import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ApiError, toApiError } from '../../core/api-error';
import { AuthState } from '../../core/auth/auth.state';

@Component({
  selector: 'app-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="login">
      <h1>Iniciar sesión</h1>
      <p class="hint">Ingrese con su usuario para cargar y revisar los comercios.</p>

      @if (expired) {
        <p class="message warn" role="status">Su sesión terminó. Inicie sesión de nuevo.</p>
      }

      @if (error(); as failure) {
        <p class="message error" role="alert">{{ failure.message }}</p>
      }

      <form (submit)="submit($event)">
        <label for="username">Usuario</label>
        <input
          #user
          id="username"
          name="username"
          type="text"
          autocomplete="username"
          [value]="username()"
          (input)="username.set(user.value)"
        />

        <label for="password">Contraseña</label>
        <input
          #pass
          id="password"
          name="password"
          type="password"
          autocomplete="current-password"
          [value]="password()"
          (input)="password.set(pass.value)"
        />

        <button type="submit" [disabled]="loading() || !username() || !password()">Entrar</button>
      </form>
    </section>
  `,
})
export class LoginComponent {
  private readonly auth = inject(AuthState);
  private readonly router = inject(Router);

  protected readonly expired = inject(ActivatedRoute).snapshot.queryParamMap.get('expired') === '1';
  protected readonly username = signal('');
  protected readonly password = signal('');
  protected readonly loading = signal(false);
  protected readonly error = signal<ApiError | null>(null);

  protected async submit(event: Event): Promise<void> {
    event.preventDefault();

    if (this.loading() || !this.username() || !this.password()) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    try {
      await this.auth.login(this.username().trim(), this.password());
      this.password.set('');
      await this.router.navigateByUrl('/carga');
    } catch (failure) {
      this.password.set('');
      this.error.set(toApiError(failure));
    } finally {
      this.loading.set(false);
    }
  }
}
