import { AuthState } from './auth.state';

/**
 * A page cannot stop the user from closing the tab or the browser. What it can do:
 *  - ask the browser to show its own confirmation while there is a session (beforeunload), and
 *  - announce to the server that the page is leaving (pagehide), so the session is audited and closed if the page does not come back.
 * A reload fires the same event as a close, so the page only announces that it is leaving; the server closes the
 * session after a few seconds unless the page comes back (reload) and resumes it.
 */
export function registerUnloadHandlers(auth: AuthState, target: Window = window): void {
  target.addEventListener('beforeunload', (event) => {
    if (auth.isAuthenticated()) {
      event.preventDefault();
      event.returnValue = true;
    }
  });

  target.addEventListener('pagehide', () => auth.leave());

  // Back/forward can bring the page back from the browser cache without reloading it
  target.addEventListener('pageshow', (event) => {
    if ((event as PageTransitionEvent).persisted) {
      void auth.comeBack();
    }
  });
}
