import { TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ApiError } from '../../core/api-error';
import { CommerceApi } from '../../core/commerce-api.service';
import { CommerceRecord, ProcessResult } from '../../core/models';
import { RecordsState } from './records.state';

const record = (id: number, date: string): CommerceRecord => ({
  id,
  sourceFile: 'commerce_15032024.csv',
  processDate: date,
  codComRed: String(1000 + id),
  nomComRed: 'Bazar',
  tipDoc: 'RUC',
  numDoc: '123',
  createdAt: '2026-10-03T00:00:00Z',
});

function setup(api: Partial<CommerceApi>): RecordsState {
  TestBed.configureTestingModule({ providers: [{ provide: CommerceApi, useValue: api }] });
  return TestBed.inject(RecordsState);
}

describe('RecordsState', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('loads nothing until the user searches', () => {
    const listCommerce = vi.fn(() => of([record(1, '2024-03-15')]));
    const state = setup({ listCommerce });

    state.setFilter('2024-03-15');

    expect(listCommerce).not.toHaveBeenCalled();
    expect(state.records()).toEqual([]);
    expect(state.searched()).toBe(false);
  });

  it('searches by the chosen date', () => {
    const listCommerce = vi.fn(() => of([record(1, '2024-03-15'), record(2, '2024-03-15')]));
    const state = setup({ listCommerce });

    state.setFilter('2024-03-15');
    state.search();

    expect(listCommerce).toHaveBeenCalledWith('2024-03-15');
    expect(state.records()).toHaveLength(2);
    expect(state.searched()).toBe(true);
    expect(state.loading()).toBe(false);
  });

  it('does not search without a date', () => {
    const listCommerce = vi.fn(() => of([]));
    const state = setup({ listCommerce });

    state.search();

    expect(listCommerce).not.toHaveBeenCalled();
  });

  it('clears the table when the date changes', () => {
    const state = setup({ listCommerce: () => of([record(1, '2024-03-15')]) });
    state.setFilter('2024-03-15');
    state.search();

    state.setFilter('2024-03-16');

    expect(state.records()).toEqual([]);
    expect(state.searched()).toBe(false);
    expect(state.filter()).toBe('2024-03-16');
  });

  it('ignores the answer of a search made for an older date', () => {
    const pending = new Subject<CommerceRecord[]>();
    const state = setup({ listCommerce: () => pending });
    state.setFilter('2024-03-15');
    state.search();

    state.setFilter('2024-03-16');
    pending.next([record(1, '2024-03-15')]);

    expect(state.records()).toEqual([]);
    expect(state.searched()).toBe(false);
    expect(state.loading()).toBe(false);
  });

  it('shows a date coming from a link: sets it and searches it', () => {
    const listCommerce = vi.fn(() => of([record(1, '2024-03-21')]));
    const state = setup({ listCommerce });

    state.show('2024-03-21');

    expect(state.filter()).toBe('2024-03-21');
    expect(listCommerce).toHaveBeenCalledWith('2024-03-21');
    expect(state.records()).toHaveLength(1);
  });

  it('keeps its data after the screen is left and opened again', () => {
    const state = setup({ listCommerce: () => of([record(1, '2024-03-15')]) });
    state.setFilter('2024-03-15');
    state.search();

    const sameState = TestBed.inject(RecordsState);

    expect(sameState).toBe(state);
    expect(sameState.filter()).toBe('2024-03-15');
    expect(sameState.records()).toHaveLength(1);
  });

  it('clear empties the date, the table and the messages', () => {
    const state = setup({
      listCommerce: () => of([record(1, '2024-03-15')]),
      processDate: () => of<ProcessResult>({ processDate: '2024-03-15', quarantinedCount: 5 }),
    });
    state.setFilter('2024-03-15');
    state.search();
    state.process();

    state.clear();

    expect(state.filter()).toBe('');
    expect(state.records()).toEqual([]);
    expect(state.searched()).toBe(false);
    expect(state.result()).toBeNull();
    expect(state.error()).toBeNull();
  });

  it('process shows the count and reloads the same date', () => {
    const listCommerce = vi.fn(() => of([record(1, '2024-03-15')]));
    const processDate = vi.fn(() => of<ProcessResult>({ processDate: '2024-03-15', quarantinedCount: 5 }));
    const state = setup({ listCommerce, processDate });
    state.setFilter('2024-03-15');

    state.process();

    expect(processDate).toHaveBeenCalledWith('2024-03-15');
    expect(state.result()?.quarantinedCount).toBe(5);
    expect(listCommerce).toHaveBeenCalledTimes(1);
    expect(state.processing()).toBe(false);
  });

  it('process ignores a second click while the first one is running', () => {
    const pending = new Subject<ProcessResult>();
    const processDate = vi.fn(() => pending);
    const state = setup({ listCommerce: () => of([]), processDate });
    state.setFilter('2024-03-15');

    state.process();
    state.process();

    expect(processDate).toHaveBeenCalledTimes(1);
    expect(state.processing()).toBe(true);
  });

  it('changing the date removes the result of the previous process', () => {
    const state = setup({
      listCommerce: () => of([]),
      processDate: () => of<ProcessResult>({ processDate: '2024-03-15', quarantinedCount: 5 }),
    });
    state.setFilter('2024-03-15');
    state.process();

    state.setFilter('2024-03-16');

    expect(state.result()).toBeNull();
  });

  it('shows the message of a failed search and empties the table', () => {
    const state = setup({ listCommerce: () => throwError(() => new ApiError('Sin conexión', 0)) });
    state.setFilter('2024-03-15');

    state.search();

    expect(state.error()?.message).toBe('Sin conexión');
    expect(state.records()).toEqual([]);
    expect(state.loading()).toBe(false);
  });
});
