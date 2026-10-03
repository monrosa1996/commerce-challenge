import { Injectable } from '@angular/core';

/** The browser address, behind a class so tests can replace it. A full navigation also clears the memory of the page. */
@Injectable({ providedIn: 'root' })
export class AppLocation {
  assign(url: string): void {
    window.location.assign(url);
  }
}
