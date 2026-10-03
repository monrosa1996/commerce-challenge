import { HttpEvent, HttpEventType } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { EMPTY, Observable, Subject, catchError, exhaustMap, finalize, takeUntil, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiError, toApiError } from '../../core/api-error';
import { CommerceApi } from '../../core/commerce-api.service';
import { UploadResult } from '../../core/models';
import { CsvValidation, decodeUtf8, validateCsv } from '../../shared/csv-validation';

/**
 * State of the Carga screen. It lives while the app is open, so the chosen file, the preview and the
 * result stay when the user moves to another tab, and an upload in progress keeps running.
 */
@Injectable({ providedIn: 'root' })
export class UploadState {
  private readonly api = inject(CommerceApi);
  private readonly send$ = new Subject<void>();
  private readonly cancel$ = new Subject<void>();

  readonly file = signal<File | null>(null);
  readonly validation = signal<CsvValidation | null>(null);
  readonly uploading = signal(false);
  readonly progress = signal(0);
  readonly result = signal<UploadResult | null>(null);
  /** Dates of the records of the file that was uploaded, to link to Registros. */
  readonly resultDates = signal<string[]>([]);
  readonly error = signal<ApiError | null>(null);
  readonly notice = signal<string | null>(null);

  readonly canSend = computed(
    () => this.file() !== null && this.validation()?.valid === true && !this.uploading(),
  );

  readonly errorRows = computed(() => {
    const rows = this.validation()?.errors.map((item) => item.row) ?? [];
    return new Set(rows.filter((row): row is number => row !== null));
  });

  constructor() {
    // exhaustMap ignores a second click while the first request is still running
    this.send$.pipe(exhaustMap(() => this.upload())).subscribe();
  }

  /** Reads the chosen file and checks it locally. Passing null clears the selection. */
  async select(file: File | null): Promise<void> {
    this.reset();
    this.file.set(file);

    if (!file) {
      return;
    }

    const text = decodeUtf8(await file.arrayBuffer());
    if (this.file() !== file) {
      return;
    }

    this.validation.set(
      validateCsv(
        { fileName: file.name, size: file.size, text },
        { maxFileSizeBytes: environment.maxFileSizeBytes, maxRows: environment.maxRows },
      ),
    );
  }

  submit(): void {
    if (this.canSend()) {
      this.send$.next();
    }
  }

  cancel(): void {
    if (!this.uploading()) {
      return;
    }

    this.cancel$.next();
    this.notice.set('Carga cancelada. Si ya había terminado, al volver a subir el archivo verás un aviso de registros existentes.');
  }

  /** Empties the screen and the memory. It does nothing while an upload is running. */
  clear(): void {
    if (this.uploading()) {
      return;
    }

    this.reset();
    this.file.set(null);
  }

  private reset(): void {
    this.validation.set(null);
    this.result.set(null);
    this.resultDates.set([]);
    this.error.set(null);
    this.notice.set(null);
    this.progress.set(0);
  }

  private upload(): Observable<unknown> {
    const file = this.file();
    if (!file) {
      return EMPTY;
    }

    const dates = this.validation()?.dates ?? [];

    this.uploading.set(true);
    this.progress.set(0);
    this.result.set(null);
    this.resultDates.set([]);
    this.error.set(null);
    this.notice.set(null);

    return this.api.uploadFile(file).pipe(
      takeUntil(this.cancel$),
      tap((event) => this.handle(event, dates)),
      catchError((failure: unknown) => {
        this.error.set(toApiError(failure));
        return EMPTY;
      }),
      finalize(() => this.uploading.set(false)),
    );
  }

  private handle(event: HttpEvent<UploadResult>, dates: string[]): void {
    if (event.type === HttpEventType.UploadProgress && event.total) {
      this.progress.set(Math.round((100 * event.loaded) / event.total));
    } else if (event.type === HttpEventType.Response && event.body) {
      this.progress.set(100);
      this.result.set(event.body);
      this.resultDates.set(dates);
    }
  }
}
