import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { environment } from '../../../environments/environment';
import { formatDate } from '../../shared/date-format';
import { UploadState } from './upload.state';

const MAX_DATE_LINKS = 5;

@Component({
  selector: 'app-upload',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <h1>Carga de archivo</h1>
    <p class="hint">
      Archivo <code>commerce_DDMMYYYY.csv</code>, máximo {{ maxMb }} MB y {{ maxRows }} filas.
    </p>

    <div class="row">
      <input
        #fileInput
        type="file"
        accept=".csv"
        [disabled]="state.uploading()"
        (change)="choose(fileInput)"
      />
      <button type="button" [disabled]="!state.canSend()" (click)="state.submit()">Enviar</button>
      @if (state.uploading()) {
        <button type="button" class="secondary" (click)="state.cancel()">Cancelar</button>
        <progress [value]="state.progress()" max="100"></progress>
        <span>{{ state.progress() }}%</span>
      } @else {
        <button type="button" class="secondary" [disabled]="!state.file()" (click)="clear(fileInput)">Limpiar</button>
      }
    </div>

    @if (state.file(); as chosen) {
      <p class="hint">Archivo seleccionado: <strong>{{ chosen.name }}</strong></p>
    }

    @if (state.notice(); as text) {
      <p class="message warn" role="status">{{ text }}</p>
    }

    @if (state.result(); as done) {
      <div class="message ok" role="status">
        Se cargaron {{ done.insertedCount }} registros del archivo {{ done.sourceFile }}.
        @if (linkDates().length > 0) {
          <div>
            @for (date of linkDates(); track date; let last = $last) {
              <a routerLink="/registros" [queryParams]="{ processDate: date }">Ver registros del {{ format(date) }}</a>
              @if (!last) {
                <span> · </span>
              }
            }
            @if (extraDates() > 0) {
              <span> · y {{ extraDates() }} fechas más</span>
            }
          </div>
        }
        @if (done.warnings.length > 0) {
          <ul>
            @for (warning of done.warnings; track $index) {
              <li>{{ warning }}</li>
            }
          </ul>
        }
      </div>
    }

    @if (state.error(); as failure) {
      <div class="message error" role="alert">
        {{ failure.message }}
        @if (failure.errors.length > 0) {
          <ul>
            @for (item of failure.errors; track $index) {
              <li>{{ describe(item.row, item.column, item.message) }}</li>
            }
          </ul>
        }
      </div>
    }

    @if (state.validation(); as check) {
      @if (check.fatalError) {
        <p class="message error" role="alert">{{ check.fatalError }}</p>
      } @else {
        <p>
          Separador detectado: <code>{{ check.separator }}</code> · {{ check.rowCount }} filas
        </p>

        @for (warning of check.warnings; track $index) {
          <p class="message warn">{{ warning }}</p>
        }

        @if (check.errors.length > 0) {
          <div class="message error" role="alert">
            El archivo tiene errores y no se puede enviar:
            <ul>
              @for (item of check.errors; track $index) {
                <li>{{ describe(item.row, item.column, item.message) }}</li>
              }
            </ul>
          </div>
        }

        <h2>Vista previa (primeras {{ check.preview.length }} filas)</h2>
        <div class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>#</th>
                @for (name of check.headers; track $index) {
                  <th>{{ name }}</th>
                }
              </tr>
            </thead>
            <tbody>
              @for (row of check.preview; track $index; let i = $index) {
                <tr [class.row-error]="state.errorRows().has(i + 1)">
                  <td>{{ i + 1 }}</td>
                  @for (cell of row; track $index) {
                    <td>{{ cell }}</td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    }
  `,
})
export class UploadComponent {
  protected readonly state = inject(UploadState);
  protected readonly format = formatDate;

  protected readonly maxMb = environment.maxFileSizeBytes / 1024 / 1024;
  protected readonly maxRows = environment.maxRows;

  protected readonly linkDates = computed(() => this.state.resultDates().slice(0, MAX_DATE_LINKS));
  protected readonly extraDates = computed(() => Math.max(0, this.state.resultDates().length - MAX_DATE_LINKS));

  protected choose(input: HTMLInputElement): void {
    void this.state.select(input.files?.item(0) ?? null);
  }

  protected clear(input: HTMLInputElement): void {
    this.state.clear();
    input.value = '';
  }

  protected describe(row: number | null, column: string | null, message: string): string {
    const where = [row !== null ? `Fila ${row}` : null, column].filter((part) => part !== null).join(' · ');
    return where ? `${where}: ${message}` : message;
  }
}
