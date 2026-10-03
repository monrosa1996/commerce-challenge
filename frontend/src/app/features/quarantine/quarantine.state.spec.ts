import { TestBed } from '@angular/core/testing';
import { Subject, of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { CommerceApi } from '../../core/commerce-api.service';
import { QuarantineRecord } from '../../core/models';
import { QuarantineState } from './quarantine.state';

const record = (id: number): QuarantineRecord => ({
  id,
  sourceFile: 'commerce_15032024.csv',
  processDate: '2024-03-15',
  codComRed: String(1000 + id),
  nomComRed: null,
  tipDoc: 'RUC',
  numDoc: '12A-45',
  motivo: 'El número (numdoc) contiene letras',
  quarantinedAt: '2026-10-03T00:00:00Z',
});

function setup(api: Partial<CommerceApi>): QuarantineState {
  TestBed.configureTestingModule({ providers: [{ provide: CommerceApi, useValue: api }] });
  return TestBed.inject(QuarantineState);
}

describe('QuarantineState', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('loads nothing until the user searches by date', () => {
    const listQuarantine = vi.fn(() => of([record(1)]));
    const state = setup({ listQuarantine });

    state.setFilter('2024-03-15');
    expect(listQuarantine).not.toHaveBeenCalled();

    state.search();
    expect(listQuarantine).toHaveBeenCalledWith('2024-03-15');
    expect(state.records()).toHaveLength(1);
    expect(state.searched()).toBe(true);
  });

  it('clears the table when the date changes', () => {
    const state = setup({ listQuarantine: () => of([record(1)]) });
    state.setFilter('2024-03-15');
    state.search();

    state.setFilter('2024-03-16');

    expect(state.records()).toEqual([]);
    expect(state.searched()).toBe(false);
  });

  it('ignores the answer of a search made for an older date', () => {
    const pending = new Subject<QuarantineRecord[]>();
    const state = setup({ listQuarantine: () => pending });
    state.setFilter('2024-03-15');
    state.search();

    state.setFilter('2024-03-16');
    pending.next([record(1)]);

    expect(state.records()).toEqual([]);
  });

  it('shows a date coming from a link', () => {
    const listQuarantine = vi.fn(() => of([record(1)]));
    const state = setup({ listQuarantine });

    state.show('2024-03-15');

    expect(state.filter()).toBe('2024-03-15');
    expect(state.records()).toHaveLength(1);
  });

  it('clear empties the date and the table', () => {
    const state = setup({ listQuarantine: () => of([record(1)]) });
    state.setFilter('2024-03-15');
    state.search();

    state.clear();

    expect(state.filter()).toBe('');
    expect(state.records()).toEqual([]);
    expect(state.searched()).toBe(false);
  });
});
