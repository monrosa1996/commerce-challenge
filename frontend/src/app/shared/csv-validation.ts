import { RowError } from '../core/models';

/**
 * Local validation of the CSV file. It gives an answer before sending,
 * but the API repeats every rule and has the final word.
 * Pure functions: no Angular, no browser objects.
 */

export const EXPECTED_COLUMNS = [
  'pc_processdate',
  'pc_codcomred',
  'pc_nomcomred',
  'pc_tipdoc',
  'pc_numdoc',
] as const;

export const MAX_REPORTED_ERRORS = 50;
export const PREVIEW_ROWS = 50;

const FILE_NAME_PATTERN = /^commerce_(\d{2})(\d{2})(\d{4})\.csv$/i;
const ISO_DATE_PATTERN = /^(\d{4})-(\d{2})-(\d{2})$/;
const SLASH_DATE_PATTERN = /^(\d{2})\/(\d{2})\/(\d{4})$/;

const MAX_LENGTHS: ReadonlyArray<readonly [string, number]> = [
  ['pc_codcomred', 20],
  ['pc_nomcomred', 150],
  ['pc_tipdoc', 10],
  ['pc_numdoc', 30],
];

export interface CsvLimits {
  maxFileSizeBytes: number;
  maxRows: number;
}

export interface CsvInput {
  fileName: string;
  size: number;
  /** Decoded text, or null when the bytes are not valid UTF-8 text. */
  text: string | null;
}

export type Separator = ',' | ';';

export interface CsvValidation {
  valid: boolean;
  /** A problem with the whole file (name, size, header...). Row problems go to `errors`. */
  fatalError: string | null;
  separator: Separator | null;
  headers: string[];
  preview: string[][];
  rowCount: number;
  /** Distinct dates (yyyy-MM-dd) found in the file, sorted. */
  dates: string[];
  errors: RowError[];
  warnings: string[];
}

export interface ParsedDate {
  iso: string;
  format: 'iso' | 'slash';
}

/** Returns null when the bytes are not valid UTF-8 or contain null bytes. A leading BOM is removed. */
export function decodeUtf8(buffer: ArrayBuffer): string | null {
  const bytes = new Uint8Array(buffer);
  if (bytes.includes(0)) {
    return null;
  }

  try {
    return new TextDecoder('utf-8', { fatal: true }).decode(bytes);
  } catch {
    return null;
  }
}

export function validateFileName(fileName: string): string | null {
  const match = FILE_NAME_PATTERN.exec(fileName);
  if (!match) {
    return 'El nombre del archivo debe tener el formato commerce_DDMMYYYY.csv';
  }

  if (!isRealDate(Number(match[3]), Number(match[2]), Number(match[1]))) {
    return 'El nombre del archivo contiene una fecha inválida';
  }

  return null;
}

/** Accepts yyyy-MM-dd and dd/MM/yyyy with a real calendar date. */
export function parseDate(value: string): ParsedDate | null {
  const iso = ISO_DATE_PATTERN.exec(value);
  if (iso) {
    return build(Number(iso[1]), Number(iso[2]), Number(iso[3]), 'iso');
  }

  const slash = SLASH_DATE_PATTERN.exec(value);
  if (slash) {
    return build(Number(slash[3]), Number(slash[2]), Number(slash[1]), 'slash');
  }

  return null;
}

export function detectSeparator(text: string): Separator | null {
  const lineEnd = text.search(/[\r\n]/);
  const header = lineEnd < 0 ? text : text.slice(0, lineEnd);
  const commas = header.split(',').length - 1;
  const semicolons = header.split(';').length - 1;

  if (commas === 0 && semicolons === 0) {
    return null;
  }

  return commas >= semicolons ? ',' : ';';
}

/** Small CSV reader: quoted fields, escaped quotes, CRLF. Blank lines are skipped. Fields are trimmed. */
export function parseCsv(text: string, separator: Separator): string[][] {
  const records: string[][] = [];
  let record: string[] = [];
  let field = '';
  let inQuotes = false;

  const endField = (): void => {
    record.push(field.trim());
    field = '';
  };

  const endRecord = (): void => {
    endField();
    if (!record.every((value) => value === '')) {
      records.push(record);
    }
    record = [];
  };

  for (let i = 0; i < text.length; i++) {
    const char = text.charAt(i);

    if (inQuotes) {
      if (char === '"') {
        if (text.charAt(i + 1) === '"') {
          field += '"';
          i++;
        } else {
          inQuotes = false;
        }
      } else {
        field += char;
      }
    } else if (char === '"' && field === '') {
      inQuotes = true;
    } else if (char === separator) {
      endField();
    } else if (char === '\n') {
      endRecord();
    } else if (char !== '\r') {
      field += char;
    }
  }

  if (field !== '' || record.length > 0) {
    endRecord();
  }

  return records;
}

