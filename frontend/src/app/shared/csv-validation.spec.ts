import { describe, expect, it } from 'vitest';
import { CsvLimits, decodeUtf8, parseDate, validateCsv, validateFileName } from './csv-validation';

const LIMITS: CsvLimits = { maxFileSizeBytes: 5 * 1024 * 1024, maxRows: 50_000 };
const HEADER = 'pc_processdate,pc_codcomred,pc_nomcomred,pc_tipdoc,pc_numdoc';

function check(text: string, fileName = 'commerce_15032024.csv') {
  return validateCsv({ fileName, size: text.length, text }, LIMITS);
}

describe('validateCsv', () => {
  it('accepts a valid comma file and detects the separator', () => {
    const result = check(`${HEADER}\n2024-03-15,1001,Bazar,RUC,0912345678001\n2024-03-15,1002,Farmacia,CED,0923456789\n`);

    expect(result.valid).toBe(true);
    expect(result.separator).toBe(',');
    expect(result.rowCount).toBe(2);
    expect(result.preview).toHaveLength(2);
  });

  it('accepts a semicolon file with dd/MM/yyyy dates', () => {
    const result = check('pc_processdate;pc_codcomred;pc_nomcomred;pc_tipdoc;pc_numdoc\n16/03/2024;1001;Bazar;RUC;123\n');

    expect(result.valid).toBe(true);
    expect(result.separator).toBe(';');
  });

  it('keeps commas inside quotes', () => {
    const result = check(`${HEADER}\n2024-03-15,1001,"Comercial, S.A.",RUC,123\n`);

    expect(result.valid).toBe(true);
    expect(result.preview[0]?.[2]).toBe('Comercial, S.A.');
  });

  it('rejects an empty file', () => {
    const result = validateCsv({ fileName: 'commerce_15032024.csv', size: 0, text: '' }, LIMITS);

    expect(result.fatalError).toBe('El archivo está vacío');
  });

  it('rejects a file with only the header', () => {
    expect(check(`${HEADER}\n`).fatalError).toBe('El archivo no contiene registros');
  });

  it('names the missing column', () => {
    const result = check('pc_processdate,pc_codcomred,pc_nomcomred,pc_tipdoc\n2024-03-15,1001,Bazar,RUC\n');

    expect(result.valid).toBe(false);
    expect(result.fatalError).toContain('pc_numdoc');
  });

  it('names the extra column', () => {
    const result = check(`${HEADER},pc_extra\n2024-03-15,1001,Bazar,RUC,123,x\n`);

    expect(result.fatalError).toContain('pc_extra');
  });

  it('reports a row with a wrong number of fields', () => {
    const result = check(`${HEADER}\n2024-03-15,1001,Bazar,RUC,123\n2024-03-15,1002,Farmacia,CED\n`);

    expect(result.valid).toBe(false);
    expect(result.errors).toEqual([{ row: 2, column: null, message: 'La fila tiene 4 campos y se esperaban 5' }]);
  });

  it('reports an impossible date with its row', () => {
    const result = check(`${HEADER}\n2024-03-15,1001,Bazar,RUC,123\n31/02/2024,1002,Farmacia,CED,456\n`);

    expect(result.errors).toHaveLength(1);
    expect(result.errors[0]).toMatchObject({ row: 2, column: 'pc_processdate' });
  });

  it('reports an empty commerce code', () => {
    const result = check(`${HEADER}\n2024-03-15,,Bazar,RUC,123\n`);

    expect(result.errors[0]).toMatchObject({ row: 1, column: 'pc_codcomred' });
  });

  it('reports repeated keys on every row involved', () => {
    const result = check(
      `${HEADER}\n2024-03-15,1003,A,RUC,1\n2024-03-15,1004,B,RUC,2\n2024-03-15,1003,C,RUC,3\n`,
    );

    expect(result.errors.map((error) => error.row)).toEqual([1, 3]);
  });

  it('warns when both date formats are mixed', () => {
    const result = check(`${HEADER}\n2024-03-15,1001,Bazar,RUC,123\n16/03/2024,1002,Farmacia,CED,456\n`);

    expect(result.valid).toBe(true);
    expect(result.warnings).toHaveLength(1);
  });

  it('rejects a file that is not text', () => {
    const result = validateCsv({ fileName: 'commerce_15032024.csv', size: 10, text: null }, LIMITS);

    expect(result.fatalError).toBe('El archivo no es un CSV de texto');
  });

  it('rejects a file above the size limit', () => {
    const result = validateCsv({ fileName: 'commerce_15032024.csv', size: 6 * 1024 * 1024, text: HEADER }, LIMITS);

    expect(result.fatalError).toContain('5 MB');
  });

  it('lists the distinct dates of the file, sorted', () => {
    const result = check(
      `${HEADER}\n16/03/2024,1001,A,RUC,1\n2024-03-15,1002,B,RUC,2\n2024-03-15,1003,C,RUC,3\n`,
    );

    expect(result.dates).toEqual(['2024-03-15', '2024-03-16']);
  });

  it('limits the preview to 50 rows', () => {
    const rows = Array.from({ length: 80 }, (_, i) => `2024-03-15,${i + 1},Bazar,RUC,123`).join('\n');
    const result = check(`${HEADER}\n${rows}\n`);

    expect(result.rowCount).toBe(80);
    expect(result.preview).toHaveLength(50);
  });
});

describe('validateFileName', () => {
  it('accepts commerce_DDMMYYYY.csv', () => {
    expect(validateFileName('commerce_15032024.csv')).toBeNull();
  });

  it.each(['commerce_31022024.csv', 'comercio_15032024.csv', 'commerce_15032024.txt'])('rejects %s', (name) => {
    expect(validateFileName(name)).not.toBeNull();
  });
});

describe('parseDate', () => {
  it('parses both formats', () => {
    expect(parseDate('2024-03-15')).toEqual({ iso: '2024-03-15', format: 'iso' });
    expect(parseDate('15/03/2024')).toEqual({ iso: '2024-03-15', format: 'slash' });
  });

  it('rejects impossible dates', () => {
    expect(parseDate('31/02/2024')).toBeNull();
    expect(parseDate('2024-13-01')).toBeNull();
    expect(parseDate('15-03-2024')).toBeNull();
  });
});

describe('decodeUtf8', () => {
  it('decodes text and removes the BOM', () => {
    const bytes = new Uint8Array([0xef, 0xbb, 0xbf, 0x61, 0x62]);

    expect(decodeUtf8(bytes.buffer)).toBe('ab');
  });

  it('returns null for invalid UTF-8 and for null bytes', () => {
    expect(decodeUtf8(new Uint8Array([0xff, 0xfe, 0x00]).buffer)).toBeNull();
    expect(decodeUtf8(new Uint8Array([0x61, 0x00, 0x62]).buffer)).toBeNull();
  });
});
