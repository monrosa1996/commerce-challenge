import { HttpResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Subject, of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { CommerceApi } from '../../core/commerce-api.service';
import { UploadResult } from '../../core/models';
import { UploadState } from './upload.state';

const HEADER = 'pc_processdate,pc_codcomred,pc_nomcomred,pc_tipdoc,pc_numdoc';
const VALID = `${HEADER}\n2024-03-15,1001,Bazar,RUC,123\n2024-03-16,1002,Farmacia,CED,456\n`;
const INVALID = `${HEADER}\n31/02/2024,1001,Bazar,RUC,123\n`;

function fakeFile(name: string, text: string): File {
  const bytes = new TextEncoder().encode(text);

  return { name, size: bytes.length, arrayBuffer: () => Promise.resolve(bytes.buffer as ArrayBuffer) } as unknown as File;
}

const response = (body: UploadResult) => new HttpResponse<UploadResult>({ status: 201, body });
const uploaded: UploadResult = { sourceFile: 'commerce_15032024.csv', insertedCount: 2, warnings: [] };

function setup(api: Partial<CommerceApi>): UploadState {
  TestBed.configureTestingModule({ providers: [{ provide: CommerceApi, useValue: api }] });
  return TestBed.inject(UploadState);
}

describe('UploadState', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('checks the chosen file and lists its dates', async () => {
    const state = setup({});

    await state.select(fakeFile('commerce_15032024.csv', VALID));

    expect(state.canSend()).toBe(true);
    expect(state.validation()?.dates).toEqual(['2024-03-15', '2024-03-16']);
  });

  it('does not allow sending a file with errors', async () => {
    const state = setup({});

    await state.select(fakeFile('commerce_15032024.csv', INVALID));

    expect(state.canSend()).toBe(false);
    expect(state.errorRows().has(1)).toBe(true);
  });

  it('after a successful upload it keeps the dates to link to Registros', async () => {
    const state = setup({ uploadFile: () => of(response(uploaded)) });
    await state.select(fakeFile('commerce_15032024.csv', VALID));

    state.submit();

    expect(state.result()?.insertedCount).toBe(2);
    expect(state.resultDates()).toEqual(['2024-03-15', '2024-03-16']);
    expect(state.uploading()).toBe(false);
  });

  it('sends only once when the button is clicked twice', async () => {
    const pending = new Subject<HttpResponse<UploadResult>>();
    const uploadFile = vi.fn(() => pending);
    const state = setup({ uploadFile });
    await state.select(fakeFile('commerce_15032024.csv', VALID));

    state.submit();
    state.submit();

    expect(uploadFile).toHaveBeenCalledTimes(1);
    expect(state.uploading()).toBe(true);
  });

  it('cancel stops the upload and shows a notice', async () => {
    const pending = new Subject<HttpResponse<UploadResult>>();
    const state = setup({ uploadFile: () => pending });
    await state.select(fakeFile('commerce_15032024.csv', VALID));
    state.submit();

    state.cancel();

    expect(state.uploading()).toBe(false);
    expect(state.notice()).toContain('Carga cancelada');
    expect(state.result()).toBeNull();
  });

  it('choosing another file removes the previous result', async () => {
    const state = setup({ uploadFile: () => of(response(uploaded)) });
    await state.select(fakeFile('commerce_15032024.csv', VALID));
    state.submit();

    await state.select(fakeFile('commerce_16032024.csv', VALID));

    expect(state.result()).toBeNull();
    expect(state.resultDates()).toEqual([]);
  });

  it('clear empties the file, the preview and the result', async () => {
    const state = setup({ uploadFile: () => of(response(uploaded)) });
    await state.select(fakeFile('commerce_15032024.csv', VALID));
    state.submit();

    state.clear();

    expect(state.file()).toBeNull();
    expect(state.validation()).toBeNull();
    expect(state.result()).toBeNull();
    expect(state.resultDates()).toEqual([]);
    expect(state.canSend()).toBe(false);
  });

  it('clear does nothing while an upload is running', async () => {
    const state = setup({ uploadFile: () => new Subject<HttpResponse<UploadResult>>() });
    await state.select(fakeFile('commerce_15032024.csv', VALID));
    state.submit();

    state.clear();

    expect(state.file()).not.toBeNull();
    expect(state.uploading()).toBe(true);
  });

  it('keeps its data after the screen is left and opened again', async () => {
    const state = setup({});
    await state.select(fakeFile('commerce_15032024.csv', VALID));

    const sameState = TestBed.inject(UploadState);

    expect(sameState).toBe(state);
    expect(sameState.file()?.name).toBe('commerce_15032024.csv');
    expect(sameState.validation()?.valid).toBe(true);
  });
});