export function validateCsv(input: CsvInput, limits: CsvLimits): CsvValidation {
  if (input.size === 0) {
    return fatal('El archivo está vacío');
  }

  const nameError = validateFileName(input.fileName);
  if (nameError) {
    return fatal(nameError);
  }

  if (input.size > limits.maxFileSizeBytes) {
    return fatal(`El archivo supera el máximo de ${limits.maxFileSizeBytes / 1024 / 1024} MB`);
  }

  if (input.text === null) {
    return fatal('El archivo no es un CSV de texto');
  }

  const separator = detectSeparator(input.text);
  if (separator === null) {
    return fatal("No se pudo detectar el separador (',' o ';') en el encabezado");
  }

  const records = parseCsv(input.text, separator);
  const headerRecord = records[0];
  if (!headerRecord) {
    return fatal('El archivo no contiene registros', { separator });
  }

  const headers = headerRecord;
  const names = headers.map((name) => name.toLowerCase());

  const structureError = checkHeader(names);
  if (structureError) {
    return fatal(structureError, { separator, headers });
  }

  if (records.length === 1) {
    return fatal('El archivo no contiene registros', { separator, headers });
  }

  if (records.length - 1 > limits.maxRows) {
    return fatal(`El archivo supera el máximo de ${limits.maxRows} filas`, { separator, headers });
  }

  const { errors, warnings, dates } = checkRows(records, names);

  return {
    valid: errors.length === 0,
    fatalError: null,
    separator,
    headers,
    preview: records.slice(1, 1 + PREVIEW_ROWS),
    rowCount: records.length - 1,
    dates,
    errors,
    warnings,
  };
}

function fatal(message: string, partial: Partial<CsvValidation> = {}): CsvValidation {
  return {
    valid: false,
    fatalError: message,
    separator: null,
    headers: [],
    preview: [],
    rowCount: 0,
    dates: [],
    errors: [],
    warnings: [],
    ...partial,
  };
}

function checkHeader(names: string[]): string | null {
  const missing = EXPECTED_COLUMNS.filter((column) => !names.includes(column));
  const extra = [...new Set(names.filter((name) => !(EXPECTED_COLUMNS as readonly string[]).includes(name)))];
  const repeated = [...new Set(names.filter((name, index) => names.indexOf(name) !== index))];

  if (missing.length === 0 && extra.length === 0 && repeated.length === 0) {
    return null;
  }

  const parts: string[] = [];
  if (missing.length > 0) parts.push(`Faltan columnas: ${missing.join(', ')}.`);
  if (extra.length > 0) parts.push(`Sobran columnas: ${extra.join(', ')}.`);
  if (repeated.length > 0) parts.push(`Columnas repetidas: ${repeated.join(', ')}.`);

  return `El encabezado no tiene la estructura esperada. ${parts.join(' ')}`;
}

function checkRows(
  records: string[][],
  names: string[],
): { errors: RowError[]; warnings: string[]; dates: string[] } {
  const errors: RowError[] = [];
  const keyRows = new Map<string, number[]>();
  const formats = new Set<string>();
  const dates = new Set<string>();
  const index = (column: string): number => names.indexOf(column);

  // records[0] is the header, so the data row number equals the index
  for (let row = 1; row < records.length && errors.length < MAX_REPORTED_ERRORS; row++) {
    const fields = records[row] ?? [];

    if (fields.length !== names.length) {
      errors.push({
        row,
        column: null,
        message: `La fila tiene ${fields.length} campos y se esperaban ${names.length}`,
      });
      continue;
    }

    const dateText = fields[index('pc_processdate')] ?? '';
    const code = fields[index('pc_codcomred')] ?? '';
    let valid = true;

    const date = parseDate(dateText);
    if (date) {
      formats.add(date.format);
      dates.add(date.iso);
    } else {
      errors.push({ row, column: 'pc_processdate', message: `Fecha inválida: '${dateText}'` });
      valid = false;
    }

    if (code === '') {
      errors.push({ row, column: 'pc_codcomred', message: 'El código del comercio está vacío' });
      valid = false;
    }

    for (const [column, max] of MAX_LENGTHS) {
      if ((fields[index(column)] ?? '').length > max) {
        errors.push({ row, column, message: `El valor supera los ${max} caracteres` });
        valid = false;
      }
    }

    if (valid && date) {
      const key = `${date.iso}|${code}`;
      keyRows.set(key, [...(keyRows.get(key) ?? []), row]);
    }
  }

  for (const [key, rows] of keyRows) {
    if (rows.length > 1) {
      for (const row of rows) {
        errors.push({
          row,
          column: 'pc_codcomred',
          message: `Clave repetida ${key} en las filas ${rows.join(', ')}`,
        });
      }
    }
  }

  errors.sort((a, b) => (a.row ?? 0) - (b.row ?? 0));

  const warnings =
    formats.size > 1 ? ['El archivo mezcla fechas con formato yyyy-MM-dd y dd/MM/yyyy. Se cargará igual.'] : [];

  return { errors: errors.slice(0, MAX_REPORTED_ERRORS), warnings, dates: [...dates].sort() };
}

function build(year: number, month: number, day: number, format: 'iso' | 'slash'): ParsedDate | null {
  if (!isRealDate(year, month, day)) {
    return null;
  }

  const pad = (value: number, length: number): string => String(value).padStart(length, '0');

  return { iso: `${pad(year, 4)}-${pad(month, 2)}-${pad(day, 2)}`, format };
}

function isRealDate(year: number, month: number, day: number): boolean {
  const date = new Date(Date.UTC(year, month - 1, day));

  return (
    year >= 1000 &&
    date.getUTCFullYear() === year &&
    date.getUTCMonth() === month - 1 &&
    date.getUTCDate() === day
  );
}
