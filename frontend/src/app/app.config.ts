import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { AuthState } from './core/auth/auth.state';
import { registerUnloadHandlers } from './core/auth/session-guard';
import { TabLock } from './core/auth/tab-lock';
import { errorInterceptor } from './core/error.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // authInterceptor goes first so it sees the errors that errorInterceptor already turned into ApiError
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
    provideAppInitializer(() => registerUnloadHandlers(inject(AuthState))),
    // The application waits for this before it starts, so a second tab never makes calls to the API.
    // After a reload, the session is taken back before the first route is decided.
    provideAppInitializer(async () => {
      // inject() only works before the first await
      const tabLock = inject(TabLock);
      const auth = inject(AuthState);
      await tabLock.acquire();

      if (!tabLock.blocked()) {
        await auth.restore();
      }
    }),
  ],
};
