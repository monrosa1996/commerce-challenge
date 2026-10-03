import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { formatDate } from '../../shared/date-format';
import { RecordsState } from './records.state';

@Component({
  selector: 'app-records',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink],
  template: `
    <h1>Registros cargados</h1>
    <p class="hint">
      Elige una fecha y pulsa Buscar para ver lo que se cargó ese día. Al procesar la fecha se revisa el nombre y el
      número de cada registro: los que tienen errores salen de esta lista y pasan a Cuarentena.
    </p>

    <div class="row">
      <label for="process-date">Fecha de proceso</label>
      <input
        #dateInput
        id="process-date"
        type="date"
        [value]="state.filter()"
        (input)="state.setFilter(dateInput.value)"
      />
      <button type="button" class="secondary" [disabled]="!state.filter() || state.loading()" (click)="state.search()">
        Buscar
      </button>
      <button type="button" [disabled]="!state.filter() || state.processing()" (click)="state.process()">
        Procesar fecha
      </button>
      <button type="button" class="secondary" [disabled]="state.loading() || state.processing()" (click)="state.clear()">
        Limpiar
      </button>
    </div>

    @if (state.result(); as done) {
      <p class="message ok" role="status">
        {{ done.quarantinedCount }} registros enviados a cuarentena.
        <a routerLink="/cuarentena" [queryParams]="{ processDate: done.processDate }">Ver cuarentena del {{ format(done.processDate) }}</a>
      </p>
    }

    @if (state.error(); as failure) {
      <p class="message error" role="alert">{{ failure.message }}</p>
    }

    @if (state.loading()) {
      <p class="hint" role="status">Cargando...</p>
    } @else if (!state.filter()) {
      <p class="hint">Elige una fecha para empezar.</p>
    } @else if (!state.searched()) {
      <p class="hint">Pulsa Buscar para ver los registros del {{ format(state.filter()) }}.</p>
    } @else if (state.records().length === 0) {
      <p class="hint">No hay registros para el {{ format(state.filter()) }}.</p>
    } @else {
      <p class="hint">{{ state.records().length }} registros del {{ format(state.filter()) }}</p>
      <div class="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Fecha</th>
              <th>Código</th>
              <th>Nombre</th>
              <th>Tipo doc.</th>
              <th>Número doc.</th>
              <th>Archivo</th>
              <th>Cargado</th>
            </tr>
          </thead>
          <tbody>
            @for (record of state.records(); track record.id) {
              <tr>
                <td>{{ record.processDate }}</td>
                <td>{{ record.codComRed }}</td>
                <td>{{ record.nomComRed }}</td>
                <td>{{ record.tipDoc }}</td>
                <td>{{ record.numDoc }}</td>
                <td>{{ record.sourceFile }}</td>
                <td>{{ record.createdAt }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
})
export class RecordsComponent {
  protected readonly state = inject(RecordsState);
  protected readonly format = formatDate;

  constructor() {
    // A link from another screen brings the date; without it the screen keeps what it had
    const date = inject(ActivatedRoute).snapshot.queryParamMap.get('processDate');
    if (date) {
      this.state.show(date);
    }
  }
}
