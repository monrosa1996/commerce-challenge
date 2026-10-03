import { Injectable, inject, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { ApiError, toApiError } from '../../core/api-error';
import { CommerceApi } from '../../core/commerce-api.service';
import { QuarantineRecord } from '../../core/models';

/**
 * State of the Cuarentena screen. It lives while the app is open, so the date and the results stay
 * when the user moves to another tab. When the date changes, the results are cleared.
 */
@Injectable({ providedIn: 'root' })
export class QuarantineState {
  private readonly api = inject(CommerceApi);
  private request: Subscription | null = null;

  readonly filter = signal('');
  readonly records = signal<QuarantineRecord[]>([]);
  /** True when `records` is the answer for the date in `filter`. */
  readonly searched = signal(false);
  readonly loading = signal(false);
  readonly error = signal<ApiError | null>(null);

  setFilter(date: string): void {
    if (date === this.filter()) {
      return;
    }

    this.filter.set(date);
    this.resetResults();
  }

  /** Used by the links from other screens: sets the date and searches it. */
  show(date: string): void {
    this.filter.set(date);
    this.resetResults();
    this.search();
  }

  search(): void {
    const date = this.filter();
    if (!date) {
      return;
    }

    this.request?.unsubscribe();
    this.loading.set(true);
    this.error.set(null);

    this.request = this.api.listQuarantine(date).subscribe({
      next: (records) => {
        this.records.set(records);
        this.searched.set(true);
        this.loading.set(false);
      },
      error: (failure: unknown) => {
        this.records.set([]);
        this.searched.set(false);
        this.error.set(toApiError(failure));
        this.loading.set(false);
      },
    });
  }

  /** Empties the screen and the memory. */
  clear(): void {
    this.filter.set('');
    this.resetResults();
  }

  private resetResults(): void {
    this.request?.unsubscribe();
    this.request = null;
    this.records.set([]);
    this.searched.set(false);
    this.loading.set(false);
    this.error.set(null);
  }
}
