import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { formatDate } from '../../shared/date-format';
import { QuarantineState } from './quarantine.state';

@Component({
  selector: 'app-quarantine',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Registros en cuarentena</h1>
    <p class="hint">Elige una fecha y pulsa Buscar para ver los registros apartados ese día, con su motivo.</p>

    <div class="row">
      <label for="filter-date">Fecha de proceso</label>
      <input
        #dateInput
        id="filter-date"
        type="date"
        [value]="state.filter()"
        (input)="state.setFilter(dateInput.value)"
      />
      <button type="button" class="secondary" [disabled]="!state.filter() || state.loading()" (click)="state.search()">
        Buscar
      </button>
      <button type="button" class="secondary" [disabled]="state.loading()" (click)="state.clear()">Limpiar</button>
    </div>

    @if (state.error(); as failure) {
      <p class="message error" role="alert">{{ failure.message }}</p>
    }

    @if (state.loading()) {
      <p class="hint" role="status">Cargando...</p>
    } @else if (!state.filter()) {
      <p class="hint">Elige una fecha para empezar.</p>
    } @else if (!state.searched()) {
      <p class="hint">Pulsa Buscar para ver la cuarentena del {{ format(state.filter()) }}.</p>
    } @else if (state.records().length === 0) {
      <p class="hint">No hay registros en cuarentena para el {{ format(state.filter()) }}.</p>
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
              <th>Motivo</th>
              <th>Archivo</th>
              <th>En cuarentena desde</th>
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
                <td>{{ record.motivo }}</td>
                <td>{{ record.sourceFile }}</td>
                <td>{{ record.quarantinedAt }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
})
export class QuarantineComponent {
  protected readonly state = inject(QuarantineState);
  protected readonly format = formatDate;

  constructor() {
    // A link from another screen brings the date; without it the screen keeps what it had
    const date = inject(ActivatedRoute).snapshot.queryParamMap.get('processDate');
    if (date) {
      this.state.show(date);
    }
  }
}
