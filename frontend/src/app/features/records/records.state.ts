import { Injectable, inject, signal } from '@angular/core';
import { EMPTY, Observable, Subject, Subscription, catchError, exhaustMap, finalize, tap } from 'rxjs';
import { ApiError, toApiError } from '../../core/api-error';
import { CommerceApi } from '../../core/commerce-api.service';
import { CommerceRecord, ProcessResult } from '../../core/models';

/**
 * State of the Registros screen. It lives while the app is open, so the date and the results stay
 * when the user moves to another tab. The table always belongs to the date in `filter`: when the
 * date changes, the results are cleared.
 */
@Injectable({ providedIn: 'root' })
export class RecordsState {
  private readonly api = inject(CommerceApi);
  private readonly process$ = new Subject<void>();
  private request: Subscription | null = null;

  readonly filter = signal('');
  readonly records = signal<CommerceRecord[]>([]);
  /** True when `records` is the answer for the date in `filter`. */
  readonly searched = signal(false);
  readonly loading = signal(false);
  readonly processing = signal(false);
  readonly result = signal<ProcessResult | null>(null);
  readonly error = signal<ApiError | null>(null);

  constructor() {
    // exhaustMap ignores a second click while the first request is still running
    this.process$.pipe(exhaustMap(() => this.runProcess())).subscribe();
  }

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

    this.request = this.api.listCommerce(date).subscribe({
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

  process(): void {
    if (this.filter() && !this.processing()) {
      this.process$.next();
    }
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
    this.result.set(null);
    this.error.set(null);
  }

  private runProcess(): Observable<unknown> {
    const date = this.filter();

    this.processing.set(true);
    this.result.set(null);
    this.error.set(null);

    return this.api.processDate(date).pipe(
      tap((result) => {
        if (this.filter() === date) {
          this.result.set(result);
          this.search();
        }
      }),
      catchError((failure: unknown) => {
        if (this.filter() === date) {
          this.error.set(toApiError(failure));
        }
        return EMPTY;
      }),
      finalize(() => this.processing.set(false)),
    );
  }
}
